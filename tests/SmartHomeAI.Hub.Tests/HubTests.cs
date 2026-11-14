using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
        var state = new HubState(new OccupancyEngine(time), new AutomationEngine(time));
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
        state = new HubState(new OccupancyEngine(time), new AutomationEngine(time));
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
