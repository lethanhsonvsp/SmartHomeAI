using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Core.Automation;

public enum CommandSource { Auto, Manual }

public record DeviceCommand(string DeviceId, bool On, CommandSource Source = CommandSource.Auto);

public record AutomationOptions
{
    public double DarkLux { get; init; } = 50;
    public double HotTempC { get; init; } = 28;
    public TimeSpan ManualBlock { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan MinInterval { get; init; } = TimeSpan.FromSeconds(60);
    public string SofaZone { get; init; } = "sofa";
    /// <summary>A device state change not matching a Hub command within this window is a manual (button/app) change.</summary>
    public TimeSpan CommandMatchWindow { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Wait before the 2nd, 3rd... send while the device has not reported state on; sends = length + 1, the last wait ends in "no response".</summary>
    public TimeSpan[] RetrySchedule { get; init; } = [TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120)];
    /// <summary>No auto right after start-up, so devices can report their state first.</summary>
    public TimeSpan StartupGrace { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>The person must lie on the sofa continuously this long before the fan turns on (needs AutomationInput.LyingFor).</summary>
    public TimeSpan SofaLyingMin { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Hub: minimum gap between any two commands to the same device (relay protection); extra ones are merged, the last one is sent.</summary>
    public TimeSpan MinCommandInterval { get; init; } = TimeSpan.FromSeconds(3);
    /// <summary>Hub: minimum time a device stays on / off between two AUTO state changes. Manual and alert commands are exempt.</summary>
    public TimeSpan MinHoldOn { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan MinHoldOff { get; init; } = TimeSpan.FromSeconds(10);
    public int MaxAutoChangesPerHour { get; init; } = 6;
    public TimeSpan AutoLockFor { get; init; } = TimeSpan.FromHours(1);
}

public record AutomationInput(OccupancyState State, IReadOnlyList<Person> Persons, double? Lux, double? TempC, bool LightOn, bool FanOn, TimeSpan? LyingFor = null);

public enum DiagnosisCode
{
    WillTurnOn, AlreadyOn, NotOccupied, LightBrightEnough, NoLuxData,
    FanNotOnSofa, FanNotLying, FanTooCool, NoTempData, BlockedByManual, Cooldown,
    LyingNotLongEnough, StartingUp, DeviceUnresponsive, AutoLocked,
}

/// <param name="Params">Keys by code: state, lux, threshold, zone, posture, temp, seconds.</param>
public record Diagnosis(string DeviceId, DiagnosisCode Code, IReadOnlyDictionary<string, object?> Params);

public interface IHabitAdvisor { bool ShouldTurnOnFan(AutomationInput context); }

public class NoHabitAdvisor : IHabitAdvisor { public bool ShouldTurnOnFan(AutomationInput context) => false; }

/// <summary>Pure rules SC01-SC05, SC09. Only turns devices on; turning off is not its job.</summary>
public class AutomationEngine(TimeProvider time, AutomationOptions? options = null, IHabitAdvisor? advisor = null)
{
    readonly AutomationOptions _o = options ?? new();
    readonly IHabitAdvisor _advisor = advisor ?? new NoHabitAdvisor();
    readonly Dictionary<string, DateTimeOffset> _manual = new(), _auto = new(), _lock = new();
    readonly Dictionary<string, bool> _reported = new();
    readonly Dictionary<string, (bool On, DateTimeOffset At)> _lastCmd = new();
    readonly Dictionary<string, (int Sent, DateTimeOffset Last)> _pending = new();
    readonly Dictionary<string, List<DateTimeOffset>> _changes = new();
    readonly DateTimeOffset _start = time.GetUtcNow();

    public AutomationOptions Options => _o;
    public IReadOnlyDictionary<string, DateTimeOffset> ManualTimes => _manual;

    public void OnManualCommand(string deviceId, bool on)
    {
        _manual[deviceId] = time.GetUtcNow();
        _lock.Remove(deviceId); _changes.Remove(deviceId); // a manual command unlocks the safety net
    }

    /// <summary>Restores a manual time saved before a restart; ignored once it is older than ManualBlock.</summary>
    public void RestoreManual(string deviceId, DateTimeOffset at)
    {
        if (time.GetUtcNow() - at < _o.ManualBlock) _manual[deviceId] = at;
    }

    /// <summary>The Hub published a command (any source); a state change matching it is not a manual change.</summary>
    public void OnHubCommand(string deviceId, bool on) => _lastCmd[deviceId] = (on, time.GetUtcNow());

    /// <summary>Device reported its state. True if it changed outside the system (button/app): counted as a manual command.</summary>
    public bool OnDeviceState(string deviceId, bool on)
    {
        var now = time.GetUtcNow();
        var known = _reported.TryGetValue(deviceId, out var prev); // first report after start-up is not a change
        _reported[deviceId] = on;
        if (on || (_pending.TryGetValue(deviceId, out var p) && Unresponsive(p))) _pending.Remove(deviceId);
        if (!known || prev == on) return false;
        if (_lastCmd.TryGetValue(deviceId, out var c) && c.On == on && now - c.At <= _o.CommandMatchWindow) return false;
        OnManualCommand(deviceId, on);
        return true;
    }

    bool Unresponsive((int Sent, DateTimeOffset Last) p) =>
        p.Sent > _o.RetrySchedule.Length && time.GetUtcNow() - p.Last >= Wait(p.Sent);

    TimeSpan Wait(int sent) => _o.RetrySchedule[Math.Min(sent, _o.RetrySchedule.Length) - 1];

    int RecentChanges(string id) =>
        _changes.TryGetValue(id, out var l) ? l.Count(t => time.GetUtcNow() - t < TimeSpan.FromHours(1)) : 0;

    public IReadOnlyList<DeviceCommand> Evaluate(AutomationInput i)
    {
        var cmds = new List<DeviceCommand>();
        foreach (var d in Diagnose(i))
            if (d.Code == DiagnosisCode.WillTurnOn)
            {
                var now = time.GetUtcNow();
                _auto[d.DeviceId] = now;
                _lastCmd[d.DeviceId] = (true, now);
                var sent = _pending.TryGetValue(d.DeviceId, out var p) ? p.Sent + 1 : 1;
                _pending[d.DeviceId] = (sent, now);
                if (sent == 1) // retries are not new changes
                    (_changes.TryGetValue(d.DeviceId, out var l) ? l : _changes[d.DeviceId] = []).Add(now);
                cmds.Add(new(d.DeviceId, true));
            }
            else if (d.Code == DiagnosisCode.AutoLocked && !_lock.ContainsKey(d.DeviceId))
            {
                _lock[d.DeviceId] = time.GetUtcNow() + _o.AutoLockFor;
                _changes.Remove(d.DeviceId);
            }
        return cmds;
    }

    /// <summary>Why each device (light, fan) would or would not be switched on now. No side effects.</summary>
    public IReadOnlyList<Diagnosis> Diagnose(AutomationInput i) => [CheckLight(i), CheckFan(i)];

    Diagnosis CheckLight(AutomationInput i)
    {
        if (i.State != OccupancyState.Occupied) return Not("light", i);
        if (i.LightOn) return new("light", DiagnosisCode.AlreadyOn, P());
        if (i.Lux is not double lux) return new("light", DiagnosisCode.NoLuxData, P());
        if (lux >= _o.DarkLux) return new("light", DiagnosisCode.LightBrightEnough, P(("lux", lux), ("threshold", _o.DarkLux)));
        return Gate("light", P(("lux", lux), ("threshold", _o.DarkLux)));
    }

    Diagnosis CheckFan(AutomationInput i)
    {
        if (i.State != OccupancyState.Occupied) return Not("fan", i);
        if (i.FanOn) return new("fan", DiagnosisCode.AlreadyOn, P());
        var onSofa = i.Persons.Where(p => p.Zone == _o.SofaZone).ToList();
        if (onSofa.Count == 0) return new("fan", DiagnosisCode.FanNotOnSofa, P(("zone", i.Persons.FirstOrDefault()?.Zone), ("posture", i.Persons.FirstOrDefault()?.Posture)));
        if (!onSofa.Any(p => p.Posture == "lying")) return new("fan", DiagnosisCode.FanNotLying, P(("posture", onSofa[0].Posture)));
        if (i.TempC is not double t) return new("fan", DiagnosisCode.NoTempData, P());
        var args = P(("temp", t), ("threshold", _o.HotTempC));
        if (t < _o.HotTempC && !_advisor.ShouldTurnOnFan(i)) return new("fan", DiagnosisCode.FanTooCool, args);
        if (i.LyingFor is TimeSpan l && l < _o.SofaLyingMin)
            return Gate("fan", args, new Diagnosis("fan", DiagnosisCode.LyingNotLongEnough, P(("seconds", Math.Floor(l.TotalSeconds)), ("need", _o.SofaLyingMin.TotalSeconds))));
        return Gate("fan", args);
    }

    static Diagnosis Not(string device, AutomationInput i) => new(device, DiagnosisCode.NotOccupied, P(("state", i.State)));

    static IReadOnlyDictionary<string, object?> P(params (string, object?)[] kv) => kv.ToDictionary(k => k.Item1, k => k.Item2);

    /// <summary>Conditions met: manual block and cooldown decide whether it really fires.</summary>
    Diagnosis Gate(string device, IReadOnlyDictionary<string, object?> args, Diagnosis? instead = null)
    {
        var now = time.GetUtcNow();
        if (now - _start < _o.StartupGrace) return new(device, DiagnosisCode.StartingUp, P());
        if (_manual.TryGetValue(device, out var m) && now - m < _o.ManualBlock)
            return new(device, DiagnosisCode.BlockedByManual, P(("seconds", Math.Ceiling((_o.ManualBlock - (now - m)).TotalSeconds))));
        if (instead is not null) return instead; // conditions are met except a timing one
        if ((_lock.TryGetValue(device, out var lu) && now < lu) || RecentChanges(device) >= _o.MaxAutoChangesPerHour)
            return new(device, DiagnosisCode.AutoLocked, P(("count", _o.MaxAutoChangesPerHour)));
        if (_pending.TryGetValue(device, out var p))
        {
            var due = p.Last + Wait(p.Sent);
            if (now < due) return new(device, DiagnosisCode.Cooldown, P(("seconds", Math.Ceiling((due - now).TotalSeconds))));
            if (p.Sent > _o.RetrySchedule.Length) return new(device, DiagnosisCode.DeviceUnresponsive, P(("count", p.Sent)));
        }
        else if (_auto.TryGetValue(device, out var a) && now - a < _o.MinInterval)
            return new(device, DiagnosisCode.Cooldown, P(("seconds", Math.Ceiling((_o.MinInterval - (now - a)).TotalSeconds))));
        return new(device, DiagnosisCode.WillTurnOn, args);
    }
}
