using MQTTnet;

namespace SmartHomeAI.Hub;

public class MqttService(MessageRouter router, MqttBus bus, IConfiguration cfg, ILogger<MqttService> logger, TapoP100Adapter tapo) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var host = cfg["Mqtt:Host"] ?? "localhost";
        var port = cfg.GetValue("Mqtt:Port", 1883);
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                var payload = e.ApplicationMessage.ConvertPayloadToString() ?? "";
                router.Handle(e.ApplicationMessage.Topic, payload);
                tapo.Receive(e.ApplicationMessage.Topic, payload, e.ApplicationMessage.Retain);
            }
            catch (Exception ex) { logger.LogError(ex, "Handler failed for {Topic}", e.ApplicationMessage.Topic); }
            return Task.CompletedTask;
        };
        bus.Client = client;
        var ob = new MqttClientOptionsBuilder().WithTcpServer(host, port).WithClientId("smarthome-hub");
        var user = cfg["Mqtt:User"];
        if (!string.IsNullOrEmpty(user)) ob.WithCredentials(user, cfg["Mqtt:Password"] ?? "");
        var opts = ob.Build();
        var sub = new MqttClientSubscribeOptionsBuilder().WithTopicFilter("home/#", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).Build();

        while (!ct.IsCancellationRequested)
        {
            if (!client.IsConnected)
                try
                {
                    await client.ConnectAsync(opts, ct);
                    await client.SubscribeAsync(sub, ct);
                    logger.LogInformation("MQTT connected to {Host}:{Port}", host, port);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("MQTT connect failed ({Error}), retrying", ex.Message);
                }
            router.Tick();
            try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { }
        }
    }
}
