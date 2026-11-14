using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub.Tests;

public class DebounceWiringTests : IDisposable
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    readonly FakeBus _bus = new();
    readonly string _path = Path.Combine(Path.GetTempPath(), $"manual-{Guid.NewGuid():N}.json");
    HubState _state = null!;
    MessageRouter _r = null!;

    public void Dispose() { try { File.Delete(_path); } catch { } }

    void Start(AutomationOptions o, bool store = false)
    {
        _state = new(new OccupancyEngine(_t), new AutomationEngine(_t, o), new AlertManager(_t), null, new DecisionLog(_t), _t, store ? new ManualStore(_path) : null);
        _r = new(_state, new RecLog(), _t, NullLogger<MessageRouter>.Instance, _bus);
        _r.Handle("home/ai/status", "{" + Ts + ",\"online\":true}");
        _r.Handle("home/device/light/state", "{" + Ts + ",\"on\":false}");
        _r.Handle("home/device/fan/state", "{" + Ts + ",\"on\":false}");
    }

    void Env(double temp, double lux) => _r.Handle("home/sensor/esp1/env", $"{{{Ts},\"temp_c\":{temp},\"humidity\":50,\"lux\":{lux}}}");
    void Person(string zone, string posture) => _r.Handle("home/ai/livingroom/state",
        $"{{{Ts},\"seq\":1,\"camera_ok\":true,\"persons\":[{{\"track_id\":1,\"zone\":\"{zone}\",\"posture\":\"{posture}\",\"conf\":0.9}}]}}");
    int Sets(string dev) => _bus.Sent.Count(s => s.Topic == $"home/device/{dev}/set");
    List<string> Msgs() => [.. _state.Decisions.Latest(500).Select(e => e.Message)];
    void Run(TimeSpan total, string zone, string posture)
    {
        for (var e = TimeSpan.Zero; e < total; e += TimeSpan.FromSeconds(5)) { Person(zone, posture); _t.Advance(TimeSpan.FromSeconds(5)); }
        Person(zone, posture);
    }

    [Fact]
    public void A_ExternalOff_Logged_BlocksAuto()
    {
        Start(TestOpts.NoWait);
        Env(30, 100);
        _r.Handle("home/device/fan/state", "{" + Ts + ",\"on\":true}");
        _r.Handle("home/device/fan/state", "{" + Ts + ",\"on\":false}");
        Assert.Contains("Quạt trần bị tắt ngoài hệ thống (nút/ứng dụng): tự động tạm dừng", Msgs());
        Run(TimeSpan.FromMinutes(29), "sofa", "lying");
        Assert.Equal(0, Sets("fan"));
    }

    [Fact]
    public void A_HubAutoEcho_NotManual()
    {
        Start(TestOpts.NoWait);
        Env(25, 5);
        Person("livingroom", "standing");
        Assert.Equal(1, Sets("light"));
        _r.Handle("home/device/light/state", "{" + Ts + ",\"on\":true}");
        Assert.DoesNotContain(Msgs(), m => m.Contains("ngoài hệ thống"));
    }

    [Fact]
    public void B_MuteDevice_ThreeCommandsThenStops()
    {
        Start(TestOpts.NoWait);
        Env(25, 5);
        Run(TimeSpan.FromMinutes(10), "livingroom", "standing");
        Assert.Equal(3, Sets("light"));
        Assert.Contains("Đèn không phản hồi sau 3 lần: dừng gửi lệnh tự động cho tới khi thiết bị báo trạng thái", Msgs());
        _r.Handle("home/device/light/state", "{" + Ts + ",\"on\":true}"); // any state, here on: resumes nothing to send
        Run(TimeSpan.FromMinutes(2), "livingroom", "standing");
        Assert.Equal(3, Sets("light"));
    }

    [Fact]
    public void C_Restart_KeepsManual_AndWaits30s()
    {
        Start(TestOpts.NoWait, store: true);
        _r.Handle("home/device/light/set", "{" + Ts + ",\"on\":false,\"source\":\"manual\"}");
        Assert.True(File.Exists(_path));
        _t.Advance(TimeSpan.FromMinutes(5));
        _bus.Sent.Clear();
        Start(new AutomationOptions { SofaLyingMin = TimeSpan.Zero }, store: true); // restart: default 30 s start-up grace
        Env(25, 5);
        Person("livingroom", "standing");
        Assert.Empty(_bus.Sent);
        Assert.Contains("Đang chờ thiết bị báo trạng thái sau khi khởi động", Msgs());
        Run(TimeSpan.FromSeconds(31), "livingroom", "standing");
        Assert.Equal(0, Sets("light")); // grace over, but the manual pause survived the restart
        Run(TimeSpan.FromMinutes(25), "livingroom", "standing");
        Assert.Equal(1, Sets("light")); // pause (30 min from the manual command) ended
    }

    [Fact]
    public void D_Lying2sWaits_5sTurnsOn()
    {
        Start(new AutomationOptions { StartupGrace = TimeSpan.Zero });
        Env(30, 100);
        Person("sofa", "lying");
        _t.Advance(TimeSpan.FromSeconds(2));
        Person("sofa", "lying");
        Assert.Equal(0, Sets("fan"));
        Assert.Contains("Quạt trần chưa bật: mới nằm ở sofa 0 s, chờ đủ 5 s", Msgs()); // written once, when the reason first appears
        _t.Advance(TimeSpan.FromSeconds(2));
        Person("sofa", "lying");
        _t.Advance(TimeSpan.FromSeconds(2));
        Person("sofa", "lying");
        Assert.Equal(1, Sets("fan"));
    }

    void Dev(string id, bool on) => _r.Handle($"home/device/{id}/state", "{" + Ts + $",\"on\":{(on ? "true" : "false")}}}");
    void DevStatus(bool online) => _r.Handle("home/device/status", "{" + Ts + $",\"online\":{(online ? "true" : "false")}}}");

    [Fact]
    public void Sync_OffWithin2sOfOnline_NotManual_AutoStillTurnsOn()
    {
        Start(TestOpts.NoWait);
        DevStatus(true);
        Dev("light", true); Dev("fan", true); // known on (sync, not manual)
        _t.Advance(TimeSpan.FromSeconds(2));
        Dev("light", false); Dev("fan", false);
        Assert.DoesNotContain(Msgs(), m => m.Contains("ngoài hệ thống"));
        Assert.Single(Msgs(), m => m.Contains("Ổ cắm kết nối lại"));
        Env(30, 5);
        Person("sofa", "lying");
        Assert.Equal(1, Sets("light"));
        Assert.Equal(1, Sets("fan"));
    }

    [Fact]
    public void Sync_OffAfterWindow_StillManual_BlocksAuto()
    {
        Start(TestOpts.NoWait);
        Dev("fan", true);
        DevStatus(true);
        _t.Advance(TimeSpan.FromSeconds(10));
        Dev("fan", false);
        Assert.Contains(Msgs(), m => m.Contains("Quạt trần bị tắt ngoài hệ thống"));
        Env(30, 100);
        Person("sofa", "lying");
        Assert.Equal(0, Sets("fan"));
    }

    [Fact]
    public void Sync_FirstStateEver_NotManual()
    {
        Start(TestOpts.NoWait);
        Dev("fan", true); // first state seen... here already known from Start (off); new hub below
        var s = new HubState(new OccupancyEngine(_t), new AutomationEngine(_t, TestOpts.NoWait), new AlertManager(_t), null, new DecisionLog(_t), _t);
        s.Apply(new DeviceStateMsg(_t.GetUtcNow(), "light", false));
        Assert.DoesNotContain(s.Decisions.Latest(50), e => e.Message.Contains("ngoài hệ thống"));
    }

    [Fact]
    public void Sync_OfflineThenOnlineAgain_LogsEachReconnect()
    {
        Start(TestOpts.NoWait);
        DevStatus(true); DevStatus(true); // repeated online is not a reconnect
        DevStatus(false); DevStatus(true);
        Assert.Equal(2, Msgs().Count(m => m.Contains("Ổ cắm kết nối lại")));
        Dev("fan", true); Dev("fan", false);
        Assert.DoesNotContain(Msgs(), m => m.Contains("ngoài hệ thống"));
    }
}
