using System.Diagnostics;

namespace SmartHomeAI.Dashboard.Services;

public enum HubStatus { Checking, Running, Started, Failed }

/// <summary>Khi Dashboard lên: bật Mosquitto (nếu cần) và Hub (nếu /health không đáp). Chỉ dừng Hub do chính nó tạo.</summary>
public class HubLauncher(IConfiguration cfg, ILogger<HubLauncher> log) : IHostedService
{
    Process? _child;
    public HubStatus Status { get; private set; } = HubStatus.Checking;
    public string Detail { get; private set; } = "";

    public Task StartAsync(CancellationToken ct) { _ = Task.Run(Boot); return Task.CompletedTask; }

    public Task StopAsync(CancellationToken ct)
    {
        if (_child is { } p) { log.LogInformation("Dừng Hub do Dashboard khởi động"); Repo.Stop(p, 3000); }
        return Task.CompletedTask;
    }

    async Task Boot()
    {
        try
        {
            var root = Repo.Root(cfg);
            var url = cfg["Hub:BaseUrl"] ?? "http://localhost:5002";
            if (cfg.GetValue("Startup:StartMosquitto", true) && !Repo.PortOpen("localhost", 1883)) await Mosquitto(root);
            if (await Healthy(url)) { Status = HubStatus.Running; return; }

            var psi = Repo.Psi(cfg["Hub:Command"] ?? "dotnet", cfg["Hub:Args"] ?? "run --project src/SmartHomeAI.Hub --no-launch-profile", root, false);
            psi.Environment["ASPNETCORE_URLS"] = url;
            if (!cfg.GetValue("Hub:UsePostgres", false)) psi.Environment["ConnectionStrings__Postgres"] = "";
            _child = Process.Start(psi);
            log.LogInformation("Đã khởi động Hub (pid {Pid})", _child?.Id);
            for (var i = 0; i < 90 && _child is { HasExited: false }; i++)
            {
                await Task.Delay(1000);
                if (await Healthy(url)) { Status = HubStatus.Started; return; }
            }
            Fail(_child is { HasExited: true } ? $"Hub thoát mã {_child.ExitCode}" : "Hub không đáp /health sau 90 giây");
        }
        catch (Exception e) { Fail(e.Message); }
    }

    void Fail(string why) { Status = HubStatus.Failed; Detail = why; log.LogWarning("Hub không chạy được: {Why}", why); }

    async Task Mosquitto(string root)
    {
        try
        {
            log.LogInformation("Cổng 1883 chưa mở, chạy docker compose up -d mosquitto");
            using var p = Process.Start(Repo.Psi("docker", "compose up -d mosquitto", root, false));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await p!.WaitForExitAsync(cts.Token);
            if (p.ExitCode != 0) log.LogWarning("docker compose thoát mã {Code}", p.ExitCode);
        }
        catch (Exception e) { log.LogWarning("Không bật được Mosquitto: {Msg}", e.Message); }
    }

    static async Task<bool> Healthy(string url)
    {
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            return (await h.GetAsync(url.TrimEnd('/') + "/health")).IsSuccessStatusCode;
        }
        catch (Exception) { return false; }
    }
}
