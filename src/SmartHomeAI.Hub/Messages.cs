using System.Text.Json;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public abstract record Msg(DateTimeOffset Ts);
public record EnvMsg(DateTimeOffset Ts, string Node, double TempC, double Humidity, double Lux) : Msg(Ts);
public record DoorMsg(DateTimeOffset Ts, string Node, DoorEvent Door) : Msg(Ts);
public record StatusMsg(DateTimeOffset Ts, string Source, AiStatus Status) : Msg(Ts);
public record DeviceStateMsg(DateTimeOffset Ts, string Device, bool On) : Msg(Ts);
public record DeviceSetMsg(DateTimeOffset Ts, string Device, bool On, string Source) : Msg(Ts);
public record AiStateMsg(DateTimeOffset Ts, AiState State) : Msg(Ts);
public record AiTransitionMsg(DateTimeOffset Ts, AiTransition Transition, string FromZone, string ToZone, double Conf, string Raw) : Msg(Ts);

/// <summary>Topic + payload to typed message per docs/mqtt-topics.md. Returns null for unknown topics; throws FormatException on bad payload.</summary>
public static class MqttParser
{
    static readonly string[] Zones = ["livingroom", "sofa", "door", "other_exit"];
    static readonly string[] Postures = ["standing", "sitting", "lying", "moving", "unknown"];
    static readonly string[] Sources = ["auto", "manual", "alert"];

    public static Msg? Parse(string topic, string payload)
    {
        var t = topic.Split('/');
        if (t.Length < 3 || t[0] != "home") return null;
        Func<JsonElement, Msg>? f = (t.Length, t[1], t[2]) switch
        {
            (4, "sensor", var n) when t[3] == "env" => e => new EnvMsg(Ts(e), n, Num(e, "temp_c"), Num(e, "humidity", 0, 100), Num(e, "lux", 0)),
            (4, "sensor", var n) when t[3] == "door" => e => new DoorMsg(Ts(e), n, new DoorEvent(Bool(e, "open"))),
            (4, "sensor", var n) when t[3] == "status" => e => new StatusMsg(Ts(e), "sensor/" + n, new AiStatus(Bool(e, "online"))),
            (3, "device", "status") => e => new StatusMsg(Ts(e), "device", new AiStatus(Bool(e, "online"))),
            (4, "device", var d) when t[3] == "state" => e => new DeviceStateMsg(Ts(e), d, Bool(e, "on")),
            (4, "device", var d) when t[3] == "set" => e => new DeviceSetMsg(Ts(e), d, Bool(e, "on"), Enum(e, "source", Sources)),
            (3, "ai", "status") => e => new StatusMsg(Ts(e), "ai", new AiStatus(Bool(e, "online"))),
            (4, "ai", "livingroom") when t[3] == "state" => e => new AiStateMsg(Ts(e), ParseState(e)),
            (4, "ai", "livingroom") when t[3] == "transition" => e => ParseTransition(e, payload),
            _ => null,
        };
        if (f is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("not an object");
            return f(doc.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new FormatException($"{topic}: {ex.Message}", ex);
        }
    }

    static JsonElement Need(JsonElement e, string k) => e.TryGetProperty(k, out var v) ? v : throw new FormatException($"missing {k}");
    static DateTimeOffset Ts(JsonElement e) => DateTimeOffset.TryParse(Need(e, "ts").GetString(), out var d) ? d : throw new FormatException("bad ts");
    static bool Bool(JsonElement e, string k) => Need(e, k) is { ValueKind: JsonValueKind.True or JsonValueKind.False } v ? v.GetBoolean() : throw new FormatException($"{k} not bool");
    static double Num(JsonElement e, string k, double min = double.MinValue, double max = double.MaxValue)
    {
        var v = Need(e, k);
        if (v.ValueKind != JsonValueKind.Number) throw new FormatException($"{k} not number");
        var d = v.GetDouble();
        return d >= min && d <= max ? d : throw new FormatException($"{k} out of range");
    }
    static int Int(JsonElement e, string k) => Need(e, k) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : throw new FormatException($"{k} not integer");
    static string Enum(JsonElement e, string k, string[] allowed) =>
        Need(e, k).GetString() is { } s && allowed.Contains(s) ? s : throw new FormatException($"bad {k}");

    static AiState ParseState(JsonElement e)
    {
        Int(e, "seq");
        var arr = Need(e, "persons");
        if (arr.ValueKind != JsonValueKind.Array) throw new FormatException("persons not array");
        var persons = arr.EnumerateArray().Select(p => p.ValueKind == JsonValueKind.Object
            ? new Person(Int(p, "track_id"), Enum(p, "zone", Zones), Enum(p, "posture", Postures), Num(p, "conf", 0, 1))
            : throw new FormatException("person not object")).ToList();
        return new AiState(Bool(e, "camera_ok"), persons);
    }

    static AiTransitionMsg ParseTransition(JsonElement e, string raw)
    {
        var dir = Enum(e, "direction", ["in", "out", "unknown"]) switch { "in" => Direction.In, "out" => Direction.Out, _ => Direction.Unknown };
        return new AiTransitionMsg(Ts(e), new AiTransition(Int(e, "track_id"), dir), Enum(e, "from_zone", Zones), Enum(e, "to_zone", Zones), Num(e, "conf", 0, 1), raw);
    }
}
