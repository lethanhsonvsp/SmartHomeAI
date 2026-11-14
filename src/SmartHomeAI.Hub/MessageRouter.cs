namespace SmartHomeAI.Hub;

/// <summary>Broker-free core of the Hub: topic+payload in, state and log out.</summary>
public class MessageRouter(HubState state, IEventLog log, TimeProvider time, ILogger<MessageRouter> logger, IMqttPublisher bus)
{
    readonly HashSet<string> _sent = []; // payloads we published and already logged; their bus echo must not be logged twice

    public void Handle(string topic, string payload)
    {
        Msg? m;
        try { m = MqttParser.Parse(topic, payload); }
        catch (FormatException ex) { logger.LogWarning("Dropped invalid payload: {Error}", ex.Message); return; }
        if (m is null) return;
        if (!(m is DeviceSetMsg && TakeSent(topic, payload))) Log(m);
        Publish(state.Apply(m));
        RunAuto();
    }

    public void Tick() { Publish(state.Tick()); RunAuto(); }

    /// <summary>Manual command from the API. Logged once, when its echo returns on the bus.</summary>
    public bool PublishManual(string id, bool on)
    {
        if (id is not ("light" or "fan")) return false;
        bus.Publish($"home/device/{id}/set", Payload(on, "manual"));
        return true;
    }

    void RunAuto()
    {
        foreach (var c in state.Automate())
        {
            var topic = $"home/device/{c.DeviceId}/set";
            var payload = Payload(c.On, "auto");
            lock (_sent) _sent.Add(topic + payload);
            log.Command(new DeviceSetMsg(time.GetUtcNow(), c.DeviceId, c.On, "auto"));
            bus.Publish(topic, payload);
        }
    }

    bool TakeSent(string topic, string payload) { lock (_sent) return _sent.Remove(topic + payload); }

    string Payload(bool on, string source) =>
        $"{{\"ts\":\"{time.GetUtcNow():yyyy-MM-ddTHH:mm:ssZ}\",\"on\":{(on ? "true" : "false")},\"source\":\"{source}\"}}";

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
