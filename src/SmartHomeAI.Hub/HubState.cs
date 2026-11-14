using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public record EnvReading(DateTimeOffset Ts, double TempC, double Humidity, double Lux);
public record DeviceReading(DateTimeOffset Ts, bool On);

/// <summary>Latest-value store; one lock guards it and the (non-thread-safe) OccupancyEngine.</summary>
public class HubState(OccupancyEngine engine, AutomationEngine automation, AlertManager alerts, HistoryStore? history = null)
{
    public HistoryStore History { get; } = history ?? new(TimeProvider.System);
    readonly object _gate = new();
    readonly Dictionary<string, EnvReading> _env = [];
    readonly Dictionary<string, (DateTimeOffset Ts, bool Open)> _door = [];
    readonly Dictionary<string, DeviceReading> _devices = [];
    readonly Dictionary<string, bool> _online = [];
    OccupancyState? _last;
    IReadOnlyList<Person> _persons = [];
    bool _auto = true;

    /// <summary>Applies a message; returns the new occupancy state if it changed since last call.</summary>
    public OccupancyState? Apply(Msg m)
    {
        lock (_gate)
        {
            switch (m)
            {
                case EnvMsg e: _env[e.Node] = new(e.Ts, e.TempC, e.Humidity, e.Lux); History.AddEnv(_env[e.Node]); break;
                case DoorMsg d: _door[d.Node] = (d.Ts, d.Door.Open); engine.OnDoor(d.Door); break;
                case StatusMsg s: _online[s.Source] = s.Status.Online; if (s.Source == "ai") engine.OnAiStatus(s.Status); break;
                case DeviceStateMsg d: _devices[d.Device] = new(d.Ts, d.On); break;
                case DeviceSetMsg { Source: "manual" } c: automation.OnManualCommand(c.Device, c.On); break;
                case AiStateMsg a: _persons = a.State.Persons; engine.OnAiState(a.State); break;
                case AiTransitionMsg t: engine.OnTransition(t.Transition); break;
            }
            return Changed();
        }
    }

    /// <summary>Time alone can change occupancy (stale data, verification elapsed).</summary>
    public OccupancyState? Tick()
    {
        lock (_gate)
        {
            var c = Changed();
            History.SamplePersons(engine.State == OccupancyState.Unknown ? 0 : _persons.Count);
            return c;
        }
    }

    public bool Auto { get { lock (_gate) return _auto; } set { lock (_gate) _auto = value; } }

    /// <summary>Auto commands for the current state; empty in Manual mode.</summary>
    public IReadOnlyList<DeviceCommand> Automate()
    {
        lock (_gate)
        {
            if (!_auto) return [];
            var latest = _env.Values.OrderByDescending(e => e.Ts).FirstOrDefault();
            bool On(string id) => _devices.TryGetValue(id, out var d) && d.On;
            return automation.Evaluate(new(engine.State, _persons, latest?.Lux, latest?.TempC, On("light"), On("fan")));
        }
    }

    /// <summary>Runs the alert rules on the current state (call every tick and after each message).</summary>
    public IReadOnlyList<AlertAction> EvaluateAlerts(IReadOnlyCollection<string> autoOffAllowed)
    {
        lock (_gate)
        {
            bool On(string id) => _devices.TryGetValue(id, out var d) && d.On;
            return alerts.Evaluate(new(engine.State, On("light"), On("fan"), autoOffAllowed));
        }
    }

    public IReadOnlyList<AlertRecord> AlertHistory() { lock (_gate) return [.. alerts.History]; }
    public bool AlertFeedback(string id, bool correct) { lock (_gate) return alerts.OnUserFeedback(id, correct); }

    OccupancyState? Changed()
    {
        var s = engine.State;
        if (s == _last) return null;
        _last = s;
        History.AddOccupancy(s.ToString());
        return s;
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            // Unknown = AI offline / camera down / state stale (same StateStale rule as Occupancy): no persons.
            var persons = engine.State == OccupancyState.Unknown ? [] : _persons;
            return new
            {
                persons = persons.Select(p => new { trackId = p.TrackId, zone = p.Zone, posture = p.Posture, conf = p.Conf }).ToList(),
                personCount = persons.Count,
                mode = _auto ? "auto" : "manual",
                occupancy = engine.State.ToString(),
                env = new Dictionary<string, EnvReading>(_env),
                door = _door.ToDictionary(k => k.Key, k => new { ts = k.Value.Ts, open = k.Value.Open }),
                devices = new Dictionary<string, DeviceReading>(_devices),
                online = new Dictionary<string, bool>(_online),
            };
        }
    }
}
