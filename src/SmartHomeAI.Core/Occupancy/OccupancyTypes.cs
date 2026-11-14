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
    public string[] RoomZones { get; init; } = ["livingroom", "sofa"];
}
