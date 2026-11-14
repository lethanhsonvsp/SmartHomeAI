using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartHomeAI.Dashboard;

public record Env(double TempC, double Humidity, double Lux);
public record Door(bool Open);
public record Device(bool On);
public record PersonInfo(int TrackId, string Zone, string Posture, double Conf);
public record HubSnapshot(string Mode, string Occupancy, Dictionary<string, Env> Env, Dictionary<string, Door> Door,
    Dictionary<string, Device> Devices, Dictionary<string, bool> Online, List<PersonInfo>? Persons = null, int PersonCount = 0);
// Hub /api/alerts is snake_case (raised_at, cancelled_at); reason: PersonReturned|DevicesOff|DataLost|null; feedback: None|Correct|Wrong.
public record AlertItem(string Id,
    [property: JsonPropertyName("raised_at")] DateTimeOffset RaisedAt,
    [property: JsonPropertyName("cancelled_at")] DateTimeOffset? CancelledAt,
    string? Reason, string Feedback);

public record EnvPt(DateTimeOffset Ts, double TempC, double Humidity, double Lux);
public record OccPt(DateTimeOffset Ts, string State);
public record PersonPt(DateTimeOffset Ts, int Count);
public record EspState(double TempC, double Humidity, double Lux, bool DoorOpen, bool Buzzer);
public record History(List<EnvPt> Env, List<OccPt> Occupancy, List<PersonPt> Persons);

/// <summary>Server-side client for the Hub REST API; null/false results mean Hub unreachable or failed.</summary>
public class HubClient(HttpClient http)
{
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    async Task<T?> Get<T>(string url) where T : class
    {
        try { return await http.GetFromJsonAsync<T>(url, J); } catch (Exception) { return null; }
    }

    async Task<bool> Post(string url, object body)
    {
        try { return (await http.PostAsJsonAsync(url, body, J)).IsSuccessStatusCode; } catch (Exception) { return false; }
    }

    public Task<HubSnapshot?> State() => Get<HubSnapshot>("/api/state");
    public Task<List<AlertItem>?> Alerts() => Get<List<AlertItem>>("/api/alerts");
    public Task<History?> History(int minutes) => Get<History>($"/api/history?minutes={minutes}");
    public Task<bool> SetMode(bool auto) => Post("/api/mode", new { auto });
    public Task<bool> SetDevice(string id, bool on) => Post($"/api/device/{id}", new { on });
    public Task<bool> Feedback(string id, bool correct) => Post($"/api/alerts/{Uri.EscapeDataString(id)}/feedback", new { correct });
}
