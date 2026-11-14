using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Core.Alerts;

/// <summary>NV06, SC07, SC11. Pure rules: one alertId per away episode, timers run from AwayConfirmed.</summary>
public class AlertManager(TimeProvider time, AlertOptions? options = null)
{
    readonly AlertOptions _o = options ?? new();
    readonly List<AlertRecord> _history = [];
    int _seq;
    DateTimeOffset? _awaySince, _lastRaised;
    string? _activeId;
    bool _done, _autoOffDone; // _done: alert was cancelled by devices off, no re-raise this episode

    public IReadOnlyList<AlertRecord> History => _history;

    public IReadOnlyList<AlertAction> Evaluate(AlertInput i)
    {
        var acts = new List<AlertAction>();
        var now = time.GetUtcNow();
        if (i.State != OccupancyState.AwayConfirmed)
        {
            // Unknown = no data, so no alert (DataLost); any other state means the person is back.
            Cancel(acts, now, i.State == OccupancyState.Unknown ? CancelReason.DataLost : CancelReason.PersonReturned);
            _awaySince = _lastRaised = null; _done = _autoOffDone = false;
            return acts;
        }
        _awaySince ??= now;
        List<string> on = [];
        if (i.LightOn) on.Add("light");
        if (i.FanOn) on.Add("fan");
        if (on.Count == 0) { Cancel(acts, now, CancelReason.DevicesOff); return acts; }
        if (_done) return acts;

        var elapsed = now - _awaySince.Value;
        if (_activeId is null && elapsed >= _o.FirstAfter)
        {
            _activeId = $"alert-{++_seq}";
            _lastRaised = now;
            _history.Add(new(_activeId, now, null, null, Feedback.None));
            acts.Add(new RaiseAlert(_activeId, on, AlertLevel.First));
        }
        else if (_activeId is not null && now - _lastRaised >= _o.RemindEvery)
        {
            _lastRaised = now;
            acts.Add(new RaiseAlert(_activeId, on, AlertLevel.Reminder));
        }
        if (_o.AutoOffEnabled && !_autoOffDone && elapsed >= _o.AutoOffAfter)
        {
            var off = on.Where(i.AutoOffAllowed.Contains).ToList();
            if (off.Count > 0) { _autoOffDone = true; acts.Add(new AutoOff(off)); }
        }
        return acts;
    }

    /// <summary>Returns false if the alertId is unknown.</summary>
    public bool OnUserFeedback(string alertId, bool correct)
    {
        var n = _history.FindIndex(r => r.Id == alertId);
        if (n < 0) return false;
        _history[n] = _history[n] with { Feedback = correct ? Feedback.Correct : Feedback.Wrong };
        return true;
    }

    void Cancel(List<AlertAction> acts, DateTimeOffset now, CancelReason reason)
    {
        if (_activeId is null) return;
        var n = _history.FindIndex(r => r.Id == _activeId);
        _history[n] = _history[n] with { CancelledAt = now, Reason = reason };
        acts.Add(new CancelAlert(_activeId, reason));
        _activeId = null;
        _done = true;
    }
}
