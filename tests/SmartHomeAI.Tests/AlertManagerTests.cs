using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Tests;

public class AlertManagerTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    static readonly string[] Allowed = ["light", "fan"];

    AlertManager M(bool autoOff = false) => new(_t, new AlertOptions { AutoOffEnabled = autoOff });
    static AlertInput In(OccupancyState s = OccupancyState.AwayConfirmed, bool light = true, bool fan = false)
        => new(s, light, fan, Allowed);
    IReadOnlyList<AlertAction> At(AlertManager m, TimeSpan after, AlertInput i) { _t.Advance(after); return m.Evaluate(i); }
    static void R(IReadOnlyList<AlertAction> a, string id, string devs, AlertLevel lvl)
    {
        var r = Assert.IsType<RaiseAlert>(Assert.Single(a));
        Assert.Equal((id, devs, lvl), (r.Id, string.Join(",", r.DevicesOn), r.Level));
    }
    static string Off(IReadOnlyList<AlertAction> a) => string.Join(",", a.OfType<AutoOff>().Single().DeviceIds);
    static TimeSpan Min(double m) => TimeSpan.FromMinutes(m);

    [Fact] public void SC07_FirstAfter2Min_ReminderAfter5MoreMin()
    {
        var m = M();
        Assert.Empty(m.Evaluate(In()));
        Assert.Empty(At(m, Min(1.9), In()));
        R(At(m, Min(0.1), In()), "alert-1", "light", AlertLevel.First);
        Assert.Empty(At(m, Min(4.9), In()));
        R(At(m, Min(0.1), In()), "alert-1", "light", AlertLevel.Reminder);
        R(At(m, Min(5), In()), "alert-1", "light", AlertLevel.Reminder);
    }

    [Fact] public void SC11_PersonReturns_Cancels()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In());
        Assert.Equal([new CancelAlert("alert-1", CancelReason.PersonReturned)], At(m, Min(1), In(OccupancyState.Occupied)));
        Assert.Equal(CancelReason.PersonReturned, m.History[0].Reason);
        Assert.Empty(At(m, Min(10), In(OccupancyState.Occupied)));
    }

    [Fact] public void Unknown_Cancels_DataLost()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In());
        Assert.Empty(At(m, Min(1), In(OccupancyState.Unknown))); // frozen
        Assert.Equal([new CancelAlert("alert-1", CancelReason.DataLost)], At(m, Min(1), In(OccupancyState.Unknown)));
        Assert.Equal(CancelReason.DataLost, m.History[0].Reason);
    }

    [Fact] public void F_ShortUnknown_FreezesCountersAndKeepsAlert()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In()); // alert-1 raised
        Assert.Empty(m.Evaluate(In(OccupancyState.Unknown)));
        Assert.Empty(At(m, TimeSpan.FromSeconds(59), In(OccupancyState.Unknown)));
        Assert.Empty(At(m, TimeSpan.FromSeconds(1), In())); // back after 60 s frozen: still no cancel, no new id
        Assert.Null(m.History[0].CancelledAt);
        Assert.Empty(At(m, Min(4.9), In())); // reminder clock resumed: 5 min counted without the 60 s gap
        R(At(m, Min(0.1), In()), "alert-1", "light", AlertLevel.Reminder);
    }

    [Fact] public void F_LongUnknown_CancelsDataLost()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In());
        m.Evaluate(In(OccupancyState.Unknown));
        Assert.Equal([new CancelAlert("alert-1", CancelReason.DataLost)], At(m, TimeSpan.FromSeconds(90), In(OccupancyState.Unknown)));
    }

    [Fact] public void DevicesOff_Cancels_NoRepeat()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In());
        Assert.Equal([new CancelAlert("alert-1", CancelReason.DevicesOff)], At(m, Min(1), In(light: false)));
        Assert.Empty(At(m, Min(10), In()));
    }

    [Fact] public void Unknown_NoAlert_AndCancelsActive()
    {
        var m = M();
        Assert.Empty(m.Evaluate(In(OccupancyState.Unknown)));
        Assert.Empty(At(m, Min(20), In(OccupancyState.Unknown)));
        m.Evaluate(In()); At(m, Min(2), In());
        m.Evaluate(In(OccupancyState.Unknown));
        Assert.IsType<CancelAlert>(Assert.Single(At(m, Min(1), In(OccupancyState.Unknown))));
    }

    [Fact] public void NoDevicesOn_NoAlert() { var m = M(); m.Evaluate(In(light: false)); Assert.Empty(At(m, Min(5), In(light: false))); }

    [Fact] public void NewAwayEpisode_NewId()
    {
        var m = M();
        m.Evaluate(In()); At(m, Min(2), In()); At(m, Min(1), In(OccupancyState.Occupied));
        m.Evaluate(In()); var a = At(m, Min(2), In());
        Assert.Equal("alert-2", Assert.IsType<RaiseAlert>(Assert.Single(a)).Id);
        Assert.Equal(2, m.History.Count);
    }

    [Fact] public void AutoOffDisabled_NeverTurnsOff()
    {
        var m = M(); m.Evaluate(In());
        Assert.DoesNotContain(At(m, Min(12), In()), a => a is AutoOff);
    }

    [Fact] public void AutoOffEnabled_At10Min_OnlyAllowedDevicesOn()
    {
        var m = M(true); m.Evaluate(In(fan: true));
        Assert.DoesNotContain(At(m, Min(9.9), In(fan: true)), a => a is AutoOff);
        Assert.Equal("light,fan", Off(At(m, Min(0.1), In(fan: true))));
        Assert.DoesNotContain(At(m, Min(1), In(fan: true)), a => a is AutoOff);
    }

    [Fact] public void AutoOff_RespectsAllowedList()
    {
        var m = M(true); m.Evaluate(In(fan: true));
        var a = At(m, Min(10), new AlertInput(OccupancyState.AwayConfirmed, true, true, ["fan"]));
        Assert.Equal("fan", Off(a));
    }

    [Fact] public void Feedback_RecordedInHistory()
    {
        var m = M(); m.Evaluate(In()); At(m, Min(2), In());
        Assert.Equal(Feedback.None, m.History[0].Feedback);
        Assert.True(m.OnUserFeedback("alert-1", false));
        Assert.Equal(Feedback.Wrong, m.History[0].Feedback);
        Assert.True(m.OnUserFeedback("alert-1", true));
        Assert.Equal(Feedback.Correct, m.History[0].Feedback);
        Assert.False(m.OnUserFeedback("nope", true));
    }

    [Fact] public void N04_AiFlapping_AfterAlert_OneAlertNeverCancelled()
    {
        var m = M();
        var e = new OccupancyEngine(_t);
        e.OnAiState(new(true, [new(1, "sofa")]));
        e.OnTransition(new(1, Direction.Out, "door"));
        var raised = 0; var cancelled = 0;
        void Tick(int s)
        {
            for (var i = 0; i < s; i++)
            {
                _t.Advance(TimeSpan.FromSeconds(1));
                if (e.State != OccupancyState.Unknown) e.OnAiState(new(true, []));
                var acts = m.Evaluate(In(e.State));
                raised += acts.OfType<RaiseAlert>().Count(a => a.Level == AlertLevel.First);
                cancelled += acts.OfType<CancelAlert>().Count();
            }
        }
        Tick(35 + 120);
        Assert.Equal(1, raised);
        for (var k = 0; k < 5; k++)
        {
            e.OnAiStatus(new(false)); Tick(20);
            e.OnAiStatus(new(true)); e.OnAiState(new(true, [])); Tick(5);
        }
        Assert.Equal((1, 0), (raised, cancelled));
        Assert.Equal("alert-1", Assert.Single(m.History).Id);
        Assert.Null(m.History[0].Reason);
        e.OnAiState(new(true, [new(2, "livingroom")]));
        Assert.Equal([new CancelAlert("alert-1", CancelReason.PersonReturned)], m.Evaluate(In(e.State)));
    }
}
