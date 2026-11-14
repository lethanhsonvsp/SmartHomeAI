using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;

namespace SmartHomeAI.Hub;

public interface IEventLog
{
    void Env(EnvMsg m);
    void Door(DoorMsg m);
    void Command(DeviceSetMsg m);
    void AiEvent(DateTimeOffset ts, string kind, string json);
    void Occupancy(DateTimeOffset ts, string state);
}

public class NullEventLog : IEventLog
{
    public void Env(EnvMsg m) { }
    public void Door(DoorMsg m) { }
    public void Command(DeviceSetMsg m) { }
    public void AiEvent(DateTimeOffset ts, string kind, string json) { }
    public void Occupancy(DateTimeOffset ts, string state) { }
}

/// <summary>Queues writes and runs them on a background loop so MQTT handling never waits on PostgreSQL.
/// shortcut: queue is bounded and drops newest on overflow, upgrade when logs must be lossless.</summary>
public class PgEventLog(NpgsqlDataSource db, ILogger<PgEventLog> logger) : BackgroundService, IEventLog
{
    readonly Channel<(string Sql, NpgsqlParameter[] P)> _q = Channel.CreateBounded<(string, NpgsqlParameter[])>(10_000);
    bool _ready;

    static NpgsqlParameter P(string n, object v, NpgsqlDbType t) => new(n, t) { Value = v };
    static NpgsqlParameter Ts(DateTimeOffset v) => P("ts", v.UtcDateTime, NpgsqlDbType.TimestampTz);
    static NpgsqlParameter S(string n, string v) => P(n, v, NpgsqlDbType.Text);
    static NpgsqlParameter D(string n, double v) => P(n, v, NpgsqlDbType.Double);
    static NpgsqlParameter B(string n, bool v) => P(n, v, NpgsqlDbType.Boolean);

    void Enqueue(string sql, params NpgsqlParameter[] p)
    {
        if (!_q.Writer.TryWrite((sql, p))) logger.LogWarning("DB log queue full, dropping a record");
    }

    public void Env(EnvMsg m) => Enqueue("INSERT INTO sensor_reading(ts,node,temp_c,humidity,lux) VALUES(@ts,@n,@t,@h,@l)",
        Ts(m.Ts), S("n", m.Node), D("t", m.TempC), D("h", m.Humidity), D("l", m.Lux));
    public void Door(DoorMsg m) => Enqueue("INSERT INTO door_event(ts,node,open) VALUES(@ts,@n,@o)", Ts(m.Ts), S("n", m.Node), B("o", m.Door.Open));
    public void Command(DeviceSetMsg m) => Enqueue("INSERT INTO device_command_log(ts,device,\"on\",source) VALUES(@ts,@d,@o,@s)",
        Ts(m.Ts), S("d", m.Device), B("o", m.On), S("s", m.Source));
    public void AiEvent(DateTimeOffset ts, string kind, string json) => Enqueue("INSERT INTO ai_event(ts,kind,payload) VALUES(@ts,@k,@p)",
        Ts(ts), S("k", kind), P("p", json, NpgsqlDbType.Jsonb));
    public void Occupancy(DateTimeOffset ts, string state) => Enqueue("INSERT INTO occupancy_log(ts,state) VALUES(@ts,@s)", Ts(ts), S("s", state));

    async Task<bool> InitAsync(CancellationToken ct)
    {
        try
        {
            foreach (var f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "sql"), "*.sql").Order())
            {
                await using var c = db.CreateCommand(await File.ReadAllTextAsync(f, ct));
                await c.ExecuteNonQueryAsync(ct);
            }
            logger.LogInformation("PostgreSQL ready, logging enabled");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("PostgreSQL unavailable, record dropped: {Error}", ex.Message);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _ready = await InitAsync(ct);
        await foreach (var (sql, p) in _q.Reader.ReadAllAsync(ct))
        {
            if (!_ready && !(_ready = await InitAsync(ct))) continue; // retried per record; one cheap failed connect each
            try
            {
                await using var c = db.CreateCommand(sql);
                c.Parameters.AddRange(p);
                await c.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _ready = false;
                logger.LogWarning("DB write failed: {Error}", ex.Message);
            }
        }
    }
}
