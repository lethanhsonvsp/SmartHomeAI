using Npgsql;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(sp => new OccupancyEngine(sp.GetRequiredService<TimeProvider>(), HubConfig.Occupancy(builder.Configuration)));
builder.Services.AddSingleton(sp => new AutomationEngine(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new AlertManager(sp.GetRequiredService<TimeProvider>(),
    builder.Configuration.GetSection("Alerts").Get<AlertOptions>()));
builder.Services.AddSingleton(new AlertWiring(builder.Configuration.GetSection("Alerts:AutoOffAllowed").Get<string[]>() ?? []));
builder.Services.AddSingleton(sp => new HistoryStore(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new DecisionLog(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new HubState(sp.GetRequiredService<OccupancyEngine>(), sp.GetRequiredService<AutomationEngine>(),
    sp.GetRequiredService<AlertManager>(), sp.GetRequiredService<HistoryStore>(), sp.GetRequiredService<DecisionLog>()));
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
builder.Services.AddHostedService<MqttService>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/state", (HubState s) => Results.Json(s.Snapshot()));
app.MapGet("/api/history", (string? minutes, HubState s) =>
    HistoryStore.ParseMinutes(minutes) is { } m ? Results.Json(s.History.Query(m))
        : Results.BadRequest(new { error = "minutes must be an integer > 0" }));
app.MapGet("/api/events", (string? limit, HubState s) =>
    DecisionLog.ParseLimit(limit) is { } n ? Results.Json(s.Decisions.Latest(n).Select(e => new { ts = e.Ts, kind = e.Kind, device = e.Device, on = e.On, message = e.Message }))
        : Results.BadRequest(new { error = "limit must be an integer in 1..200" }));
var write = app.MapGroup("/api").AddEndpointFilterFactory(ApiKey.Filter);
write.MapPost("/mode", (ModeRequest r, HubState s) => { s.Auto = r.Auto; return Results.Json(new { auto = r.Auto }); });
write.MapPost("/device/{id}", (string id, DeviceRequest r, MessageRouter router) =>
    router.PublishManual(id, r.On) ? Results.Json(new { id, on = r.On }) : Results.BadRequest(new { error = "id must be light or fan" }));
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

public partial class Program;
