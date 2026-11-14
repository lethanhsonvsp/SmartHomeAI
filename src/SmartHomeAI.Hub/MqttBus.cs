using MQTTnet;

namespace SmartHomeAI.Hub;

public interface IMqttPublisher { void Publish(string topic, string payload); }

/// <summary>Publishes through the client owned by MqttService; drops (with a warning) while disconnected.</summary>
public class MqttBus(ILogger<MqttBus> logger) : IMqttPublisher
{
    public IMqttClient? Client { get; set; }

    public void Publish(string topic, string payload)
    {
        var c = Client;
        if (c is null || !c.IsConnected) { logger.LogWarning("MQTT not connected, dropped {Topic}", topic); return; }
        var msg = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).Build();
        _ = c.PublishAsync(msg).ContinueWith(t => logger.LogWarning("MQTT publish failed: {Error}", t.Exception?.GetBaseException().Message), TaskContinuationOptions.OnlyOnFaulted);
    }
}
