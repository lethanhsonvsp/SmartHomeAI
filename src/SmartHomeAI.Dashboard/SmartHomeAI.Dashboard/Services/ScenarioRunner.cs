using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHomeAI.Dashboard.Services;

public record Scenario(string Code, string Title, string? Description, double DurationSec, string[] Expect, string[] Requires, string Group);
public enum ScenarioState { Idle, Running, Done, Error }

/// <summary>Chạy một kịch bản `python -m sim run CODE --speed N` mỗi lần. Lệnh từ Sim:*; mã và tốc độ chỉ nhận khi nằm trong danh sách đã lấy.</summary>
public partial class ScenarioRunner : IHostedService
{
    public static readonly int[] Speeds = [1, 2, 5, 10];
    public const int DefaultSpeed = 5;
    public const string SpeedNote = "Hub tính thời gian thật: tốc độ cao có thể làm hệ thống không kịp phản ứng; dùng 1x để thấy đúng hành vi thật";
    [GeneratedRegex("^[A-Z]{1,2}[0-9]{2}$")] private static partial Regex CodeRx();
    [GeneratedRegex("^SC[0-9]{2}$")] private static partial Regex ScRx();

    readonly IConfiguration _cfg;
    readonly SimRunner _sim;
    readonly AiRunner _ai;
    readonly object _l = new();
    readonly Queue<string> _tail = new();
    Process? _p;
    bool _stopped;
    public IReadOnlyList<Scenario> List { get; private set; } = [];
    public string? ListError { get; private set; }
    public ScenarioState State { get; private set; }
    public string? Code { get; private set; }
    public DateTime StartedAt { get; private set; }
    public double EstSec { get; private set; }
    public string? Error { get; private set; }
    public string? Note { get; private set; }
    public string[] LogTail { get { lock (_tail) return [.. _tail]; } }
    public bool Running => State == ScenarioState.Running;
    public bool AiBlocks => _ai.State is AiState.Running or AiState.Starting;
    public double Progress => Running ? Math.Min(.98, (DateTime.UtcNow - StartedAt).TotalSeconds / Math.Max(EstSec, .5)) : State == ScenarioState.Done ? 1 : 0;

    public ScenarioRunner(IConfiguration cfg, SimRunner sim, AiRunner ai) { _cfg = cfg; _sim = sim; _ai = ai; }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) { Stop(); return Task.CompletedTask; }

    string Py => Path.Combine(Repo.Root(_cfg), _cfg["Sim:Python"] is { Length: > 0 } p ? p : "sim/.venv/bin/python");

    /// <summary>Lấy danh sách từ `sim list --json` (lệnh cấu hình Sim:ListArgs).</summary>
    public async Task Reload()
    {
        try
        {
            var root = Repo.Root(_cfg);
            var psi = Repo.Psi(Py, _cfg["Sim:ListArgs"] is { Length: > 0 } a ? a : "-m sim list --json", root, true);
            using var p = Process.Start(psi)!;
            var outT = p.StandardOutput.ReadToEndAsync(); var errT = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(15000);
            try { await p.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { p.Kill(true); throw new Exception("Lấy danh sách kịch bản quá thời gian"); }
            if (p.ExitCode != 0) throw new Exception("sim list lỗi: " + (await errT).Trim().Split('\n').LastOrDefault());
            using var doc = JsonDocument.Parse(await outT);
            List = [.. doc.RootElement.EnumerateArray().Select(e => new Scenario(
                e.GetProperty("code").GetString()!, e.GetProperty("title").GetString() ?? "",
                e.TryGetProperty("description", out var d) ? d.GetString() : null,
                e.TryGetProperty("durationSec", out var s) ? s.GetDouble() : 0,
                Arr(e, "expect"), Arr(e, "requires"), "Thực tế (R)"))
                .Where(x => CodeRx().IsMatch(x.Code) && x.Code.StartsWith('R'))];
            // `sim list --json` chỉ có R; SC lấy từ `sim list` (chỉ có mã). N, S là bộ test chống nhiễu nên bỏ qua.
            var sc = await Plain(root);
            List = [.. sc.Where(c => !List.Any(x => x.Code == c)).Select(c => new Scenario(c, "Kịch bản cơ bản " + c, "Kịch bản kiểm tra cơ bản " + c, 0, [], ["p100"], "Cơ bản (SC)")), .. List];
            ListError = null;
        }
        catch (Exception e) { ListError = e.Message; }
    }

    async Task<string[]> Plain(string root)
    {
        try
        {
            using var p = Process.Start(Repo.Psi(Py, "-m sim list", root, true))!;
            var outT = p.StandardOutput.ReadToEndAsync(); _ = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(15000);
            try { await p.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { p.Kill(true); return []; }
            return p.ExitCode != 0 ? [] : [.. (await outT).Split((char[])[' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Where(c => ScRx().IsMatch(c)).Distinct()];
        }
        catch (Exception) { return []; }
    }

    static string[] Arr(JsonElement e, string k) => e.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array ? [.. a.EnumerateArray().Select(x => x.GetString() ?? "")] : [];

    void Add(string? l) { if (l is null) return; lock (_tail) { _tail.Enqueue(l); while (_tail.Count > 6) _tail.Dequeue(); } }

    /// <summary>Trả lý do từ chối, null nếu đã bắt đầu chạy.</summary>
    public async Task<string?> Run(string code, int speed)
    {
        if (!CodeRx().IsMatch(code) || List.FirstOrDefault(x => x.Code == code) is not { } sc) return "Kịch bản không hợp lệ";
        if (!Speeds.Contains(speed)) return "Tốc độ không hợp lệ";
        if (AiBlocks) return "Tắt AI camera trước (kịch bản và camera cùng gửi dữ liệu người)";
        lock (_l)
        {
            if (Running) return "Đang có kịch bản chạy";
            State = ScenarioState.Running; Code = code; Error = null; StartedAt = DateTime.UtcNow; EstSec = sc.DurationSec / speed; _stopped = false;
            lock (_tail) _tail.Clear();
        }
        try
        {
            // Bật mô phỏng mà kịch bản cần (và chờ chúng nối broker) trước khi phát.
            // Kịch bản tự phát cảm biến; ESP32 giả sẽ đè lux/nhiệt độ mặc định nên tắt và không bật lại ("esp" trong requires bị bỏ qua).
            Note = null;
            if (_sim.Esp.On) { _sim.Esp.Set(false); Note = "Đã tắt ESP32 giả trong lúc chạy kịch bản vì kịch bản tự phát dữ liệu cảm biến"; Add(Note); }
            if (sc.Requires.Contains("p100") && !_sim.Plug.On) { _sim.Plug.Set(true); await Task.Delay(2000); }
            if (_stopped) return null;
            var root = Repo.Root(_cfg);
            var p = Process.Start(Repo.Psi(Py, $"-u -m sim run {code} --speed {speed}", root, true))!;
            p.OutputDataReceived += (_, e) => Add(e.Data);
            p.ErrorDataReceived += (_, e) => Add(e.Data);
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) =>
            {
                if (!ReferenceEquals(p, _p) || _stopped) return;
                if (p.ExitCode == 0) State = ScenarioState.Done; else { State = ScenarioState.Error; Error = $"Kịch bản thoát, mã {p.ExitCode}"; }
            };
            _p = p;
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if (p.HasExited && p.ExitCode == 0 && State == ScenarioState.Running) State = ScenarioState.Done;
        }
        catch (Exception e) { State = ScenarioState.Error; Error = e.Message; }
        return null;
    }

    public void Stop()
    {
        _stopped = true;
        if (_p is { } p) Repo.Stop(p, 2000);
        _p = null;
        if (Running) { State = ScenarioState.Idle; Add("Đã dừng"); }
    }
}
