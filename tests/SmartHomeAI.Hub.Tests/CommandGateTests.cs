using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub.Tests;

public class CommandGateTests
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void Twenty_manual_commands_in_5s_send_at_most_two()
    {
        var bus = new FakeBus();
        var state = new HubState(new OccupancyEngine(_t), new AutomationEngine(_t, TestOpts.NoWait), new AlertManager(_t), clock: _t);
        var r = new MessageRouter(state, new RecLog(), _t, NullLogger<MessageRouter>.Instance, bus);
        var held = 0;
        for (var n = 0; n < 20; n++)
        {
            r.TryPublishManual("fan", n % 2 == 0, out var c); if (c) held++;
            _t.Advance(S(0.25)); r.Tick();
        }
        Assert.Equal(2, bus.Sent.Count); // first, then the merged last one after 3 s
        Assert.Equal(19, held);
        Assert.Contains(state.Decisions.Latest(50), e => e.Message.StartsWith("Quạt trần: gộp ") && e.Message.EndsWith("chỉ gửi lệnh cuối"));
    }

    [Fact]
    public void Auto_two_changes_in_8s_second_held_to_10s_alert_exempt()
    {
        var g = new CommandGate(_t, new AutomationOptions());
        Assert.Equal(GateOutcome.Send, g.Offer("fan", true, "auto", null));
        _t.Advance(S(8));
        Assert.Equal(GateOutcome.Coalesced, g.Offer("fan", false, "auto", null));
        Assert.Empty(g.Due(_ => null));
        _t.Advance(S(1.9));
        Assert.Empty(g.Due(_ => null));
        _t.Advance(S(0.1));
        Assert.Single(g.Due(_ => null));
        // alert (and manual) are not held by MinHold, only by the 3 s merge
        _t.Advance(S(3));
        Assert.Equal(GateOutcome.Send, g.Offer("fan", true, "alert", null));
    }

    [Fact]
    public void Duplicate_state_not_sent_but_retry_of_unreported_is()
    {
        var g = new CommandGate(_t, new AutomationOptions());
        Assert.Equal(GateOutcome.Duplicate, g.Offer("fan", true, "manual", true));
        Assert.Equal(GateOutcome.Send, g.Offer("fan", true, "auto", false));
        _t.Advance(S(60));
        Assert.Equal(GateOutcome.Send, g.Offer("fan", true, "auto", false)); // retry to a mute device
    }

    [Fact]
    public void Merged_command_equal_to_reported_state_is_dropped()
    {
        var g = new CommandGate(_t, new AutomationOptions());
        g.Offer("fan", true, "manual", false);
        g.Offer("fan", false, "manual", false);
        g.Offer("fan", true, "manual", false); // held; the device reports on meanwhile
        _t.Advance(S(3));
        var d = Assert.Single(g.Due(_ => true));
        Assert.True(d.Duplicate);
    }
}
