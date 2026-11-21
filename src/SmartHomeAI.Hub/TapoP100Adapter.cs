using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace SmartHomeAI.Hub;

public sealed record TapoLogin(string User, string Password, string Host);

/// <summary>Routes one configured Hub device to a local TPAP bridge. Login arrives once via API (or env) and is remembered in a 0600 file
/// until logout, so a Hub restart reconnects by itself. Only a timeout (plug not on the network yet) is retried; auth failures never are, to avoid a lockout.</summary>
public sealed class TapoP100Adapter(IConfiguration cfg, IMqttPublisher bus, MessageRouter router, ILogger<TapoP100Adapter> log) : BackgroundService
{
    readonly Channel<bool> commands = Channel.CreateBounded<bool>(new BoundedChannelOptions(32) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    readonly Channel<TapoLogin> logins = Channel.CreateBounded<TapoLogin>(1);
    CancellationTokenSource? session;
    volatile string state = "idle"; // idle | connecting | connected | error
    volatile string? error;
    public (string State, string? Error) Status => (state, error);
    public string? Host { get; private set; }
    string Device => cfg["TapoP100:DeviceId"] ?? "light";

    /// <summary>False when a session is already active/starting or input is invalid; credentials stay in memory only.</summary>
    public bool Login(string? user, string? password, string? host)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password) || !System.Net.IPAddress.TryParse(host, out _)) return false;
        if (state is "connecting" or "connected") return false;
        error = null; state = "connecting"; Host = host;
        if (logins.Writer.TryWrite(new(user.Trim(), password, host!))) return true;
        state = "idle"; return false;
    }

    public void Logout() { session?.Cancel(); error = null; state = "idle"; Forget(); }

    string SavePath => cfg["TapoP100:SavePath"] ?? "./data/tapo.json";
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(60);

    TapoLogin? Saved()
    {
        try { return File.Exists(SavePath) ? JsonSerializer.Deserialize<TapoLogin>(File.ReadAllText(SavePath)) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { log.LogWarning("Cannot read saved Tapo login ({Type})", ex.GetType().Name); return null; }
    }

    void Remember(TapoLogin login)
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(SavePath)) is { } dir) Directory.CreateDirectory(dir);
            // Tạo tệp với quyền 0600 trước khi ghi mật khẩu vào
            using (var f = new FileStream(SavePath, new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write,
                UnixCreateMode = OperatingSystem.IsWindows() ? null : UnixFileMode.UserRead | UnixFileMode.UserWrite }))
                JsonSerializer.Serialize(f, login);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log.LogWarning("Cannot save Tapo login ({Type}); it stays in memory only", ex.GetType().Name); }
    }

    void Forget()
    {
        try { File.Delete(SavePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log.LogWarning("Cannot delete saved Tapo login ({Type})", ex.GetType().Name); }
    }

    public void Receive(string topic, string payload, bool retained)
    {
        if (state != "connected" || retained || topic != $"home/device/{Device}/set") return;
        try
        {
            if (MqttParser.Parse(topic, payload) is DeviceSetMsg command && !commands.Writer.TryWrite(command.On))
                log.LogWarning("Tapo command queue full; command dropped");
        }
        catch (FormatException) { log.LogWarning("Invalid Tapo command ignored"); }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (Device is not ("light" or "fan")) { log.LogError("TapoP100:DeviceId must be light or fan"); return; }
        if (cfg.GetValue("TapoP100:Enabled", false)
            && Environment.GetEnvironmentVariable("TAPO_USERNAME") is { } u && Environment.GetEnvironmentVariable("TAPO_PASSWORD") is { } pw)
            Login(u, pw, cfg["TapoP100:Host"] ?? "192.168.0.8");
        else if (Saved() is { } saved) Login(saved.User, saved.Password, saved.Host);
        TapoLogin? retry = null;
        while (!ct.IsCancellationRequested)
        {
            TapoLogin login;
            if (retry is null) login = await logins.Reader.ReadAsync(ct);
            else
            {
                // Chờ rồi thử lại phiên vừa hết thời gian chờ, trừ khi có đăng nhập mới hoặc đã đăng xuất
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wait.CancelAfter(RetryDelay);
                try { await logins.Reader.WaitToReadAsync(wait.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                if (logins.Reader.TryRead(out var fresh)) login = fresh;
                else if (state == "error" && error == "timeout") { login = retry; error = null; state = "connecting"; }
                else { retry = null; continue; }
            }
            retry = null;
            using var s = CancellationTokenSource.CreateLinkedTokenSource(ct);
            session = s;
            try { await Run(login, s.Token); }
            finally
            {
                session = null;
                if (state != "error") state = "idle";
                while (commands.Reader.TryRead(out _)) { } // lệnh cũ không được áp vào phiên sau
            }
            // chỉ thử lại đăng nhập từng thành công (đã lưu): gõ nhầm IP thì báo lỗi rồi dừng
            if (state == "error" && error == "timeout" && Saved() == login) retry = login;
        }
    }

    async Task Run(TapoLogin login, CancellationToken ct)
    {
        var root = cfg["TapoP100:Root"] ?? FindRoot();
        var psi = new ProcessStartInfo(cfg["TapoP100:Python"] ?? Path.Combine(root, ".venv/bin/python"))
        {
            WorkingDirectory = root, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        };
        psi.ArgumentList.Add("-u"); psi.ArgumentList.Add("-m"); psi.ArgumentList.Add("src.tapo_p100.bridge");
        psi.Environment["TAPO_HOST"] = login.Host;
        psi.Environment["TAPO_USERNAME"] = login.User; psi.Environment["TAPO_PASSWORD"] = login.Password;
        psi.Environment["TAPO_TIMEOUT"] = cfg["TapoP100:TimeoutSeconds"] ?? "10";
        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            // Drain stderr without logging arbitrary library output or credentials.
            _ = process.StandardError.ReadToEndAsync(ct);
            if (!await ReadState(process, ct)) return;
            state = "connected";
            Remember(login);
            log.LogInformation("Tapo P100 ready for {Device}", Device);
            while (!ct.IsCancellationRequested)
            {
                using var poll = CancellationTokenSource.CreateLinkedTokenSource(ct);
                poll.CancelAfter(TimeSpan.FromSeconds(15));
                bool on;
                try { on = await commands.Reader.ReadAsync(poll.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await process.StandardInput.WriteLineAsync("\"status\"");
                    if (!await ReadState(process, ct)) return;
                    continue;
                }
                await process.StandardInput.WriteLineAsync(on ? "\"on\"" : "\"off\"");
                if (!await ReadState(process, ct)) return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { error = "bridge_error"; state = "error"; log.LogError("Tapo adapter stopped ({Type}); no automatic retry", ex.GetType().Name); }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
        }
    }

    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "src/tapo_p100/bridge.py"))) return dir.FullName;
        return Directory.GetCurrentDirectory();
    }

    async Task<bool> ReadState(Process process, CancellationToken ct)
    {
        var seconds = Math.Clamp(cfg.GetValue("TapoP100:TimeoutSeconds", 10), 1, 120);
        var line = await process.StandardOutput.ReadLineAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(seconds * 3 + 5), ct);
        if (line is null) { error = "bridge_error"; state = "error"; log.LogError("Tapo bridge stopped; no automatic retry"); return false; }
        using var doc = JsonDocument.Parse(line);
        var obj = doc.RootElement;
        if (!obj.GetProperty("ok").GetBoolean())
        {
            var error = obj.TryGetProperty("error", out var e) ? e.GetString() : null;
            // Only known codes may reach logs or the UI.
            var code = error is "auth_locked" or "auth_credentials" or "auth_device_proof" or "timeout" or "session_expired" or "device_error" ? error : "bridge_error";
            this.error = code; state = "error";
            log.LogError("Tapo failed: {Code}", code);
            return false;
        }
        var on = obj.GetProperty("on").GetBoolean();
        var payload = JsonSerializer.Serialize(new { ts = DateTimeOffset.UtcNow, on });
        var topic = $"home/device/{Device}/state";
        router.Handle(topic, payload);
        bus.Publish(topic, payload);
        return true;
    }
}
