using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

namespace SmartHomeAI.Hub.Tests;

class FakeBus : IMqttPublisher
{
    public List<(string Topic, string Payload)> Sent = [];
    public void Publish(string topic, string payload) => Sent.Add((topic, payload));
}

class RecLog : IEventLog
{
    public List<string> Rows = [];
    public void Env(EnvMsg m) => Rows.Add("env");
    public void Door(DoorMsg m) => Rows.Add("door");
    public void Command(DeviceSetMsg m) => Rows.Add("cmd");
    public void AiEvent(DateTimeOffset ts, string kind, string json) => Rows.Add("ai:" + kind);
    public void Occupancy(DateTimeOffset ts, string state) => Rows.Add("occ:" + state);
    public void AlertRaised(string id, DateTimeOffset ts) => Rows.Add("alert+" + id);
    public void AlertCancelled(string id, DateTimeOffset ts, string reason) => Rows.Add("alert-" + id + ":" + reason);
    public void AlertFeedback(string id, bool correct) => Rows.Add("fb:" + id + ":" + correct);
}

public class ParserTests
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";

    [Fact]
    public void AiState_ok()
    {
        var m = Assert.IsType<AiStateMsg>(MqttParser.Parse("home/ai/livingroom/state",
            "{" + Ts + ",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":3,\"zone\":\"sofa\",\"posture\":\"lying\",\"conf\":0.8}]}"));
        Assert.True(m.State.CameraOk);
        Assert.Equal("sofa", m.State.Persons[0].Zone);
    }

    [Fact]
    public void Transition_status_door_env_device_ok()
    {
        var t = Assert.IsType<AiTransitionMsg>(MqttParser.Parse("home/ai/livingroom/transition",
            "{" + Ts + ",\"track_id\":3,\"from_zone\":\"livingroom\",\"to_zone\":\"door\",\"direction\":\"out\",\"conf\":0.7}"));
        Assert.Equal(Direction.Out, t.Transition.Direction);
        Assert.False(Assert.IsType<StatusMsg>(MqttParser.Parse("home/ai/status", "{" + Ts + ",\"online\":false}")).Status.Online);
        Assert.True(Assert.IsType<DoorMsg>(MqttParser.Parse("home/sensor/esp1/door", "{" + Ts + ",\"open\":true}")).Door.Open);
        Assert.Equal(18, Assert.IsType<EnvMsg>(MqttParser.Parse("home/sensor/esp1/env", "{" + Ts + ",\"temp_c\":31.4,\"humidity\":62,\"lux\":18}")).Lux);
        Assert.Equal("manual", Assert.IsType<DeviceSetMsg>(MqttParser.Parse("home/device/fan/set", "{" + Ts + ",\"on\":true,\"source\":\"manual\"}")).Source);
    }

    [Theory]
    [InlineData("home/ai/livingroom/state", "not json")]
    [InlineData("home/ai/livingroom/state", "{\"ts\":\"2026-11-14T12:00:00Z\",\"seq\":1,\"camera_ok\":true}")]
    [InlineData("home/ai/livingroom/state", "{\"ts\":\"2026-11-14T12:00:00Z\",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":1,\"zone\":\"kitchen\",\"posture\":\"lying\",\"conf\":1}]}")]
    [InlineData("home/sensor/esp1/env", "{\"ts\":\"2026-11-14T12:00:00Z\",\"temp_c\":\"hot\",\"humidity\":50,\"lux\":1}")]
    [InlineData("home/sensor/esp1/env", "{\"ts\":\"2026-11-14T12:00:00Z\",\"temp_c\":20,\"humidity\":150,\"lux\":1}")]
    [InlineData("home/sensor/esp1/door", "{\"ts\":\"yesterday\",\"open\":true}")]
    [InlineData("home/device/fan/set", "{\"ts\":\"2026-11-14T12:00:00Z\",\"on\":true,\"source\":\"god\"}")]
    public void Invalid_payload_throws(string topic, string payload) =>
        Assert.Throws<FormatException>(() => MqttParser.Parse(topic, payload));

    [Fact]
    public void Unknown_topic_is_null() => Assert.Null(MqttParser.Parse("home/other/x", "{}"));
}

public class RouterTests
{
    [Fact]
    public void Routes_to_engine_logs_and_survives_garbage()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
        var log = new RecLog();
        var state = new HubState(new OccupancyEngine(time), new AutomationEngine(time), new AlertManager(time));
        var r = new MessageRouter(state, log, time, NullLogger<MessageRouter>.Instance, new FakeBus());
        const string ts = "\"ts\":\"2026-11-14T12:00:00Z\"";

        r.Handle("home/ai/status", "{" + ts + ",\"online\":true}");
        r.Handle("home/ai/livingroom/state", "garbage");
        r.Handle("home/ai/livingroom/state", "{" + ts + ",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":1,\"zone\":\"sofa\",\"posture\":\"lying\",\"conf\":0.9}]}");
        r.Handle("home/sensor/esp1/env", "{" + ts + ",\"temp_c\":31,\"humidity\":60,\"lux\":10}");
        r.Handle("home/device/fan/state", "{" + ts + ",\"on\":true}");
        r.Handle("home/device/fan/set", "{" + ts + ",\"on\":true,\"source\":\"auto\"}");

        Assert.Contains("occ:Occupied", log.Rows);
        Assert.Contains("env", log.Rows);
        Assert.Contains("cmd", log.Rows);
        Assert.Contains("ai:status", log.Rows);
        var json = System.Text.Json.JsonSerializer.Serialize(state.Snapshot());
        Assert.Contains("\"occupancy\":\"Occupied\"", json);
        Assert.Contains("\"ai\":true", json);
        Assert.Contains("\"temp_c\"".Replace("_c", "C"), json, StringComparison.OrdinalIgnoreCase);

        time.Advance(TimeSpan.FromSeconds(20)); // stale -> Unknown, detected by Tick
        r.Tick();
        Assert.Equal("occ:Unknown", log.Rows[^1]);
    }
}

public class AutomationWiringTests
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";
    readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    readonly FakeBus bus = new();
    readonly RecLog log = new();
    readonly HubState state;
    readonly MessageRouter r;

    public AutomationWiringTests()
    {
        state = new HubState(new OccupancyEngine(time), new AutomationEngine(time), new AlertManager(time));
        r = new MessageRouter(state, log, time, NullLogger<MessageRouter>.Instance, bus);
        r.Handle("home/ai/status", "{" + Ts + ",\"online\":true}");
        r.Handle("home/device/light/state", "{" + Ts + ",\"on\":false}");
        r.Handle("home/sensor/esp1/env", "{" + Ts + ",\"temp_c\":25,\"humidity\":60,\"lux\":5}");
    }

    void Occupied() => r.Handle("home/ai/livingroom/state", "{" + Ts + ",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":1,\"zone\":\"livingroom\",\"posture\":\"standing\",\"conf\":0.9}]}");

    [Fact]
    public void Dark_and_occupied_turns_light_on()
    {
        Occupied();
        var (topic, payload) = Assert.Single(bus.Sent);
        Assert.Equal("home/device/light/set", topic);
        Assert.Contains("\"on\":true", payload);
        Assert.Contains("\"source\":\"auto\"", payload);
        Assert.Equal(1, log.Rows.Count(x => x == "cmd"));
        r.Handle(topic, payload); // bus echo: not logged again, not a manual command
        Assert.Equal(1, log.Rows.Count(x => x == "cmd"));
        time.Advance(TimeSpan.FromMinutes(5));
        Occupied(); // keep AI data fresh
        Assert.Equal(2, bus.Sent.Count); // own auto command did not block the next auto
    }

    [Fact]
    public void Manual_mode_publishes_nothing()
    {
        state.Auto = false;
        Occupied();
        r.Tick();
        Assert.Empty(bus.Sent);
        Assert.Contains("\"mode\":\"manual\"", System.Text.Json.JsonSerializer.Serialize(state.Snapshot()));
    }

    [Fact]
    public void Manual_command_blocks_auto_SC09()
    {
        r.Handle("home/device/light/set", "{" + Ts + ",\"on\":false,\"source\":\"manual\"}");
        Occupied();
        Assert.Empty(bus.Sent);
    }

    [Fact]
    public void PublishManual_validates_id_and_logs_on_echo_only()
    {
        Assert.False(r.PublishManual("tv", true));
        Assert.True(r.PublishManual("fan", true));
        var (topic, payload) = Assert.Single(bus.Sent);
        Assert.Contains("\"source\":\"manual\"", payload);
        Assert.DoesNotContain("cmd", log.Rows);
        r.Handle(topic, payload);
        Assert.Contains("cmd", log.Rows);
    }
}

public class AlertWiringTests
{
    const string Ts = "\"ts\":\"2026-11-14T12:00:00Z\"";
    readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));
    readonly FakeBus bus = new();
    readonly RecLog log = new();

    MessageRouter Make(AlertOptions? o = null, AlertWiring? w = null)
    {
        var r = new MessageRouter(new HubState(new OccupancyEngine(time), new AutomationEngine(time), new AlertManager(time, o)),
            log, time, NullLogger<MessageRouter>.Instance, bus, w);
        r.Handle("home/ai/status", "{" + Ts + ",\"online\":true}");
        r.Handle("home/device/light/state", "{" + Ts + ",\"on\":true}");
        r.Handle("home/device/fan/state", "{" + Ts + ",\"on\":true}");
        return r;
    }

    // Person walks out the door, then nobody is seen: ExitPending, then AwayConfirmed after VerifyAway.
    void Away(MessageRouter r)
    {
        r.Handle("home/ai/livingroom/state", "{" + Ts + ",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":1,\"zone\":\"door\",\"posture\":\"moving\",\"conf\":0.9}]}");
        r.Handle("home/ai/livingroom/transition", "{" + Ts + ",\"track_id\":1,\"from_zone\":\"livingroom\",\"to_zone\":\"door\",\"direction\":\"out\",\"conf\":0.9}");
        r.Handle("home/ai/livingroom/state", "{" + Ts + ",\"seq\":2,\"camera_ok\":true,\"persons\":[]}");
    }

    // Advances in 1 s ticks, keeping AI data fresh with empty-room frames.
    void Run(MessageRouter r, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            r.Handle("home/ai/livingroom/state", "{" + Ts + ",\"seq\":3,\"camera_ok\":true,\"persons\":[]}");
            r.Tick();
        }
    }

    [Fact]
    public void Away_raises_then_reminds_then_return_cancels_and_buzzer_off()
    {
        var r = Make();
        Away(r);
        Run(r, 200);
        var first = bus.Sent.Where(s => s.Topic == "home/alert/alert-1").ToList();
        Assert.Single(first);
        Assert.Contains("\"level\":\"first\"", first[0].Payload);
        Assert.Contains("\"devices_on\":[\"light\",\"fan\"]", first[0].Payload);
        Assert.Contains("\"on\":true", bus.Sent.Last(s => s.Topic == "home/sensor/esp1/buzzer/set").Payload);
        Assert.Contains("alert+alert-1", log.Rows);

        Run(r, 320);
        Assert.Contains("\"level\":\"reminder\"", bus.Sent.Last(s => s.Topic == "home/alert/alert-1").Payload);

        r.Handle("home/ai/livingroom/state", "{" + Ts + ",\"seq\":9,\"camera_ok\":true,\"persons\":[{\"track_id\":2,\"zone\":\"livingroom\",\"posture\":\"standing\",\"conf\":0.9}]}");
        Assert.Contains(bus.Sent, s => s.Topic == "home/alert/alert-1/cancel" && s.Payload.Contains("person_returned"));
        Assert.Contains("\"on\":false", bus.Sent.Last(s => s.Topic == "home/sensor/esp1/buzzer/set").Payload);
        Assert.Contains("alert-alert-1:person_returned", log.Rows);
    }

    [Fact]
    public void Data_lost_cancels_alert_with_data_lost_reason()
    {
        var r = Make();
        Away(r);
        Run(r, 200);
        time.Advance(TimeSpan.FromSeconds(20)); // AI frames stop -> Unknown
        r.Tick();
        Assert.Contains(bus.Sent, s => s.Topic == "home/alert/alert-1/cancel" && s.Payload.Contains("data_lost"));
        Assert.Contains("alert-alert-1:data_lost", log.Rows);
    }

    [Fact]
    public void Feedback_known_id_ok_unknown_false()
    {
        var r = Make();
        Away(r);
        Run(r, 200);
        Assert.True(r.Feedback("alert-1", true));
        Assert.Contains("fb:alert-1:True", log.Rows);
        Assert.False(r.Feedback("alert-99", true));
    }

    [Fact]
    public void AutoOff_disabled_by_default_sends_nothing()
    {
        var r = Make(w: new AlertWiring(["light", "fan"]));
        Away(r);
        Run(r, 700);
        Assert.DoesNotContain(bus.Sent, s => s.Payload.Contains("\"source\":\"alert\""));
    }

    [Fact]
    public void AutoOff_enabled_turns_off_only_allowed_devices()
    {
        var r = Make(new AlertOptions { AutoOffEnabled = true }, new AlertWiring(["light"]));
        Away(r);
        Run(r, 700);
        var cmd = Assert.Single(bus.Sent, s => s.Payload.Contains("\"source\":\"alert\""));
        Assert.Equal("home/device/light/set", cmd.Topic);
        Assert.Contains("\"on\":false", cmd.Payload);
    }
}

public class ConfigTests
{
    [Fact]
    public void Occupancy_defaults_when_section_missing()
    {
        var o = HubConfig.Occupancy(new ConfigurationBuilder().Build());
        Assert.Equal(TimeSpan.FromSeconds(30), o.VerifyAway);
        Assert.Equal(TimeSpan.FromSeconds(10), o.StateStale);
    }

    [Fact]
    public void Occupancy_binds_section()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Occupancy:VerifyAway"] = "00:00:05",
            ["Occupancy:StateStale"] = "00:00:03",
        }).Build();
        var o = HubConfig.Occupancy(cfg);
        Assert.Equal(TimeSpan.FromSeconds(5), o.VerifyAway);
        Assert.Equal(TimeSpan.FromSeconds(3), o.StateStale);
        Assert.Equal(["livingroom", "sofa"], o.RoomZones);
    }
}

public class ApiKeyTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", "anything", true)]
    [InlineData("k", "k", true)]
    [InlineData("k", "wrong", false)]
    [InlineData("k", null, false)]
    [InlineData("k", "", false)]
    public void Allowed(string? configured, string? supplied, bool ok) => Assert.Equal(ok, ApiKey.Allowed(configured, supplied));
}
