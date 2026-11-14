using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Tests;

public class OccupancyEngineTests
{
    readonly FakeTimeProvider _t = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    readonly OccupancyEngine _e;
    public OccupancyEngineTests() => _e = new(_t);

    static Person P(int id, string zone) => new(id, zone);
    void State(bool ok = true, params Person[] ps) => _e.OnAiState(new AiState(ok, ps));
    void Wait(int s) => _t.Advance(TimeSpan.FromSeconds(s));
    // feed an AiState every second so it never goes stale
    void Hold(int s, params Person[] ps) { for (var i = 0; i < s; i++) { Wait(1); State(true, ps); } }

    [Fact]
    public void SC05_MoveToOtherArea_NotAway()
    {
        State(true, P(1, "sofa"));
        Assert.Equal(OccupancyState.Occupied, _e.State);
        State(true);
        Hold(60);
        Assert.Equal(OccupancyState.OtherArea, _e.State);
    }

    [Fact]
    public void SC06_ExitThenReturn_Cancels()
    {
        State(true, P(1, "livingroom"));
        _e.OnTransition(new(1, Direction.Out));
        State(true);
        Hold(10);
        Assert.Equal(OccupancyState.ExitPending, _e.State);
        State(true, P(1, "livingroom"));
        Assert.Equal(OccupancyState.Occupied, _e.State);
        State(true);
        Hold(40);
        Assert.Equal(OccupancyState.OtherArea, _e.State);
    }

    [Fact]
    public void SC07_Exit_AwayConfirmedAfter30s()
    {
        State(true, P(1, "livingroom"));
        _e.OnTransition(new(1, Direction.Out));
        _e.OnDoor(new(true));
        State(true);
        Hold(29);
        Assert.Equal(OccupancyState.ExitPending, _e.State);
        Hold(1);
        Assert.Equal(OccupancyState.AwayConfirmed, _e.State);
    }

    [Fact]
    public void SC08_TwoPeople_OneLeaves_NeverPendingOrAway()
    {
        State(true, P(1, "livingroom"), P(2, "sofa"));
        _e.OnTransition(new(1, Direction.Out));
        _e.OnDoor(new(true));
        Hold(60, P(2, "sofa"));
        Assert.Equal(OccupancyState.Occupied, _e.State);
        State(true, P(2, "other_exit")); // the remaining one wanders off, still in frame
        Hold(60, P(2, "other_exit"));
        Assert.Equal(OccupancyState.OtherArea, _e.State);
    }

    [Fact]
    public void SC10_CameraLost_UnknownNeverAway()
    {
        State(true, P(1, "livingroom"));
        _e.OnTransition(new(1, Direction.Out));
        State(true);
        Hold(20);
        State(false);
        Assert.Equal(OccupancyState.Unknown, _e.State);
        Wait(60);
        Assert.Equal(OccupancyState.Unknown, _e.State); // stale too
        State(true);
        Assert.Equal(OccupancyState.ExitPending, _e.State); // recomputed, verification restarts
        _e.OnAiStatus(new(false));
        Assert.Equal(OccupancyState.Unknown, _e.State);
    }

    [Fact]
    public void SC11_PersonReturnsFromAway_Occupied()
    {
        State(true, P(1, "livingroom"));
        _e.OnTransition(new(1, Direction.Out));
        State(true);
        Hold(30);
        Assert.Equal(OccupancyState.AwayConfirmed, _e.State);
        _e.OnTransition(new(1, Direction.In));
        State(true, P(1, "door"));
        State(true, P(1, "livingroom"));
        Assert.Equal(OccupancyState.Occupied, _e.State);
    }
}
