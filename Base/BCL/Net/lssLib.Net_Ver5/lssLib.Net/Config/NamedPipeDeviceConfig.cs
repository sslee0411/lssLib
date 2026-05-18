namespace lssLib.Net;

/// <summary>Named Pipe 통신 장비 설정.</summary>
/// <example><code>
/// // 동일 PC
/// var cfg = new NamedPipeDeviceConfig(5, "Pipe-IPC", ".", "lssLib-control");
/// // cfg.SequenceMode == 1 (Sequential) ← 기본값
///
/// // 네트워크 파이프 (Windows 전용)
/// var cfg2 = new NamedPipeDeviceConfig(6, "Net-Pipe", "192.168.1.10", "lssLib-ctrl");
/// </code></example>
public sealed class NamedPipeDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.NamedPipe;

    public string ServerName { get; set; }
    public string PipeName { get; set; }
    public int ConnectTimeoutMs { get; set; } = 5000;

    public NamedPipeDeviceConfig(int deviceId, string deviceName,
        string serverName, string pipeName)
        : base(deviceId, deviceName)
    {
        ServerName = serverName;
        PipeName = pipeName;
        RetryDelay = TimeSpan.FromSeconds(1);
        ReconnectBackoff = false;
        SequenceMode = NetDeviceConfig.SequenceModes.Sequential;  // 1: 단일 순차
        RequestTimeout = TimeSpan.FromSeconds(2);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | \\\\{ServerName}\\pipe\\{PipeName}";
}
