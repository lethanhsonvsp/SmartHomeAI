using SmartHomeAI.Core.Automation;

namespace SmartHomeAI.Core.Habits;

public enum HabitSource { Manual, Auto }
public enum HabitCondition { TempAtLeast, LuxBelow }
public enum HabitStatus { Proposed, Approved, Disabled }

public record HabitObservation(DateTimeOffset Time, string DeviceId, bool On, HabitSource Source,
    double? TempC = null, double? Lux = null, string? Posture = null, string? Zone = null);

public record HabitRule(string Id, string Device, HabitCondition Condition, double Threshold, int Support,
    HabitStatus Status, DateTimeOffset UpdatedAt);

public record HabitOptions
{
    public int MinSupport { get; init; } = 5;
    public int WindowCount { get; init; } = 30;
    public TimeSpan WindowAge { get; init; } = TimeSpan.FromDays(30);
    public double MaxTempSpread { get; init; } = 3;   // max - min of the samples, deg C
    public double MaxLuxSpread { get; init; } = 40;
    public double TempTolerance { get; init; } = 0.5; // smaller change is not a new proposal
    public double LuxTolerance { get; init; } = 5;
    public string SofaZone { get; init; } = "sofa";
}

/// <summary>NV03 / SC12. Learns threshold rules from manual ON actions; only Approved rules are used.
/// Threshold = lower quartile of the samples (the user acts at the low end of what they tolerate).</summary>
public class HabitEngine(TimeProvider time, HabitOptions? options = null) : IHabitAdvisor
{
    readonly HabitOptions _o = options ?? new();
    readonly List<HabitObservation> _obs = [];
    readonly List<HabitRule> _rules = [];
    readonly object _lock = new();
    int _seq;

    public IReadOnlyList<HabitRule> Rules { get { lock (_lock) return [.. _rules]; } }

    public void Record(HabitObservation o)
    {
        lock (_lock)
        {
            _obs.Add(o);
            Learn("fan", HabitCondition.TempAtLeast);
            Learn("light", HabitCondition.LuxBelow);
        }
    }

    public bool Approve(string id) => Set(id, HabitStatus.Approved);
    public bool Disable(string id) => Set(id, HabitStatus.Disabled);

    public bool Edit(string id, double threshold)
    {
        lock (_lock)
        {
            var i = _rules.FindIndex(r => r.Id == id);
            if (i < 0) return false;
            _rules[i] = _rules[i] with { Threshold = threshold, UpdatedAt = time.GetUtcNow() };
            return true;
        }
    }

    public bool ShouldTurnOnFan(AutomationInput c)
    {
        if (c.TempC is not double t || !c.Persons.Any(p => p.Zone == _o.SofaZone && p.Posture == "lying")) return false;
        lock (_lock)
            return _rules.Any(r => r.Status == HabitStatus.Approved && r.Device == "fan"
                && r.Condition == HabitCondition.TempAtLeast && t >= r.Threshold);
    }

    bool Set(string id, HabitStatus s)
    {
        lock (_lock)
        {
            var i = _rules.FindIndex(r => r.Id == id);
            if (i < 0) return false;
            var now = time.GetUtcNow();
            if (s == HabitStatus.Approved) // a newly approved rule supersedes the older one for that device/condition
                for (var j = 0; j < _rules.Count; j++)
                    if (j != i && _rules[j].Status == HabitStatus.Approved && _rules[j].Device == _rules[i].Device
                        && _rules[j].Condition == _rules[i].Condition)
                        _rules[j] = _rules[j] with { Status = HabitStatus.Disabled, UpdatedAt = now };
            _rules[i] = _rules[i] with { Status = s, UpdatedAt = now };
            return true;
        }
    }

    void Learn(string device, HabitCondition cond)
    {
        var now = time.GetUtcNow();
        var fan = device == "fan";
        var xs = _obs.Where(o => o.DeviceId == device && o.On && o.Source == HabitSource.Manual && now - o.Time <= _o.WindowAge
                && (fan ? o.Posture == "lying" && o.Zone == _o.SofaZone : true))
            .Select(o => fan ? o.TempC : o.Lux).OfType<double>().TakeLast(_o.WindowCount).Order().ToList();
        if (xs.Count < _o.MinSupport || xs[^1] - xs[0] > (fan ? _o.MaxTempSpread : _o.MaxLuxSpread)) return;

        var thr = xs[(xs.Count - 1) / 4];
        var tol = fan ? _o.TempTolerance : _o.LuxTolerance;
        bool Near(HabitRule r) => Math.Abs(r.Threshold - thr) < tol;
        var mine = _rules.Where(r => r.Device == device && r.Condition == cond).ToList();

        if (mine.Any(r => r.Status != HabitStatus.Proposed && Near(r))) // already known or deliberately refused
        {
            var k = _rules.FindIndex(r => r.Device == device && r.Status == HabitStatus.Approved && Near(r));
            if (k >= 0) _rules[k] = _rules[k] with { Support = xs.Count };
            return;
        }
        var p = _rules.FindIndex(r => r.Device == device && r.Condition == cond && r.Status == HabitStatus.Proposed);
        if (p >= 0) _rules[p] = _rules[p] with { Threshold = thr, Support = xs.Count, UpdatedAt = now };
        else _rules.Add(new($"{device}-{++_seq}", device, cond, thr, xs.Count, HabitStatus.Proposed, now));
    }
}
