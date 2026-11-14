namespace SmartHomeAI.Hub;

/// <summary>Broker-free core of the Hub: topic+payload in, state and log out.</summary>
public class MessageRouter(HubState state, IEventLog log, TimeProvider time, ILogger<MessageRouter> logger)
{
    public void Handle(string topic, string payload)
    {
        Msg? m;
        try { m = MqttParser.Parse(topic, payload); }
        catch (FormatException ex) { logger.LogWarning("Dropped invalid payload: {Error}", ex.Message); return; }
        if (m is null) return;
        Log(m);
        Publish(state.Apply(m));
    }

    public void Tick() => Publish(state.Tick());

    void Publish(Core.Occupancy.OccupancyState? changed)
    {
        if (changed is { } s) log.Occupancy(time.GetUtcNow(), s.ToString());
    }

    void Log(Msg m)
    {
        switch (m)
        {
            case EnvMsg e: log.Env(e); break;
            case DoorMsg d: log.Door(d); break;
            case DeviceSetMsg s: log.Command(s); break;
            case AiTransitionMsg t: log.AiEvent(t.Ts, "transition", t.Raw); break;
            case StatusMsg { Source: "ai" } s: log.AiEvent(s.Ts, "status", $"{{\"online\":{(s.Status.Online ? "true" : "false")}}}"); break;
        }
    }
}
