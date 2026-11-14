using Npgsql;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(sp => new OccupancyEngine(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<HubState>();
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

app.Run();

public partial class Program;
