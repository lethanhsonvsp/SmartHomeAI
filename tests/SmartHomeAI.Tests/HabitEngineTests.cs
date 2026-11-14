using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Habits;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Tests;

public class HabitEngineTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    HabitEngine H() => new(_t);

    void Fan(HabitEngine h, params double[] temps)
    {
        foreach (var x in temps)
        {
            _t.Advance(TimeSpan.FromHours(1));
            h.Record(new(_t.GetUtcNow(), "fan", true, HabitSource.Manual, TempC: x, Posture: "lying", Zone: "sofa"));
        }
    }

    static AutomationInput Sofa(double temp) =>
        new(OccupancyState.Occupied, [new Person(1, "sofa", "lying")], 100, temp, false, false);

    [Fact] public void TooFewObservations_NoRule() { var h = H(); Fan(h, 27, 27, 27, 27); Assert.Empty(h.Rules); }

    [Fact]
    public void Enough_ProposesLowerQuartile()
    {
        var h = H(); Fan(h, 26, 27, 27.5, 28, 26.5);
        var r = Assert.Single(h.Rules);
        Assert.Equal((HabitStatus.Proposed, 5, 26.5), (r.Status, r.Support, r.Threshold));
    }

    [Fact] public void WideSpread_NoRule() { var h = H(); Fan(h, 22, 25, 27, 30, 33); Assert.Empty(h.Rules); }

    [Fact]
    public void NotOnSofa_Ignored()
    {
        var h = H();
        for (var i = 0; i < 6; i++) h.Record(new(_t.GetUtcNow(), "fan", true, HabitSource.Manual, TempC: 27, Posture: "sitting", Zone: "sofa"));
        Assert.Empty(h.Rules);
    }

    [Fact]
    public void Light_LuxRule()
    {
        var h = H();
        foreach (var l in new[] { 30.0, 35, 40, 32, 38 }) h.Record(new(_t.GetUtcNow(), "light", true, HabitSource.Manual, Lux: l));
        var r = Assert.Single(h.Rules);
        Assert.Equal((HabitCondition.LuxBelow, 32.0), (r.Condition, r.Threshold));
    }

    [Fact]
    public void ApproveEditDisable()
    {
        var h = H(); Fan(h, 27, 27, 27, 27, 27);
        var id = h.Rules[0].Id;
        Assert.True(h.Approve(id)); Assert.Equal(HabitStatus.Approved, h.Rules[0].Status);
        Assert.True(h.Edit(id, 26)); Assert.Equal(26, h.Rules[0].Threshold);
        Assert.True(h.Disable(id)); Assert.Equal(HabitStatus.Disabled, h.Rules[0].Status);
        Assert.False(h.Approve("nope"));
    }

    [Fact]
    public void Advisor_OnlyApproved()
    {
        var h = H(); Fan(h, 27, 27, 27, 27, 27);
        Assert.False(h.ShouldTurnOnFan(Sofa(28)));        // Proposed
        h.Approve(h.Rules[0].Id);
        Assert.True(h.ShouldTurnOnFan(Sofa(27)));
        Assert.False(h.ShouldTurnOnFan(Sofa(26)));
        h.Disable(h.Rules[0].Id);
        Assert.False(h.ShouldTurnOnFan(Sofa(28)));
    }

    [Fact]
    public void SC04_RealAdvisor_CoolButLearned_FanOnViaEngine()
    {
        var h = H(); Fan(h, 25, 25, 25, 25, 25);
        var e = new AutomationEngine(_t, new AutomationOptions { StartupGrace = TimeSpan.Zero }, h);
        Assert.Empty(e.Evaluate(Sofa(25)));               // not approved yet
        h.Approve(h.Rules[0].Id);
        Assert.Equal([new DeviceCommand("fan", true)], e.Evaluate(Sofa(25)));
    }

    [Fact]
    public void SC12_HabitChange_ProposesNewKeepsApproved()
    {
        var h = H(); Fan(h, 28, 28, 28, 28, 28);
        var old = h.Rules[0].Id; h.Approve(old);
        Fan(h, Enumerable.Repeat(25.0, 30).ToArray());    // window now only new habit
        Assert.Equal(2, h.Rules.Count);
        var o = h.Rules.Single(r => r.Id == old); var n = h.Rules.Single(r => r.Id != old);
        Assert.Equal((HabitStatus.Approved, 28.0), (o.Status, o.Threshold));
        Assert.Equal((HabitStatus.Proposed, 25.0), (n.Status, n.Threshold));
        h.Approve(n.Id);
        Assert.Equal(HabitStatus.Disabled, h.Rules.Single(r => r.Id == old).Status);
    }
}
