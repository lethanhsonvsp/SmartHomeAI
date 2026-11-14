namespace SmartHomeAI.Core.Occupancy;

public enum OccupancyState { Occupied, OtherArea, ExitPending, AwayConfirmed, Unknown }

// zone: livingroom, sofa, door, other_exit (docs/mqtt-topics.md)
public record Person(int TrackId, string Zone, string Posture = "unknown", double Conf = 1);

public record AiState(bool CameraOk, IReadOnlyList<Person> Persons);

public enum Direction { In, Out, Unknown }

public record AiTransition(int TrackId, Direction Direction, string ToZone);

public record DoorEvent(bool Open);

public record AiStatus(bool Online);

public record OccupancyOptions
{
    public TimeSpan VerifyAway { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan StateStale { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Occupied survives this long of continuous empty frames before becoming OtherArea (N03).</summary>
    public TimeSpan EmptyGrace { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Data loss up to this long keeps AwayConfirmed instead of re-verifying; matches AlertOptions.UnknownFreeze (N04).</summary>
    public TimeSpan AwayResume { get; init; } = TimeSpan.FromSeconds(60);
    public string[] RoomZones { get; init; } = ["livingroom", "sofa"];
}
