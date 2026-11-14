using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public record DecisionEvent(DateTimeOffset Ts, string Kind, string? Device, bool? On, string Message);

/// <summary>In-memory ring buffer feeding GET /api/events. Own lock; callers may hold HubState's.</summary>
public class DecisionLog(TimeProvider time, int capacity = 500)
{
    public const int DefaultLimit = 50, MaxLimit = 200;
    readonly object _gate = new();
    readonly Queue<DecisionEvent> _q = [];
    readonly Dictionary<string, DiagnosisCode> _lastCode = [];

    public void Add(string kind, string message, string? device = null, bool? on = null)
    {
        lock (_gate)
        {
            _q.Enqueue(new(time.GetUtcNow(), kind, device, on, message));
            while (_q.Count > capacity) _q.Dequeue();
        }
    }

    /// <summary>Records auto/wait events for one automation pass; a wait is written only when the reason for that device changed.</summary>
    public void Decide(IEnumerable<Diagnosis> diags)
    {
        foreach (var d in diags)
        {
            DiagnosisCode? prev;
            lock (_gate) { prev = _lastCode.TryGetValue(d.DeviceId, out var p) ? p : null; _lastCode[d.DeviceId] = d.Code; }
            if (prev == d.Code) continue;
            if (d.Code == DiagnosisCode.WillTurnOn) Add("auto", DecisionText.TurnOn(d), d.DeviceId, true);
            else if (d.Code == DiagnosisCode.AlreadyOn || (d.Code == DiagnosisCode.Cooldown && prev == DiagnosisCode.WillTurnOn)) continue; // not news
            else Add("wait", DecisionText.Wait(d), d.DeviceId);
        }
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<DecisionEvent> Latest(int limit)
    {
        lock (_gate) return [.. _q.Reverse().Take(limit)];
    }

    public static int? ParseLimit(string? raw) =>
        raw is null ? DefaultLimit : int.TryParse(raw, out var n) && n > 0 && n <= MaxLimit ? n : null;
}
