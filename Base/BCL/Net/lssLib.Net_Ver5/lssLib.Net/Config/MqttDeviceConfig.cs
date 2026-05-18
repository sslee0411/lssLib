
namespace lssLib.Net;

/// <summary>MQTT 통신 장비 설정.</summary>
/// <example><code>
/// var cfg = new MqttDeviceConfig(8, "IoT-Sensor", "192.168.1.100", 1883)
/// {
///     ClientId       = "lssLib-Client-01",
///     SubscribeTopic = "factory/+/sensor/#",
///     PublishTopic   = "factory/line1/command",
///     QoS            = 1
///     // SequenceMode = 0 (Parallel) ← 기본값
/// };
/// </code></example>
public sealed class MqttDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Mqtt;

    public string BrokerHost { get; set; }
    public int BrokerPort { get; set; } = 1883;
    public string? ClientId { get; set; }
    public string? SubscribeTopic { get; set; }
    public string PublishTopic { get; set; } = "lssLib/data";
    public byte QoS { get; set; } = 1;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public ushort KeepAliveSeconds { get; set; } = 60;

    public MqttDeviceConfig(int deviceId, string deviceName,
        string brokerHost, int brokerPort = 1883)
        : base(deviceId, deviceName)
    {
        BrokerHost = brokerHost;
        BrokerPort = brokerPort;
        RetryDelay = TimeSpan.FromSeconds(3);
        ReconnectBackoff = true;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(5);
    }

    public override string ToString()
        => base.ToString() + $" | {BrokerHost}:{BrokerPort} [{SubscribeTopic ?? "-"}]";
}