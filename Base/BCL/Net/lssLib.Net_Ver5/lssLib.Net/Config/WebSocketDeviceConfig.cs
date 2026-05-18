namespace lssLib.Net;

/// <summary>WebSocket 통신 장비 설정.</summary>
/// <example><code>
/// var cfg = new WebSocketDeviceConfig(7, "WS-Monitor", "ws://192.168.1.10:8080/ws");
/// // cfg.SequenceMode == 0 (Parallel) ← 기본값
/// </code></example>
public sealed class WebSocketDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.WebSocket;

    public string Url { get; set; }
    public string? SubProtocol { get; set; }
    public int ReceiveBufferSize { get; set; } = 8192;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public WebSocketDeviceConfig(int deviceId, string deviceName, string url)
        : base(deviceId, deviceName)
    {
        Url = url;
        RetryDelay = TimeSpan.FromSeconds(2);
        ReconnectBackoff = true;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(5);
    }

    public override string ToString()
        => base.ToString() + $" | {Url}";
}