using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub.Tests;

public class DecisionLogTests
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    readonly FakeBus _bus = new();
    readonly HubState _state;
    readonly MessageRouter _r;

    public DecisionLogTests()
    {
        _state = new(new OccupancyEngine(_t), new AutomationEngine(_t, TestOpts.NoWait), new AlertManager(_t), null, new DecisionLog(_t));
        _r = new(_state, new RecLog(), _t, NullLogger<MessageRouter>.Instance, _bus);
    }

    List<string> Msgs(string? kind = null) => [.. _state.Decisions.Latest(500).Where(e => kind is null || e.Kind == kind).Select(e => e.Message)];
    void Env(double t, double lux) => _r.Handle("home/sensor/esp1/env", $"{{{Ts},\"temp_c\":{t.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"humidity\":50,\"lux\":{lux}}}");
    void Person(string zone, string posture) => _r.Handle("home/ai/livingroom/state",
        $"{{{Ts},\"seq\":1,\"camera_ok\":true,\"persons\":[{{\"track_id\":1,\"zone\":\"{zone}\",\"posture\":\"{posture}\",\"conf\":0.9}}]}}");

    [Fact]
    public void Light_auto_message_and_occupancy()
    {
        Env(25, 5);
        Person("livingroom", "standing");
        Assert.Contains("Bật đèn: phòng tối (5 lux < 50)", Msgs("auto"));
        Assert.Contains("Hiện diện: Chưa rõ (mất dữ liệu) → Có người", Msgs("occupancy"));
    }

    [Fact]
    public void Fan_waits_with_reason_once_then_turns_on_with_comma()
    {
        Env(24, 100);
        Person("livingroom", "standing");
        Person("livingroom", "standing");
        Person("livingroom", "standing");
        Assert.Single(Msgs("wait"), m => m.Contains("người chưa ở vùng sofa (đang ở: phòng khách, tư thế: đứng)"));
        Person("sofa", "lying");
        Assert.Contains("Quạt trần chưa bật: nhiệt độ 24,0 °C chưa tới 28 °C", Msgs("wait"));
        Env(36.5, 100);
        Assert.Contains("Bật quạt trần: có người nằm ở sofa và nóng (36,5 °C ≥ 28 °C)", Msgs("auto"));
    }

    [Fact]
    public void Manual_off_blocks_with_minutes_and_logs_manual_and_device()
    {
        Env(36, 100);
        Person("sofa", "lying");
        _r.Handle("home/device/fan/set", $"{{{Ts},\"on\":false,\"source\":\"manual\"}}");
        _r.Handle("home/device/fan/state", $"{{{Ts},\"on\":false}}");
        Assert.Contains("Bạn tắt quạt trần bằng tay, tự động tạm dừng với thiết bị này", Msgs("manual"));
        Assert.Contains("Quạt trần đã tắt", Msgs("device"));
        Assert.Contains("Quạt trần chưa bật: bạn vừa tắt tay, tự động tạm dừng còn 30 phút", Msgs("wait"));
    }

    [Fact]
    public void Alert_raise_and_cancel_logged()
    {
        var log = new DecisionLog(_t);
        log.Add("alert", "x");
        Assert.Single(log.Latest(10));
    }

    [Fact]
    public void Capacity_limited_newest_first()
    {
        var log = new DecisionLog(_t, 3);
        for (var i = 0; i < 5; i++) log.Add("auto", $"m{i}");
        Assert.Equal(["m4", "m3", "m2"], log.Latest(10).Select(e => e.Message));
        Assert.Equal(["m4"], log.Latest(1).Select(e => e.Message));
    }

    [Theory]
    [InlineData(null, 50)] [InlineData("200", 200)] [InlineData("1", 1)]
    [InlineData("0", null)] [InlineData("201", null)] [InlineData("abc", null)] [InlineData("-5", null)]
    public void ParseLimit(string? raw, int? expected) => Assert.Equal(expected, DecisionLog.ParseLimit(raw));

    [Fact]
    public void Json_shape_camel_case()
    {
        var e = new DecisionEvent(_t.GetUtcNow(), "auto", "fan", true, "m");
        var json = JsonSerializer.Serialize(new { ts = e.Ts, kind = e.Kind, device = e.Device, on = e.On, message = e.Message });
        Assert.Contains("\"kind\":\"auto\"", json);
    }
}
