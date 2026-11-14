using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using SmartHomeAI.Core.Alerts;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;
using SmartHomeAI.Hub;

namespace SmartHomeAI.Hub.Tests;

public class HistoryTests
{
    readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-11-14T12:00:00Z"));

    HubState State(HistoryStore h) => new(new OccupancyEngine(time), new AutomationEngine(time, TestOpts.NoWait), new AlertManager(time), h);

    [Fact]
    public void Env_occupancy_persons_recorded_and_json_contract()
    {
        var h = new HistoryStore(time);
        var s = State(h);
        s.Apply(MqttParser.Parse("home/sensor/esp1/env", "{\"ts\":\"2026-11-14T12:00:00Z\",\"temp_c\":31.4,\"humidity\":62,\"lux\":18}")!);
        s.Apply(MqttParser.Parse("home/ai/status", "{\"ts\":\"2026-11-14T12:00:00Z\",\"online\":true}")!);
        s.Apply(MqttParser.Parse("home/ai/livingroom/state", "{\"ts\":\"2026-11-14T12:00:00Z\",\"seq\":1,\"camera_ok\":true,\"persons\":[{\"track_id\":1,\"zone\":\"sofa\",\"posture\":\"lying\",\"conf\":0.9}]}")!);
        s.Tick();
        var json = JsonSerializer.Serialize(h.Query(60), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"env\":[{\"ts\":\"2026-11-14T12:00:00Z\"", json);
        Assert.Contains("\"tempC\":31.4,\"humidity\":62,\"lux\":18", json);
        Assert.Contains("{\"ts\":\"2026-11-14T12:00:00Z\",\"state\":\"Occupied\"}", json);
        Assert.Contains("\"persons\":[{\"ts\":\"2026-11-14T12:00:00Z\",\"count\":1}]", json);
    }

    [Fact]
    public void Persons_sampled_every_10s_only()
    {
        var h = new HistoryStore(time);
        h.SamplePersons(1); time.Advance(TimeSpan.FromSeconds(5)); h.SamplePersons(2);
        time.Advance(TimeSpan.FromSeconds(5)); h.SamplePersons(3);
        Assert.Equal([1, 3], h.Query(60).Persons.Select(p => p.Count));
    }

    [Fact]
    public void Window_cuts_old_and_sorts_ascending()
    {
        var h = new HistoryStore(time);
        h.AddEnv(new(time.GetUtcNow().AddMinutes(-30).UtcDateTime, 1, 1, 1));
        h.AddEnv(new(time.GetUtcNow().AddMinutes(-90).UtcDateTime, 2, 2, 2));
        h.AddEnv(new(time.GetUtcNow().AddMinutes(-10).UtcDateTime, 3, 3, 3));
        Assert.Equal([1.0, 3.0], h.Query(60).Env.Select(e => e.TempC));
        Assert.Equal(3, h.Query(360).Env.Count);
        Assert.Equal([2.0, 1.0, 3.0], h.Query(360).Env.Select(e => e.TempC));
    }

    [Fact]
    public void Capacity_and_age_limits()
    {
        var h = new HistoryStore(time, maxSamples: 3);
        for (var i = 0; i < 5; i++) { h.AddEnv(new(time.GetUtcNow().UtcDateTime, i, 0, 0)); time.Advance(TimeSpan.FromSeconds(1)); }
        Assert.Equal([2.0, 3.0, 4.0], h.Query(60).Env.Select(e => e.TempC));
        time.Advance(TimeSpan.FromHours(7));
        h.AddEnv(new(time.GetUtcNow().UtcDateTime, 9, 0, 0));
        Assert.Single(h.Query(360).Env);
    }

    [Theory]
    [InlineData(null, 60)] [InlineData("15", 15)] [InlineData("9999", 360)]
    [InlineData("0", null)] [InlineData("-5", null)] [InlineData("abc", null)] [InlineData("", null)]
    public void ParseMinutes(string? raw, int? expected) => Assert.Equal(expected, HistoryStore.ParseMinutes(raw));
}
