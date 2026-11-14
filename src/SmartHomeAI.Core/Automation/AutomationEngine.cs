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
}

public record AutomationInput(OccupancyState State, IReadOnlyList<Person> Persons, double? Lux, double? TempC, bool LightOn, bool FanOn);

public enum DiagnosisCode
{
    WillTurnOn, AlreadyOn, NotOccupied, LightBrightEnough, NoLuxData,
    FanNotOnSofa, FanNotLying, FanTooCool, NoTempData, BlockedByManual, Cooldown,
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
    readonly Dictionary<string, DateTimeOffset> _manual = new(), _auto = new();

    public void OnManualCommand(string deviceId, bool on) => _manual[deviceId] = time.GetUtcNow();

    public IReadOnlyList<DeviceCommand> Evaluate(AutomationInput i)
    {
        var cmds = new List<DeviceCommand>();
        foreach (var d in Diagnose(i))
            if (d.Code == DiagnosisCode.WillTurnOn)
            {
                _auto[d.DeviceId] = time.GetUtcNow();
                cmds.Add(new(d.DeviceId, true));
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
        return Gate("fan", args);
    }

    static Diagnosis Not(string device, AutomationInput i) => new(device, DiagnosisCode.NotOccupied, P(("state", i.State)));

    static IReadOnlyDictionary<string, object?> P(params (string, object?)[] kv) => kv.ToDictionary(k => k.Item1, k => k.Item2);

    /// <summary>Conditions met: manual block and cooldown decide whether it really fires.</summary>
    Diagnosis Gate(string device, IReadOnlyDictionary<string, object?> args)
    {
        var now = time.GetUtcNow();
        if (_manual.TryGetValue(device, out var m) && now - m < _o.ManualBlock)
            return new(device, DiagnosisCode.BlockedByManual, P(("seconds", Math.Ceiling((_o.ManualBlock - (now - m)).TotalSeconds))));
        if (_auto.TryGetValue(device, out var a) && now - a < _o.MinInterval)
            return new(device, DiagnosisCode.Cooldown, P(("seconds", Math.Ceiling((_o.MinInterval - (now - a)).TotalSeconds))));
        return new(device, DiagnosisCode.WillTurnOn, args);
    }
}
