using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Tests;

public class AutomationEngineTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    class Advisor(bool answer) : IHabitAdvisor { public bool ShouldTurnOnFan(AutomationInput c) => answer; }

    AutomationEngine E(bool? advisor = null) => new(_t, null, advisor is bool b ? new Advisor(b) : null);
    static AutomationInput In(OccupancyState s = OccupancyState.Occupied, string zone = "livingroom", string posture = "sitting",
        double? lux = 100, double? temp = 25, bool light = false, bool fan = false)
        => new(s, [new Person(1, zone, posture)], lux, temp, light, fan);
    static AutomationInput Sofa(double temp, bool fan = false) => In(zone: "sofa", posture: "lying", temp: temp, fan: fan);

    [Fact] public void SC01_DarkOccupied_TurnsLightOn() =>
        Assert.Equal([new DeviceCommand("light", true)], E().Evaluate(In(lux: 10)));

    [Fact] public void SC01_LightAlreadyOn_NoCommand() => Assert.Empty(E().Evaluate(In(lux: 10, light: true)));

    [Fact] public void SC02_Bright_NoCommand() => Assert.Empty(E().Evaluate(In(lux: 50)));

    [Fact] public void SC03_LyingHot_TurnsFanOn() =>
        Assert.Equal([new DeviceCommand("fan", true)], E().Evaluate(Sofa(30)));

    [Fact] public void SC03_FanAlreadyOn_NoCommand() => Assert.Empty(E().Evaluate(Sofa(30, fan: true)));

    [Fact] public void SC03_SittingNotLying_NoFan() => Assert.Empty(E().Evaluate(In(zone: "sofa", temp: 30)));

    [Theory] [InlineData(true, 1)] [InlineData(false, 0)]
    public void SC04_CoolLying_AsksAdvisor(bool answer, int count) =>
        Assert.Equal(count, E(answer).Evaluate(Sofa(24)).Count);

    [Fact] public void SC04_DefaultAdvisor_NoFan() => Assert.Empty(E().Evaluate(Sofa(24)));

    [Theory]
    [InlineData(OccupancyState.OtherArea)] [InlineData(OccupancyState.Unknown)]
    [InlineData(OccupancyState.AwayConfirmed)] [InlineData(OccupancyState.ExitPending)]
    public void SC05_NotOccupied_NoCommand(OccupancyState s) =>
        Assert.Empty(E(true).Evaluate(In(s, "sofa", "lying", lux: 0, temp: 35)));

    [Fact]
    public void SC09_Manual_BlocksThatDeviceOnly_ThenExpires()
    {
        var e = E();
        e.OnManualCommand("fan", false);
        _t.Advance(TimeSpan.FromMinutes(29));
        var dark = In(zone: "sofa", posture: "lying", lux: 10, temp: 30);
        Assert.Equal("light", Assert.Single(e.Evaluate(dark)).DeviceId); // fan blocked, light free
        _t.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("fan", Assert.Single(e.Evaluate(Sofa(30))).DeviceId);
    }

    [Fact]
    public void Debounce_NoSecondAutoCommandWithin60s()
    {
        var e = E();
        Assert.Single(e.Evaluate(In(lux: 10)));
        _t.Advance(TimeSpan.FromSeconds(30));
        Assert.Empty(e.Evaluate(In(lux: 10))); // still off (e.g. command not yet applied)
        _t.Advance(TimeSpan.FromSeconds(31));
        Assert.Single(e.Evaluate(In(lux: 10)));
    }
}

public class DiagnoseTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    class Advisor(bool a) : IHabitAdvisor { public bool ShouldTurnOnFan(AutomationInput c) => a; }
    AutomationEngine E(bool? adv = null) => new(_t, null, adv is bool b ? new Advisor(b) : null);
    static AutomationInput In(OccupancyState s = OccupancyState.Occupied, string zone = "livingroom", string posture = "standing",
        double? lux = 100, double? temp = 25, bool light = false, bool fan = false, bool noPerson = false)
        => new(s, noPerson ? [] : [new Person(1, zone, posture)], lux, temp, light, fan);
    static Diagnosis D(AutomationEngine e, AutomationInput i, string dev) => e.Diagnose(i).Single(d => d.DeviceId == dev);

    [Fact] public void WillTurnOn_light_and_fan()
    {
        var e = E();
        Assert.Equal(DiagnosisCode.WillTurnOn, D(e, In(lux: 5), "light").Code);
        Assert.Equal(DiagnosisCode.WillTurnOn, D(e, In(zone: "sofa", posture: "lying", temp: 36.5), "fan").Code);
    }

    [Fact] public void Diagnose_has_no_side_effects()
    {
        var e = E();
        e.Diagnose(In(lux: 5));
        Assert.Equal(DiagnosisCode.WillTurnOn, D(e, In(lux: 5), "light").Code);
        Assert.Single(e.Evaluate(In(lux: 5)));
    }

    [Fact] public void AlreadyOn() => Assert.Equal(DiagnosisCode.AlreadyOn, D(E(), In(lux: 5, light: true), "light").Code);

    [Fact] public void NotOccupied_carries_state()
    {
        var d = D(E(), In(OccupancyState.OtherArea), "fan");
        Assert.Equal(DiagnosisCode.NotOccupied, d.Code);
        Assert.Equal(OccupancyState.OtherArea, d.Params["state"]);
    }

    [Fact] public void LightBrightEnough() => Assert.Equal(DiagnosisCode.LightBrightEnough, D(E(), In(lux: 50), "light").Code);
    [Fact] public void NoLuxData() => Assert.Equal(DiagnosisCode.NoLuxData, D(E(), In(lux: null), "light").Code);

    [Fact] public void FanNotOnSofa_carries_zone()
    {
        var d = D(E(), In(), "fan");
        Assert.Equal(DiagnosisCode.FanNotOnSofa, d.Code);
        Assert.Equal("livingroom", d.Params["zone"]);
        Assert.Equal("standing", d.Params["posture"]);
        Assert.Null(D(E(), In(noPerson: true), "fan").Params["zone"]);
    }

    [Fact] public void FanNotLying_carries_posture()
    {
        var d = D(E(), In(zone: "sofa", posture: "sitting"), "fan");
        Assert.Equal(DiagnosisCode.FanNotLying, d.Code);
        Assert.Equal("sitting", d.Params["posture"]);
    }

    [Fact] public void FanTooCool_carries_temp_and_threshold()
    {
        var d = D(E(), In(zone: "sofa", posture: "lying", temp: 24), "fan");
        Assert.Equal(DiagnosisCode.FanTooCool, d.Code);
        Assert.Equal(24.0, d.Params["temp"]);
        Assert.Equal(28.0, d.Params["threshold"]);
        Assert.Equal(DiagnosisCode.WillTurnOn, D(E(true), In(zone: "sofa", posture: "lying", temp: 24), "fan").Code);
    }

    [Fact] public void NoTempData() =>
        Assert.Equal(DiagnosisCode.NoTempData, D(E(), In(zone: "sofa", posture: "lying", temp: null), "fan").Code);

    [Fact] public void BlockedByManual_seconds_left()
    {
        var e = E();
        e.OnManualCommand("fan", false);
        _t.Advance(TimeSpan.FromMinutes(8));
        var d = D(e, In(zone: "sofa", posture: "lying", temp: 30), "fan");
        Assert.Equal(DiagnosisCode.BlockedByManual, d.Code);
        Assert.Equal(22 * 60.0, d.Params["seconds"]);
    }

    [Fact] public void Cooldown_seconds_left()
    {
        var e = E();
        e.Evaluate(In(lux: 5));
        _t.Advance(TimeSpan.FromSeconds(20));
        var d = D(e, In(lux: 5), "light");
        Assert.Equal(DiagnosisCode.Cooldown, d.Code);
        Assert.Equal(40.0, d.Params["seconds"]);
    }
}
