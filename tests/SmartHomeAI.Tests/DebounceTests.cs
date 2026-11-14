using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Tests;

/// <summary>Anti-flapping fixes: A manual detection, B retry limit, D lying time, E hourly cap, start-up grace.</summary>
public class DebounceTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    static readonly AutomationOptions NoGrace = new() { StartupGrace = TimeSpan.Zero };
    AutomationEngine E(AutomationOptions? o = null) => new(_t, o ?? NoGrace);
    static AutomationInput Sofa(bool fan = false, TimeSpan? lying = null) =>
        new(OccupancyState.Occupied, [new Person(1, "sofa", "lying")], 100, 30, false, fan, lying);
    static TimeSpan S(double s) => TimeSpan.FromSeconds(s);
    DiagnosisCode Fan(AutomationEngine e, AutomationInput i) => e.Diagnose(i).Single(d => d.DeviceId == "fan").Code;

    [Fact] public void A_ButtonOff_BlocksAuto30Min()
    {
        var e = E();
        Assert.False(e.OnDeviceState("fan", true)); // first report after start-up is not a change
        _t.Advance(S(60));
        Assert.True(e.OnDeviceState("fan", false));
        _t.Advance(TimeSpan.FromMinutes(29));
        Assert.Empty(e.Evaluate(Sofa()));
        _t.Advance(TimeSpan.FromMinutes(1.1));
        Assert.Single(e.Evaluate(Sofa()));
    }

    [Fact] public void A_StateEchoOfHubCommand_IsNotManual()
    {
        var e = E();
        e.OnDeviceState("fan", false);
        Assert.Single(e.Evaluate(Sofa())); // auto on
        _t.Advance(S(2));
        Assert.False(e.OnDeviceState("fan", true));
        e.OnHubCommand("fan", false); // e.g. alert auto-off
        Assert.False(e.OnDeviceState("fan", false));
        _t.Advance(S(61));
        Assert.Single(e.Evaluate(Sofa())); // no manual block
    }

    [Fact] public void A_ChangeAfterWindow_IsManual()
    {
        var e = E();
        e.OnDeviceState("fan", false);
        e.Evaluate(Sofa());
        _t.Advance(S(11));
        Assert.True(e.OnDeviceState("fan", true));
    }

    [Fact] public void B_MuteDevice_MaxThreeCommandsThenUnresponsive_AnyStateResumes()
    {
        var e = E();
        Assert.Single(e.Evaluate(Sofa()));
        _t.Advance(S(59)); Assert.Empty(e.Evaluate(Sofa()));
        _t.Advance(S(1)); Assert.Single(e.Evaluate(Sofa())); // 2nd after 60 s
        _t.Advance(S(119)); Assert.Empty(e.Evaluate(Sofa()));
        _t.Advance(S(1)); Assert.Single(e.Evaluate(Sofa())); // 3rd after 120 s
        _t.Advance(S(119)); Assert.Equal(DiagnosisCode.Cooldown, Fan(e, Sofa()));
        _t.Advance(S(1)); Assert.Equal(DiagnosisCode.DeviceUnresponsive, Fan(e, Sofa()));
        _t.Advance(TimeSpan.FromHours(2)); Assert.Empty(e.Evaluate(Sofa()));
        e.OnDeviceState("fan", false); // any state resumes
        Assert.Single(e.Evaluate(Sofa()));
    }

    [Fact] public void B_StateOn_ClearsPending()
    {
        var e = E();
        e.Evaluate(Sofa());
        _t.Advance(S(60)); e.Evaluate(Sofa());
        _t.Advance(S(120)); e.Evaluate(Sofa()); // 3 sent
        e.OnDeviceState("fan", true); // reported on: pending gone
        _t.Advance(S(200));
        Assert.Equal(DiagnosisCode.WillTurnOn, Fan(e, Sofa())); // not DeviceUnresponsive
    }

    [Fact] public void D_LyingShortWaits_Enough_TurnsOn()
    {
        var e = E();
        var d = e.Diagnose(Sofa(lying: S(2))).Single(x => x.DeviceId == "fan");
        Assert.Equal(DiagnosisCode.LyingNotLongEnough, d.Code);
        Assert.Equal((2.0, 5.0), ((double)d.Params["seconds"]!, (double)d.Params["need"]!));
        Assert.Empty(e.Evaluate(Sofa(lying: S(2))));
        Assert.Single(e.Evaluate(Sofa(lying: S(5))));
    }

    [Fact] public void E_SeventhAutoChange_Locked_ManualUnlocks()
    {
        var e = E();
        for (var n = 0; n < 6; n++)
        {
            Assert.Single(e.Evaluate(Sofa()));
            e.OnDeviceState("fan", true);
            _t.Advance(S(61));
        }
        Assert.Empty(e.Evaluate(Sofa()));
        Assert.Equal(DiagnosisCode.AutoLocked, Fan(e, Sofa()));
        _t.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty(e.Evaluate(Sofa())); // still locked
        e.OnManualCommand("fan", false);
        _t.Advance(TimeSpan.FromMinutes(31)); // manual block over, 1 h lock would still run
        Assert.Single(e.Evaluate(Sofa()));
    }

    [Fact] public void StartupGrace_30s()
    {
        var e = E(new());
        Assert.Equal(DiagnosisCode.StartingUp, Fan(e, Sofa()));
        Assert.Empty(e.Evaluate(Sofa(lying: S(30))));
        _t.Advance(S(30));
        Assert.Single(e.Evaluate(Sofa(lying: S(30))));
    }

    [Fact] public void RestoreManual_OnlyWithinBlock()
    {
        var e = E();
        e.RestoreManual("fan", _t.GetUtcNow() - TimeSpan.FromMinutes(10));
        e.RestoreManual("light", _t.GetUtcNow() - TimeSpan.FromMinutes(40));
        Assert.Equal(["fan"], e.ManualTimes.Keys);
    }
}
