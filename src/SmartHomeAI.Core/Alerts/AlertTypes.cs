using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Core.Alerts;

public enum AlertLevel { First, Reminder }
public enum CancelReason { PersonReturned, DevicesOff, DataLost }
public enum Feedback { None, Correct, Wrong }

/// <param name="AutoOffAllowed">Device ids the user allows to be switched off automatically.</param>
public record AlertInput(OccupancyState State, bool LightOn, bool FanOn, IReadOnlyCollection<string> AutoOffAllowed);

public abstract record AlertAction;
public record RaiseAlert(string Id, IReadOnlyList<string> DevicesOn, AlertLevel Level) : AlertAction;
public record CancelAlert(string Id, CancelReason Reason) : AlertAction;
public record AutoOff(IReadOnlyList<string> DeviceIds) : AlertAction;

/// <summary>CancelledAt/Reason are null while the alert is still active.</summary>
public record AlertRecord(string Id, DateTimeOffset RaisedAt, DateTimeOffset? CancelledAt, CancelReason? Reason, Feedback Feedback);

public record AlertOptions
{
    public TimeSpan FirstAfter { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan RemindEvery { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan AutoOffAfter { get; init; } = TimeSpan.FromMinutes(10);
    public bool AutoOffEnabled { get; init; }
}
