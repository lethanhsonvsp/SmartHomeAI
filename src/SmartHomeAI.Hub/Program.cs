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
builder.Services.AddSingleton<HubState>();
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
app.MapPost("/api/mode", (ModeRequest r, HubState s) => { s.Auto = r.Auto; return Results.Json(new { auto = r.Auto }); });
app.MapPost("/api/device/{id}", (string id, DeviceRequest r, MessageRouter router) =>
    router.PublishManual(id, r.On) ? Results.Json(new { id, on = r.On }) : Results.BadRequest(new { error = "id must be light or fan" }));
app.MapGet("/api/alerts", (HubState s) => Results.Json(s.AlertHistory().Select(r => new
{
    id = r.Id, raised_at = r.RaisedAt, cancelled_at = r.CancelledAt,
    reason = r.Reason?.ToString(), feedback = r.Feedback.ToString(),
})));
app.MapPost("/api/alerts/{id}/feedback", (string id, FeedbackRequest r, MessageRouter router) =>
    router.Feedback(id, r.Correct) ? Results.Json(new { id, correct = r.Correct }) : Results.NotFound(new { error = "unknown alert id" }));

app.Run();

public record ModeRequest(bool Auto);
public record DeviceRequest(bool On);
public record FeedbackRequest(bool Correct);

public partial class Program;
