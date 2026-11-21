using System.Diagnostics;
using System.Net.Http.Json;

namespace SmartHomeAI.Dashboard.Services;

/// <summary>Một tiến trình mô phỏng (ESP32 hoặc ổ cắm); lệnh lấy từ cấu hình Sim:*, không nhận từ trình duyệt.</summary>
public class SimProc(string args, string root, string python, bool disabled = false)
{
    Process? _p;
    readonly object _l = new();
    public string? Error { get; private set; }
    public bool On => _p is { HasExited: false };

    public void Set(bool on)
    {
        lock (_l)
        {
            if (!on) { if (_p is { } p) Repo.Stop(p, 3000); _p = null; Error = null; return; }
            if (disabled) { Error = "P100 mô phỏng bị khóa khi dùng thiết bị thật"; return; }
            if (On) return;
            try
            {
                var p = Process.Start(Repo.Psi(Path.Combine(root, python), args, root, false))!;
                p.EnableRaisingEvents = true;
                p.Exited += (_, _) => { if (ReferenceEquals(p, _p)) Error = $"Mô phỏng thoát, mã {p.ExitCode}"; };
                _p = p; Error = null;
            }
            catch (Exception e) { Error = e.Message; }
        }
    }
}

/// <summary>Giữ hai tiến trình mô phỏng và dọn khi Dashboard tắt; Esp32 có cổng điều khiển HTTP cục bộ.</summary>
public class SimRunner : IHostedService
{
    readonly bool _autoPlug, _autoEsp;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    public SimProc Esp { get; }
    public SimProc Plug { get; }

    public SimRunner(IConfiguration cfg)
    {
        string C(string k, string d) => cfg["Sim:" + k] is { Length: > 0 } v ? v : d;
        var realPlug = cfg.GetValue("TapoP100:Enabled", false);
        _autoPlug = !realPlug && cfg.GetValue("Sim:AutoStartPlug", true); _autoEsp = cfg.GetValue("Sim:AutoStartEsp", false);
        var root = Repo.Root(cfg);
        var py = C("Python", "sim/.venv/bin/python");
        _http.BaseAddress = new Uri($"http://127.0.0.1:{C("EspControlPort", "8091")}");
        Esp = new SimProc(C("EspArgs", "-m sim esp --control-port 8091"), root, py);
        Plug = new SimProc(C("PlugArgs", "-m sim p100"), root, py, realPlug);
    }

    public Task StartAsync(CancellationToken ct)
    {
        if (_autoPlug) Plug.Set(true);
        if (_autoEsp) Esp.Set(true);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) { Esp.Set(false); Plug.Set(false); return Task.CompletedTask; }

    public async Task<EspState?> EspState()
    {
        try { return await _http.GetFromJsonAsync<EspState>("/state", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); }
        catch (Exception) { return null; }
    }

    /// <summary>Gửi một phần giá trị; false nếu sim không đáp hoặc từ chối (400).</summary>
    public async Task<bool> EspSet(object partial)
    {
        // StringContent có Content-Length; JsonContent gửi chunked, sim Python đọc ra body rỗng.
        try
        {
            var body = new StringContent(System.Text.Json.JsonSerializer.Serialize(partial), System.Text.Encoding.UTF8, "application/json");
            return (await _http.PostAsync("/set", body)).IsSuccessStatusCode;
        }
        catch (Exception) { return false; }
    }
}
