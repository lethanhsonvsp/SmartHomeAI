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

    public OccupancyState State => Evaluate();

    public void OnAiStatus(AiStatus s) { _aiOnline = s.Online; if (!s.Online) _emptySince = null; }

    public void OnAiState(AiState s)
    {
        var now = time.GetUtcNow();
        if (_lastState is null || now - _lastState > _o.StateStale) _emptySince = null; // gap: absence unproven
        _lastState = now;
        _cameraOk = s.CameraOk;
        _persons = s.Persons;
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
        if (t.Direction == Direction.Out) { _evidenceAt = time.GetUtcNow(); _exitTrack = t.TrackId; _exitSeenGone = false; }
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
        if (_persons.Any(p => _o.RoomZones.Contains(p.Zone))) return OccupancyState.Occupied;
        if (_persons.Count > 0 || _evidenceAt is null) return OccupancyState.OtherArea;
        var start = _emptySince > _evidenceAt ? _emptySince : _evidenceAt;
        return now - start >= _o.VerifyAway ? OccupancyState.AwayConfirmed : OccupancyState.ExitPending;
    }
}
