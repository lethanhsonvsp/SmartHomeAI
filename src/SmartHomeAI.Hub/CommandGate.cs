using SmartHomeAI.Core.Automation;

namespace SmartHomeAI.Hub;

public enum GateOutcome { Send, Coalesced, Duplicate }

/// <summary>Short-term debounce of every command the Hub publishes to one device (relay protection). Not thread-safe: the router calls it under its own flow.</summary>
public class CommandGate(TimeProvider time, AutomationOptions o)
{
    class Dev
    {
        public DateTimeOffset? LastSent, LastAutoChange, Earliest;
        public bool? LastAutoOn;
        public (bool On, string Source)? Pending;
        public int Merged;
    }

    readonly Dictionary<string, Dev> _d = [];
    Dev Get(string id) => _d.TryGetValue(id, out var d) ? d : _d[id] = new();

    /// <param name="reported">Last state the device reported (null if none).</param>
    public GateOutcome Offer(string id, bool on, string source, bool? reported)
    {
        var d = Get(id);
        var now = time.GetUtcNow();
        if (d.Pending is null && reported == on) return GateOutcome.Duplicate;
        var earliest = d.LastSent is { } s ? s + o.MinCommandInterval : now;
        // MinHold: only a real auto state change (opposite of the last auto one); manual and alert are exempt.
        if (source == "auto" && d.LastAutoOn is bool lo && lo != on)
            earliest = Max(earliest, d.LastAutoChange!.Value + (lo ? o.MinHoldOn : o.MinHoldOff));
        if (d.Pending is null && now >= earliest) { Record(d, on, source, now); return GateOutcome.Send; }
        d.Pending = (on, source); d.Earliest = earliest; d.Merged++;
        return GateOutcome.Coalesced;
    }

    /// <summary>Held commands whose wait is over. Merged = how many requests were folded into it; Duplicate = device already reports that state.</summary>
    public IReadOnlyList<(string Id, bool On, string Source, int Merged, bool Duplicate)> Due(Func<string, bool?> reported)
    {
        var now = time.GetUtcNow();
        List<(string, bool, string, int, bool)> r = [];
        foreach (var (id, d) in _d)
        {
            if (d.Pending is not { } p || now < d.Earliest) continue;
            var dup = reported(id) == p.On;
            if (!dup) Record(d, p.On, p.Source, now);
            r.Add((id, p.On, p.Source, d.Merged, dup));
            d.Pending = null; d.Merged = 0;
        }
        return r;
    }

    void Record(Dev d, bool on, string source, DateTimeOffset now)
    {
        d.LastSent = now;
        if (source == "auto" && on != d.LastAutoOn) { d.LastAutoOn = on; d.LastAutoChange = now; }
    }

    static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
