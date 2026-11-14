using SmartHomeAI.Core.Alerts;

namespace SmartHomeAI.Hub;

/// <summary>Device ids the user allows to be switched off automatically by an alert (config Alerts:AutoOffAllowed).</summary>
public record AlertWiring(IReadOnlyCollection<string> AutoOffAllowed);

/// <summary>Broker-free core of the Hub: topic+payload in, state and log out.</summary>
public class MessageRouter(HubState state, IEventLog log, TimeProvider time, ILogger<MessageRouter> logger, IMqttPublisher bus, AlertWiring? alertCfg = null)
{
    readonly HashSet<string> _sent = []; // payloads we published and already logged; their bus echo must not be logged twice

    readonly IReadOnlyCollection<string> _autoOffAllowed = alertCfg?.AutoOffAllowed ?? [];

    public void Handle(string topic, string payload)
    {
        Msg? m;
        try { m = MqttParser.Parse(topic, payload); }
        catch (FormatException ex) { logger.LogWarning("Dropped invalid payload: {Error}", ex.Message); return; }
        if (m is null) return;
        if (!(m is DeviceSetMsg && TakeSent(topic, payload))) Log(m);
        Publish(state.Apply(m));
        RunAuto();
        RunAlerts();
    }

    public void Tick() { Publish(state.Tick()); RunAuto(); RunAlerts(); }

    /// <summary>User verdict on an alert. False if the id is unknown.</summary>
    public bool Feedback(string id, bool correct)
    {
        if (!state.AlertFeedback(id, correct)) return false;
        log.AlertFeedback(id, correct);
        return true;
    }

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
            SendCommand(c.DeviceId, c.On, "auto");
    }

    void SendCommand(string id, bool on, string source)
    {
        var topic = $"home/device/{id}/set";
        var payload = Payload(on, source);
        lock (_sent) _sent.Add(topic + payload);
        log.Command(new DeviceSetMsg(time.GetUtcNow(), id, on, source));
        bus.Publish(topic, payload);
    }

    // home/alert/{id} {ts,id,level,devices_on}; home/alert/{id}/cancel {ts,id,reason}; home/sensor/esp1/buzzer/set {ts,on}
    void RunAlerts()
    {
        foreach (var a in state.EvaluateAlerts(_autoOffAllowed))
        {
            var now = time.GetUtcNow();
            var ts = $"{now:yyyy-MM-ddTHH:mm:ssZ}";
            switch (a)
            {
                case RaiseAlert r:
                    if (r.Level == AlertLevel.First) log.AlertRaised(r.Id, now);
                    bus.Publish($"home/alert/{r.Id}", $"{{\"ts\":\"{ts}\",\"id\":\"{r.Id}\",\"level\":\"{(r.Level == AlertLevel.First ? "first" : "reminder")}\",\"devices_on\":[{string.Join(",", r.DevicesOn.Select(d => $"\"{d}\""))}]}}");
                    bus.Publish("home/sensor/esp1/buzzer/set", $"{{\"ts\":\"{ts}\",\"on\":true}}");
                    break;
                case CancelAlert c:
                    var reason = c.Reason switch { CancelReason.PersonReturned => "person_returned", CancelReason.DataLost => "data_lost", _ => "devices_off" };
                    log.AlertCancelled(c.Id, now, reason);
                    bus.Publish($"home/alert/{c.Id}/cancel", $"{{\"ts\":\"{ts}\",\"id\":\"{c.Id}\",\"reason\":\"{reason}\"}}");
                    bus.Publish("home/sensor/esp1/buzzer/set", $"{{\"ts\":\"{ts}\",\"on\":false}}");
                    break;
                case AutoOff o:
                    foreach (var d in o.DeviceIds) SendCommand(d, false, "alert");
                    break;
            }
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
