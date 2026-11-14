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
        if (i.State != OccupancyState.Occupied) return cmds; // OtherArea (SC05), Unknown, Away, ExitPending: no action

        if (i.Lux < _o.DarkLux && !i.LightOn) TryOn("light", cmds);

        var lying = i.Persons.Any(p => p.Zone == _o.SofaZone && p.Posture == "lying");
        if (lying && !i.FanOn && i.TempC is double t
            && (t >= _o.HotTempC || _advisor.ShouldTurnOnFan(i))) TryOn("fan", cmds);
        return cmds;
    }

    void TryOn(string device, List<DeviceCommand> cmds)
    {
        var now = time.GetUtcNow();
        if (_manual.TryGetValue(device, out var m) && now - m < _o.ManualBlock) return;
        if (_auto.TryGetValue(device, out var a) && now - a < _o.MinInterval) return;
        _auto[device] = now;
        cmds.Add(new(device, true));
    }
}
