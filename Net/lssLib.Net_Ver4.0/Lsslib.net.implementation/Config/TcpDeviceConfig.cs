// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/TcpDeviceConfig.cs
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// TCP 통신 장비 설정.
/// </summary>
/// <example><code>
/// var cfg = new TcpDeviceConfig(1, "PLC-01", "192.168.1.10", 502)
/// {
///     IsRetryEnabled    = true,
///     RetryTarget       = RetryTarget.Connect | RetryTarget.Write,
///     MaxRetries        = 5,
///     ReconnectBackoff  = true,
///     IsSequential      = false,
///     PeriodicInterval  = TimeSpan.FromMilliseconds(100),
///     HeartbeatInterval = TimeSpan.FromSeconds(30)
/// };
/// cfg.AddReadCommand(modbusReadFrame);
///
/// await using var channel = new RequestResponseChannel(
///     cfg, TcpTransport.FromConfig(cfg), new BinaryProtocol(), autoRegister: true);
/// </code></example>
public sealed class TcpDeviceConfig : NetDeviceConfigBase
{
    /// <inheritdoc/>
    public override NetTransportType TransportType => NetTransportType.Tcp;

    /// <summary>서버 호스트명 또는 IP 주소.</summary>
    public string Host { get; set; }

    /// <summary>서버 포트 번호.</summary>
    public int Port { get; set; }

    /// <summary>TCP 연결 타임아웃. 기본값: 5초.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TcpDeviceConfig(int deviceId, string deviceName, string host, int port)
        : base(deviceId, deviceName)
    {
        Host = host;
        Port = port;
        // TCP 환경 기본값
        RetryDelay = TimeSpan.FromSeconds(2);
        ReconnectBackoff = true;
        IsSequential = false;
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(3);
    }

    /// <inheritdoc/>
    public override string ToString()
        => base.ToString() + $" | {Host}:{Port}";
}