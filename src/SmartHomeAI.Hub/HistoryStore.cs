namespace SmartHomeAI.Hub;

/// <summary>In-memory ring buffers feeding GET /api/history. Own lock; callers may hold HubState's.</summary>
public class HistoryStore(TimeProvider time, int maxSamples = 5000, TimeSpan? maxAge = null, TimeSpan? personsEvery = null)
{
    public const int DefaultMinutes = 60, MaxMinutes = 360;
    readonly object _gate = new();
    readonly TimeSpan _maxAge = maxAge ?? TimeSpan.FromHours(6);
    readonly TimeSpan _every = personsEvery ?? TimeSpan.FromSeconds(10);
    readonly Queue<EnvPoint> _env = [];
    readonly Queue<OccPoint> _occ = [];
    readonly Queue<PersonsPoint> _persons = [];
    DateTimeOffset _lastPersons = DateTimeOffset.MinValue;

    public void AddEnv(EnvReading e) { lock (_gate) Add(_env, new(e.Ts.UtcDateTime, e.TempC, e.Humidity, e.Lux)); }
    public void AddOccupancy(string state) { lock (_gate) { var n = time.GetUtcNow(); Add(_occ, new(n.UtcDateTime, state)); } }

    /// <summary>Takes a persons-count sample if the sampling interval has elapsed.</summary>
    public void SamplePersons(int count)
    {
        lock (_gate)
        {
            var n = time.GetUtcNow();
            if (n - _lastPersons < _every) return;
            _lastPersons = n;
            Add(_persons, new(n.UtcDateTime, count));
        }
    }

    void Add<T>(Queue<T> q, T p) where T : IPoint
    {
        q.Enqueue(p);
        while (q.Count > maxSamples) q.Dequeue();
        // shortcut: trims by sample ts only from the head; out-of-order old env ts stay until they reach the head
        var cut = (time.GetUtcNow() - _maxAge).UtcDateTime;
        while (q.Count > 0 && q.Peek().Ts < cut) q.Dequeue();
    }

    public HistoryResult Query(int minutes)
    {
        lock (_gate)
        {
            var from = (time.GetUtcNow() - TimeSpan.FromMinutes(minutes)).UtcDateTime;
            return new([.. _env.Where(p => p.Ts >= from).OrderBy(p => p.Ts)],
                       [.. _occ.Where(p => p.Ts >= from).OrderBy(p => p.Ts)],
                       [.. _persons.Where(p => p.Ts >= from).OrderBy(p => p.Ts)]);
        }
    }

    /// <summary>Parses ?minutes=; absent -> 60. Null when not an integer in 1..360 (caller answers 400).</summary>
    public static int? ParseMinutes(string? raw)
    {
        if (raw is null) return DefaultMinutes;
        return int.TryParse(raw, out var m) && m > 0 ? Math.Min(m, MaxMinutes) : null;
    }
}

public interface IPoint { DateTime Ts { get; } }
public record EnvPoint(DateTime Ts, double TempC, double Humidity, double Lux) : IPoint;
public record OccPoint(DateTime Ts, string State) : IPoint;
public record PersonsPoint(DateTime Ts, int Count) : IPoint;
public record HistoryResult(IReadOnlyList<EnvPoint> Env, IReadOnlyList<OccPoint> Occupancy, IReadOnlyList<PersonsPoint> Persons);
