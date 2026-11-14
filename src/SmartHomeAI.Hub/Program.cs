using Npgsql;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(sp => new OccupancyEngine(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new AutomationEngine(sp.GetRequiredService<TimeProvider>()));
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

app.Run();

public record ModeRequest(bool Auto);
public record DeviceRequest(bool On);

public partial class Program;
