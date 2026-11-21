using Npgsql;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(sp => new OccupancyEngine(sp.GetRequiredService<TimeProvider>(), HubConfig.Occupancy(builder.Configuration)));
builder.Services.AddSingleton(sp => new AutomationEngine(sp.GetRequiredService<TimeProvider>(),
    builder.Configuration.GetSection("Automation").Get<AutomationOptions>()));
builder.Services.AddSingleton(sp => new ManualStore(builder.Configuration["Automation:ManualPath"] ?? "./data/manual.json", sp.GetRequiredService<ILogger<ManualStore>>()));
builder.Services.AddSingleton(sp => new AlertManager(sp.GetRequiredService<TimeProvider>(),
    (builder.Configuration.GetSection("Alerts").Get<AlertOptions>() ?? new()) with { IdPrefix = $"{DateTimeOffset.UtcNow:yyMMddHHmmss}-" }));
builder.Services.AddSingleton(new AlertWiring(builder.Configuration.GetSection("Alerts:AutoOffAllowed").Get<string[]>() ?? []));
builder.Services.AddSingleton(sp => new HistoryStore(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new DecisionLog(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new HubState(sp.GetRequiredService<OccupancyEngine>(), sp.GetRequiredService<AutomationEngine>(),
    sp.GetRequiredService<AlertManager>(), sp.GetRequiredService<HistoryStore>(), sp.GetRequiredService<DecisionLog>(),
    sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ManualStore>()));
builder.Services.AddSingleton<MqttBus>();
builder.Services.AddSingleton<IMqttPublisher>(sp => sp.GetRequiredService<MqttBus>());
builder.Services.AddSingleton<MessageRouter>();

var cs = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(cs)) builder.Services.AddSingleton<IEventLog, NullEventLog>();
else
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(cs));
    builder.Services.AddSingleton<PgEventLog>();
    builder.Services.AddSingleton<IEventLog>(sp => sp.GetRequiredService<PgEventLog>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<PgEventLog>());
}
builder.Services.AddSingleton<TapoP100Adapter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TapoP100Adapter>());
builder.Services.AddHostedService<MqttService>();

var app = builder.Build();
// Production never starts open: API writes and the MQTT broker both need credentials.
if (!app.Environment.IsDevelopment())
    if (ApiKey.MissingForProduction(app.Configuration) is { } k)
        throw new InvalidOperationException($"Config '{k}' is required outside Development (env {k.Replace(":", "__")}).");
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health", (IServiceProvider sp) => Results.Ok(new { status = "ok", dbDropped = sp.GetService<PgEventLog>()?.Dropped ?? 0 }));
app.MapGet("/api/state", (HubState s) => Results.Json(s.Snapshot()));
app.MapGet("/api/history", (string? minutes, HubState s) =>
    HistoryStore.ParseMinutes(minutes) is { } m ? Results.Json(s.History.Query(m))
        : Results.BadRequest(new { error = "minutes must be an integer > 0" }));
app.MapGet("/api/events", (string? limit, HubState s) =>
    DecisionLog.ParseLimit(limit) is { } n ? Results.Json(s.Decisions.Latest(n).Select(e => new { ts = e.Ts, kind = e.Kind, device = e.Device, on = e.On, message = e.Message }))
        : Results.BadRequest(new { error = "limit must be an integer in 1..200" }));
var write = app.MapGroup("/api").AddEndpointFilterFactory(ApiKey.Filter);
write.MapPost("/mode", (ModeRequest r, HubState s) => { s.Auto = r.Auto; return Results.Json(new { auto = r.Auto }); });
write.MapPost("/device/{id}", (string id, DeviceRequest r, MessageRouter router, HubState s) =>
    s.Auto ? Results.Json(new { error = "manual control needs Manual mode" }, statusCode: 409) : router.TryPublishManual(id, r.On, out var held) ? Results.Json(held ? new Dictionary<string, object> { ["id"] = id, ["on"] = r.On, ["coalesced"] = true } : new Dictionary<string, object> { ["id"] = id, ["on"] = r.On }) : Results.BadRequest(new { error = "id must be light or fan" }));
app.MapGet("/api/tapo", (TapoP100Adapter t) => Results.Json(new { state = t.Status.State, error = t.Status.Error, host = t.Host }));
write.MapPost("/tapo/login", (TapoLoginRequest r, TapoP100Adapter t) =>
    t.Login(r.User, r.Password, r.Host) ? Results.Accepted() : Results.BadRequest(new { error = "need user, password, IPv4/IPv6 host and no active session" }));
write.MapPost("/tapo/logout", (TapoP100Adapter t) => { t.Logout(); return Results.Ok(); });
app.MapGet("/api/alerts", (HubState s) => Results.Json(s.AlertHistory().Select(r => new
{
    id = r.Id, raised_at = r.RaisedAt, cancelled_at = r.CancelledAt,
    reason = r.Reason?.ToString(), feedback = r.Feedback.ToString(),
})));
write.MapPost("/alerts/{id}/feedback", (string id, FeedbackRequest r, MessageRouter router) =>
    router.Feedback(id, r.Correct) ? Results.Json(new { id, correct = r.Correct }) : Results.NotFound(new { error = "unknown alert id" }));

app.Run();

public record ModeRequest(bool Auto);
public record DeviceRequest(bool On);
public record FeedbackRequest(bool Correct);
public record TapoLoginRequest(string? User, string? Password, string? Host);

public partial class Program;
