using SmartHomeAI.Core.Alerts;

namespace SmartHomeAI.Hub;

/// <summary>Device ids the user allows to be switched off automatically by an alert (config Alerts:AutoOffAllowed).</summary>
public record AlertWiring(IReadOnlyCollection<string> AutoOffAllowed);

/// <summary>Broker-free core of the Hub: topic+payload in, state and log out.</summary>
public class MessageRouter(HubState state, IEventLog log, TimeProvider time, ILogger<MessageRouter> logger, IMqttPublisher bus, AlertWiring? alertCfg = null)
{
    readonly object _flow = new(); // Handle (MQTT), Tick (timer loop) and the HTTP entry points all run here, one at a time
    readonly HashSet<string> _sent = []; // payloads we published and already logged; their bus echo must not be logged twice

    readonly CommandGate _gate = new(time, state.AutomationOptions);
    readonly Dictionary<string, DateTimeOffset> _lastNote = [];

    readonly IReadOnlyCollection<string> _autoOffAllowed = alertCfg?.AutoOffAllowed ?? [];

    public void Handle(string topic, string payload) { lock (_flow) HandleCore(topic, payload); }

    void HandleCore(string topic, string payload)
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

    public void Tick() { lock (_flow) { Publish(state.Tick()); RunAuto(); RunAlerts(); } }

    /// <summary>User verdict on an alert. False if the id is unknown.</summary>
    public bool Feedback(string id, bool correct) { lock (_flow) return FeedbackCore(id, correct); }

    bool FeedbackCore(string id, bool correct)
    {
        if (!state.AlertFeedback(id, correct)) return false;
        state.Decisions.Add("alert", $"Bạn phản hồi cảnh báo {id}: {(correct ? "đúng" : "sai")}");
        log.AlertFeedback(id, correct);
        return true;
    }

    /// <summary>Manual command from the API. Logged once, when its echo returns on the bus.</summary>
    public bool PublishManual(string id, bool on) => TryPublishManual(id, on, out _);

    /// <summary>coalesced: the command was held (merged with a later one) or dropped as a duplicate instead of published now.</summary>
    public bool TryPublishManual(string id, bool on, out bool coalesced)
    {
        coalesced = false;
        if (id is not ("light" or "fan")) return false;
        lock (_flow) coalesced = Dispatch(id, on, "manual") != GateOutcome.Send;
        return true;
    }

    void RunAuto()
    {
        foreach (var (id, on, source, merged, dup) in _gate.Due(state.IsOn))
        {
            if (dup) Note(id, "bỏ lệnh trùng");
            else
            {
                if (merged > 1) Note(id, $"gộp {merged} lệnh trong {o3()} giây, chỉ gửi lệnh cuối");
                Send(id, on, source);
            }
        }
        foreach (var c in state.Automate())
            SendCommand(c.DeviceId, c.On, "auto");
    }

    string o3() => $"{state.AutomationOptions.MinCommandInterval.TotalSeconds:0.#}";

    // At most one decision line per device per 10 s, so a burst does not spam the log.
    void Note(string id, string text)
    {
        var now = time.GetUtcNow();
        if (_lastNote.TryGetValue(id, out var t) && now - t < TimeSpan.FromSeconds(10)) return;
        _lastNote[id] = now;
        state.Decisions.Add("auto", $"{DecisionText.Cap(DecisionText.DeviceName(id))}: {text}", id);
    }

    GateOutcome Dispatch(string id, bool on, string source)
    {
        var r = _gate.Offer(id, on, source, state.IsOn(id));
        if (r == GateOutcome.Send) Send(id, on, source);
        else if (r == GateOutcome.Duplicate) Note(id, "bỏ lệnh trùng");
        return r;
    }

    void SendCommand(string id, bool on, string source) => Dispatch(id, on, source);

    void Send(string id, bool on, string source)
    {
        var topic = $"home/device/{id}/set";
        var payload = Payload(on, source);
        state.NoteCommand(id, on);
        if (source != "manual") // manual is logged once, when its echo returns
        {
            lock (_sent) _sent.Add(topic + payload);
            log.Command(new DeviceSetMsg(time.GetUtcNow(), id, on, source));
        }
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
                    var left = string.Join(", ", r.DevicesOn.Select(DecisionText.DeviceName));
                    state.Decisions.Add("alert", r.Level == AlertLevel.First
                        ? $"Cảnh báo {r.Id}: bạn đã rời phòng mà {left} vẫn bật"
                        : $"Nhắc lại cảnh báo {r.Id}: {left} vẫn bật");
                    bus.Publish($"home/alert/{r.Id}", $"{{\"ts\":\"{ts}\",\"id\":\"{r.Id}\",\"level\":\"{(r.Level == AlertLevel.First ? "first" : "reminder")}\",\"devices_on\":[{string.Join(",", r.DevicesOn.Select(d => $"\"{d}\""))}]}}");
                    bus.Publish("home/sensor/esp1/buzzer/set", $"{{\"ts\":\"{ts}\",\"on\":true}}");
                    break;
                case CancelAlert c:
                    var reason = c.Reason switch { CancelReason.PersonReturned => "person_returned", CancelReason.DataLost => "data_lost", _ => "devices_off" };
                    log.AlertCancelled(c.Id, now, reason);
                    state.Decisions.Add("alert", $"Hủy cảnh báo {c.Id}: " + c.Reason switch
                    {
                        CancelReason.PersonReturned => "bạn đã quay lại",
                        CancelReason.DataLost => "mất dữ liệu nhận diện",
                        _ => "đèn và quạt đã tắt",
                    });
                    bus.Publish($"home/alert/{c.Id}/cancel", $"{{\"ts\":\"{ts}\",\"id\":\"{c.Id}\",\"reason\":\"{reason}\"}}");
                    bus.Publish("home/sensor/esp1/buzzer/set", $"{{\"ts\":\"{ts}\",\"on\":false}}");
                    break;
                case AutoOff o:
                    foreach (var d in o.DeviceIds)
                    {
                        state.Decisions.Add("alert", $"Tự tắt {DecisionText.DeviceName(d)}: bạn rời phòng quá lâu mà vẫn bật", d, false);
                        SendCommand(d, false, "alert");
                    }
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
