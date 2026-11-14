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

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => SetEnabled(false);

    public async Task SetEnabled(bool on)
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
            var args = $"--source {C("Source", "0")} --detector {C("Detector", "pose")} --zones {C("Zones", "ai/config/zones.example.json")} --host {C("Host", "localhost")} --stream-port {C("StreamPort", "8090")}";
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
