using System.Diagnostics;

namespace SmartHomeAI.Dashboard.Services;

public enum AiState { Stopped, Starting, Running, Error }

/// <summary>Bật/tắt bộ AI (ai/run.sh) bằng tiến trình con; tham số chỉ lấy từ cấu hình Ai:*.</summary>
public class AiRunner(IConfiguration cfg, ILogger<AiRunner> log) : IHostedService
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Queue<string> _tail = new();
    Process? _p;
    bool _stopping;
    public AiState State { get; private set; } = AiState.Stopped;
    public bool Busy => _gate.CurrentCount == 0;
    public string[] LogTail { get { lock (_tail) return [.. _tail]; } }

    // Đo trên i7-10610U, cam Tapo soi trên cao, 40 khung: tốt nhất đứng đầu (xem docs/nhat-ky-camera-tapo.md)
    public static readonly (string Name, string Label)[] Models =
    [
        ("yolov8m-pose", "YOLOv8m: đề xuất (~400 ms/khung)"),
        ("yolov8l-pose", "YOLOv8l: chính xác nhất, chậm (~780 ms/khung)"),
        ("yolov8s-pose", "YOLOv8s: nhanh (~170 ms/khung)"),
        ("yolov8n-pose", "YOLOv8n: nhẹ nhất, hay sót (~70 ms/khung)"),
        // Chưa đo trên cam Tapo: chọn thử rồi so với YOLOv8m
        ("yolo26m-pose", "YOLO26m: đời mới, không cần NMS"),
        ("yolo26l-pose", "YOLO26l: đời mới, bản lớn"),
        ("yolo26s-pose", "YOLO26s: đời mới, bản nhỏ"),
        ("yolo26n-pose", "YOLO26n: đời mới, nhẹ nhất"),
        ("rtmo-m", "RTMO-m: khớp xương chính xác hơn"),
        ("rtmo-l", "RTMO-l: khớp xương chính xác nhất, nặng"),
        ("rtmo-s", "RTMO-s: bản nhỏ"),
    ];
    string ModelFile => Path.Combine(Repo.Root(cfg), "ai/config/model.txt");
    public IEnumerable<(string Name, string Label)> Available =>
        Models.Where(m => File.Exists(Path.Combine(Repo.Root(cfg), "ai/models", m.Name + ".onnx")));
    public string Model
    {
        get
        {
            string? m = null;
            try { m = File.ReadAllText(ModelFile).Trim(); } catch (IOException) { }
            m ??= cfg["Ai:Model"];
            return Models.Any(x => x.Name == m) ? m! : Models[0].Name;
        }
    }

    /// <summary>Chỉ nhận tên trong danh sách cố định; lưu lựa chọn và khởi động lại AI nếu đang chạy.</summary>
    public async Task SetModel(string m)
    {
        if (!Models.Any(x => x.Name == m) || !await _gate.WaitAsync(0)) return;
        try
        {
            File.WriteAllText(ModelFile, m);
            if (_p is { HasExited: false }) { Stop(); await Start(); }
        }
        finally { _gate.Release(); }
    }

    string IpFile => Path.Combine(Repo.Root(cfg), "ai/config/camera-ip.txt");
    /// <summary>IP camera đặt trên giao diện; null thì dùng nguyên Ai:Source.</summary>
    public string? CameraIp { get { try { return File.ReadAllText(IpFile).Trim() is { Length: > 0 } s ? s : null; } catch (IOException) { return null; } } }

    /// <summary>Chỉ nhận IPv4 hợp lệ (đi vào dòng lệnh AI); lưu và khởi động lại AI nếu đang chạy.</summary>
    public async Task<bool> SetCameraIp(string ip)
    {
        ip = ip.Trim();
        if (!IsIpv4(ip) || !await _gate.WaitAsync(0)) return false;
        try
        {
            File.WriteAllText(IpFile, ip);
            if (_p is { HasExited: false }) { Stop(); await Start(); }
            return true;
        }
        finally { _gate.Release(); }
    }

    static bool IsIpv4(string s) => System.Net.IPAddress.TryParse(s, out var a)
        && a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && a.ToString() == s; // "1.2" cũng parse được, so lại để chặn

    /// <summary>Thay host trong URL nguồn (rtsp://user:pass@HOST:554/...) bằng IP đã đặt; nguồn không phải URL (webcam "0") giữ nguyên.</summary>
    public static string WithHost(string src, string? ip) =>
        ip is null ? src : System.Text.RegularExpressions.Regex.Replace(src, @"^([a-z]+://(?:[^@/]*@)?)[^:/@]+", "${1}" + ip);

    public string Source => WithHost(cfg["Ai:Source"] ?? "0", CameraIp);

    // Người dùng đã bật AI thì nhớ lại (cạnh model.txt): Dashboard khởi động lại (build, mất điện) tự bật, Hub không mất dữ liệu hiện diện
    string EnabledFile => Path.Combine(Repo.Root(cfg), "ai/config/ai-enabled.txt");

    public Task StartAsync(CancellationToken ct)
    {
        if (File.Exists(EnabledFile)) _ = Toggle(true); // không chờ: nạp mô hình mất vài giây, không chặn Dashboard lên
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Toggle(false); // tắt theo Dashboard nhưng giữ lựa chọn đã lưu

    public async Task SetEnabled(bool on)
    {
        try { if (on) File.WriteAllText(EnabledFile, "1"); else File.Delete(EnabledFile); }
        catch (IOException e) { log.LogWarning("Không lưu được trạng thái bật AI: {Msg}", e.Message); }
        await Toggle(on);
    }

    async Task Toggle(bool on)
    {
        if (!await _gate.WaitAsync(0)) return; // chống bấm trùng
        try { if (on) await Start(); else Stop(); }
        finally { _gate.Release(); }
    }

    void Add(string? l) { if (l is null) return; lock (_tail) { _tail.Enqueue(l); while (_tail.Count > 6) _tail.Dequeue(); } }

    async Task Start()
    {
        if (_p is { HasExited: false }) return;
        try
        {
            lock (_tail) _tail.Clear();
            var root = Repo.Root(cfg);
            string C(string k, string d) => cfg["Ai:" + k] ?? d;
            var args = $"--source {Source} --detector {C("Detector", "pose")} {(C("Zones", "") is { Length: > 0 } z ? $"--zones {z} " : "")}--host {C("Host", "localhost")} --stream-port {C("StreamPort", "8090")}" + (cfg.GetValue<bool>("Ai:SofaLying") ? " --sofa-lying" : "")
                + $" --model ai/models/{Model}.onnx";
            State = AiState.Starting; _stopping = false;
            var p = Process.Start(Repo.Psi(Path.Combine(root, "ai/run.sh"), args, root, true))!;
            p.OutputDataReceived += (_, e) => Add(e.Data);
            p.ErrorDataReceived += (_, e) => Add(e.Data);
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) =>
            {
                if (!ReferenceEquals(p, _p) || _stopping) return;
                Add($"AI thoát, mã {p.ExitCode}"); State = AiState.Error;
            };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            _p = p;
            await Task.Delay(2500);
            if (!p.HasExited) State = AiState.Running;
            log.LogInformation("AI {State} (pid {Pid})", State, p.Id);
        }
        catch (Exception e) { Add(e.Message); State = AiState.Error; log.LogWarning("Không bật được AI: {Msg}", e.Message); }
    }

    void Stop()
    {
        if (_p is not { } p) { if (State != AiState.Error) State = AiState.Stopped; return; }
        _stopping = true;
        Repo.Stop(p, 5000);
        _p = null; State = AiState.Stopped;
    }
}
