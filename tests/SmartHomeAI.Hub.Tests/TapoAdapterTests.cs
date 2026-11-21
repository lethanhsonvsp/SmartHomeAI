using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

namespace SmartHomeAI.Hub.Tests;

public class TapoAdapterTests
{
    // Cầu nối giả: báo trạng thái một lần rồi sống tới khi bị tắt (thay cho python -m src.tapo_p100.bridge).
    static TapoP100Adapter Make(string dir)
    {
        var bridge = Path.Combine(dir, "bridge.sh");
        File.WriteAllText(bridge, "#!/bin/sh\necho '{\"ok\":true,\"on\":false}'\ncat >/dev/null\n");
        File.SetUnixFileMode(bridge, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TapoP100:Python"] = bridge, ["TapoP100:Root"] = dir, ["TapoP100:SavePath"] = Path.Combine(dir, "tapo.json"),
        }).Build();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
        var state = new HubState(new OccupancyEngine(time), new AutomationEngine(time, TestOpts.NoWait), new AlertManager(time));
        var bus = new FakeBus();
        return new TapoP100Adapter(cfg, bus, new MessageRouter(state, new RecLog(), time, NullLogger<MessageRouter>.Instance, bus), NullLogger<TapoP100Adapter>.Instance);
    }

    static async Task WaitFor(TapoP100Adapter a, string want)
    {
        for (var i = 0; i < 100 && a.Status.State != want; i++) await Task.Delay(50);
        Assert.Equal(want, a.Status.State);
    }

    [Fact]
    public async Task Login_is_remembered_across_restart_and_forgotten_on_logout()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = Directory.CreateTempSubdirectory("tapo-test").FullName;
        var save = Path.Combine(dir, "tapo.json");
        try
        {
            var first = Make(dir);
            await first.StartAsync(default);
            Assert.False(File.Exists(save));
            Assert.True(first.Login("a@b.c", "pw", "192.168.1.9"));
            await WaitFor(first, "connected");
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(save)); // có mật khẩu: chỉ chủ tệp đọc được
            await first.StopAsync(default);

            var second = Make(dir); // Hub khởi động lại: tự đăng nhập bằng thông tin đã lưu
            await second.StartAsync(default);
            await WaitFor(second, "connected");
            Assert.Equal("192.168.1.9", second.Host);
            second.Logout();
            Assert.False(File.Exists(save));
            await second.StopAsync(default);
        }
        finally { Directory.Delete(dir, true); }
    }
}
