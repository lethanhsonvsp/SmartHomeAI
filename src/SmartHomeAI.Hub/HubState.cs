using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public record EnvReading(DateTimeOffset Ts, double TempC, double Humidity, double Lux);
public record DeviceReading(DateTimeOffset Ts, bool On);

/// <summary>Latest-value store; one lock guards it and the (non-thread-safe) OccupancyEngine.</summary>
public class HubState(OccupancyEngine engine)
{
    readonly object _gate = new();
    readonly Dictionary<string, EnvReading> _env = [];
    readonly Dictionary<string, (DateTimeOffset Ts, bool Open)> _door = [];
    readonly Dictionary<string, DeviceReading> _devices = [];
    readonly Dictionary<string, bool> _online = [];
    OccupancyState? _last;

    /// <summary>Applies a message; returns the new occupancy state if it changed since last call.</summary>
    public OccupancyState? Apply(Msg m)
    {
        lock (_gate)
        {
            switch (m)
            {
                case EnvMsg e: _env[e.Node] = new(e.Ts, e.TempC, e.Humidity, e.Lux); break;
                case DoorMsg d: _door[d.Node] = (d.Ts, d.Door.Open); engine.OnDoor(d.Door); break;
                case StatusMsg s: _online[s.Source] = s.Status.Online; if (s.Source == "ai") engine.OnAiStatus(s.Status); break;
                case DeviceStateMsg d: _devices[d.Device] = new(d.Ts, d.On); break;
                case AiStateMsg a: engine.OnAiState(a.State); break;
                case AiTransitionMsg t: engine.OnTransition(t.Transition); break;
            }
            return Changed();
        }
    }

    /// <summary>Time alone can change occupancy (stale data, verification elapsed).</summary>
    public OccupancyState? Tick() { lock (_gate) return Changed(); }

    OccupancyState? Changed()
    {
        var s = engine.State;
        if (s == _last) return null;
        _last = s;
        return s;
    }

    public object Snapshot()
    {
        lock (_gate)
            return new
            {
                occupancy = engine.State.ToString(),
                env = new Dictionary<string, EnvReading>(_env),
                door = _door.ToDictionary(k => k.Key, k => new { ts = k.Value.Ts, open = k.Value.Open }),
                devices = new Dictionary<string, DeviceReading>(_devices),
                online = new Dictionary<string, bool>(_online),
            };
    }
}
