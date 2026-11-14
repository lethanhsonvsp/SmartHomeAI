using MQTTnet;

namespace SmartHomeAI.Hub;

public class MqttService(MessageRouter router, IConfiguration cfg, ILogger<MqttService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var host = cfg["Mqtt:Host"] ?? "localhost";
        var port = cfg.GetValue("Mqtt:Port", 1883);
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            try { router.Handle(e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString() ?? ""); }
            catch (Exception ex) { logger.LogError(ex, "Handler failed for {Topic}", e.ApplicationMessage.Topic); }
            return Task.CompletedTask;
        };
        var opts = new MqttClientOptionsBuilder().WithTcpServer(host, port).WithClientId("smarthome-hub").Build();
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
