namespace SmartHomeAI.Core.Occupancy;

/// <summary>Pure state machine. Time comes from TimeProvider; State is recomputed on every read.</summary>
public class OccupancyEngine(TimeProvider time, OccupancyOptions? options = null)
{
    readonly OccupancyOptions _o = options ?? new();
    bool _aiOnline = true, _cameraOk;
    DateTimeOffset? _lastState, _emptySince, _evidenceAt;
    bool _exitSeenGone; // the leaver has been seen out of the room zones since the evidence
    int? _exitTrack; // track that left; null = evidence came from the door sensor
    IReadOnlyList<Person> _persons = [];
    bool _inRoomLatch; // last frame with anyone showed a person in a room zone (not yet seen elsewhere)
    DateTimeOffset? _lastAway; // last read that returned AwayConfirmed; lets a short data loss resume it

    public OccupancyState State => Evaluate();

    public void OnAiStatus(AiStatus s) { _aiOnline = s.Online; if (!s.Online) _emptySince = null; }

    public void OnAiState(AiState s)
    {
        var now = time.GetUtcNow();
        if (_lastState is null || now - _lastState > _o.StateStale) _emptySince = null; // gap: absence unproven
        _lastState = now;
        _cameraOk = s.CameraOk;
        _persons = s.Persons;
        if (_persons.Any(p => _o.RoomZones.Contains(p.Zone))) _inRoomLatch = true;
        else if (_persons.Count > 0) _inRoomLatch = false;
        if (!s.CameraOk) _emptySince = null;
        else if (_persons.Count == 0) _emptySince ??= now;
        else
        {
            _emptySince = null;
            // someone other than the leaver is still in frame (SC08): the exit evidence is void
            if (_evidenceAt is not null && _persons.Any(p => p.TrackId != _exitTrack)) _evidenceAt = null;
        }
        if (_evidenceAt is not null && _exitTrack is int id)
        {
            var inRoom = _persons.Any(p => p.TrackId == id && _o.RoomZones.Contains(p.Zone));
            if (!inRoom) _exitSeenGone = true;
            else if (_exitSeenGone) _evidenceAt = null; // came back (SC06)
        }
    }

    public void OnTransition(AiTransition t)
    {
        // only leaving via the main door is exit evidence; other_exit is still inside the house (SC05)
        if (t.Direction == Direction.Out && t.ToZone == "door") { _evidenceAt = time.GetUtcNow(); _exitTrack = t.TrackId; _exitSeenGone = false; }
        else if (t.Direction == Direction.In) _evidenceAt = null;
    }

    public void OnDoor(DoorEvent d)
    {
        if (!d.Open) return;
        _evidenceAt = time.GetUtcNow();
        _exitTrack = null;
    }

    OccupancyState Evaluate()
    {
        var now = time.GetUtcNow();
        if (!_aiOnline || !_cameraOk || _lastState is null || now - _lastState > _o.StateStale)
        {
            _emptySince = null; // SC10: never infer away from missing data; restart verification
            return OccupancyState.Unknown;
        }
        if (_persons.Any(p => _o.RoomZones.Contains(p.Zone))) { _lastAway = null; return OccupancyState.Occupied; }
        // N03: a 1-2 frame dropout while seated is not leaving. EmptyGrace and VerifyAway overlap (both count
        // from the first empty frame); the state only changes once the grace has passed. Exit evidence skips grace.
        if (_persons.Count == 0 && _inRoomLatch && _evidenceAt is null && _emptySince is { } es && now - es < _o.EmptyGrace)
            return OccupancyState.Occupied;
        if (_persons.Count > 0 || _evidenceAt is null) { _lastAway = null; return OccupancyState.OtherArea; }
        var start = _emptySince > _evidenceAt ? _emptySince : _evidenceAt;
        if (now - start >= _o.VerifyAway) { _lastAway = now; return OccupancyState.AwayConfirmed; }
        // N04: data came back after a short loss, still nobody and evidence kept: stay AwayConfirmed, not ExitPending
        if (_lastAway is { } la && now - la <= _o.AwayResume) { _lastAway = now; return OccupancyState.AwayConfirmed; }
        _lastAway = null;
        return OccupancyState.ExitPending;
    }
}
