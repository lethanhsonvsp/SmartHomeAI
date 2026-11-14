using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public record EnvReading(DateTimeOffset Ts, double TempC, double Humidity, double Lux);
public record DeviceReading(DateTimeOffset Ts, bool On);

/// <summary>Latest-value store; one lock guards it and the (non-thread-safe) OccupancyEngine.</summary>
public class HubState(OccupancyEngine engine, AutomationEngine automation, AlertManager alerts, HistoryStore? history = null, DecisionLog? decisions = null,
    TimeProvider? clock = null, ManualStore? manualStore = null)
{
    readonly TimeProvider _clock = clock ?? TimeProvider.System;
    DateTimeOffset? _lyingSince; // continuous lying on the sofa, from AiState
    readonly bool _restored = Restore(manualStore, automation); // keeps a manual pause across restarts
    public DecisionLog Decisions { get; } = decisions ?? new(TimeProvider.System);
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
                case StatusMsg s:
                    var wasOnline = _online.TryGetValue(s.Source, out var o) && o;
                    _online[s.Source] = s.Status.Online;
                    if (s.Source == "device" && s.Status.Online && !wasOnline)
                    {
                        automation.OnDeviceOnline();
                        Decisions.Add("device", "Ổ cắm kết nối lại: đồng bộ trạng thái ban đầu (không tính là lệnh tay)");
                    } if (s.Source == "ai") engine.OnAiStatus(s.Status);
                    break;
                case DeviceStateMsg d:
                    if (automation.OnDeviceState(d.Device, d.On))
                    {
                        Decisions.Add("manual", $"{DecisionText.Cap(DecisionText.DeviceName(d.Device))} bị {(d.On ? "bật" : "tắt")} ngoài hệ thống (nút/ứng dụng): tự động tạm dừng", d.Device, d.On);
                        SaveManual();
                    }
                    if (!_devices.TryGetValue(d.Device, out var old) || old.On != d.On)
                        Decisions.Add("device", $"{DecisionText.Cap(DecisionText.DeviceName(d.Device))} đã {(d.On ? "bật" : "tắt")}", d.Device, d.On);
                    _devices[d.Device] = new(d.Ts, d.On); break;
                case DeviceSetMsg { Source: not "manual" } c: automation.OnHubCommand(c.Device, c.On); break;
                case DeviceSetMsg c:
                    automation.OnHubCommand(c.Device, c.On);
                    automation.OnManualCommand(c.Device, c.On);
                    SaveManual();
                    Decisions.Add("manual", $"Bạn {(c.On ? "bật" : "tắt")} {DecisionText.DeviceName(c.Device)} bằng tay, tự động tạm dừng với thiết bị này", c.Device, c.On);
                    break;
                case AiStateMsg a:
                    _persons = a.State.Persons;
                    if (_persons.Any(p => p.Zone == automation.Options.SofaZone && p.Posture == "lying")) _lyingSince ??= _clock.GetUtcNow(); else _lyingSince = null; engine.OnAiState(a.State); break;
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

    /// <summary>The Hub published a command itself (auto/alert); its state echo is not a manual change.</summary>
    public void NoteCommand(string id, bool on) { lock (_gate) automation.OnHubCommand(id, on); }

    public AutomationOptions AutomationOptions => automation.Options;

    /// <summary>Last state the device reported; null if none yet.</summary>
    public bool? IsOn(string id) { lock (_gate) return _devices.TryGetValue(id, out var d) ? d.On : null; }

    void SaveManual() => manualStore?.Save(automation.ManualTimes);

    static bool Restore(ManualStore? store, AutomationEngine automation)
    {
        if (store is not null) foreach (var (id, at) in store.Load()) automation.RestoreManual(id, at);
        return true;
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
            AutomationInput input = new(engine.State, _persons, latest?.Lux, latest?.TempC, On("light"), On("fan"),
                _lyingSince is { } ls ? _clock.GetUtcNow() - ls : TimeSpan.Zero);
            Decisions.Decide(automation.Diagnose(input));
            return automation.Evaluate(input);
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
        Decisions.Add("occupancy", _last is { } p ? $"Hiện diện: {DecisionText.State(p)} → {DecisionText.State(s)}" : $"Hiện diện: {DecisionText.State(s)}");
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
