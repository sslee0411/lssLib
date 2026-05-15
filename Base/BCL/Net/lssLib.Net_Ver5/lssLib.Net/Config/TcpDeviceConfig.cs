// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/TcpDeviceConfig.cs  [v5.1]
//  SequenceMode = 0 (Parallel) — TCP 기본값
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// TCP 통신 장비 설정.
/// </summary>
/// <example><code>
/// // 기본 (병렬)
/// var cfg = new TcpDeviceConfig(1, "PLC-01", "192.168.1.10", 502);
/// // cfg.SequenceMode == 0 (Parallel) ← 기본값
///
/// // 슬라이딩 윈도우 3개 동시
/// var cfg2 = new TcpDeviceConfig(2, "Multi-PLC", "192.168.1.20", 502)
/// { SequenceMode = 3 };
///
/// // Modbus TCP — 단일 순차
/// var cfg3 = new TcpDeviceConfig(3, "Modbus-TCP", "192.168.1.30", 502)
/// { SequenceMode = NetDeviceConfig.SequenceModes.Sequential };
/// </code></example>
public sealed class TcpDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Tcp;

    public string Host { get; set; }
    public int Port { get; set; }
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TcpDeviceConfig(int deviceId, string deviceName, string host, int port)
        : base(deviceId, deviceName)
    {
        Host = host;
        Port = port;
        RetryDelay = TimeSpan.FromSeconds(2);
        ReconnectBackoff = true;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;    // 0: 병렬
        HeartbeatInterval = TimeSpan.FromSeconds(30);
        RequestTimeout = TimeSpan.FromSeconds(3);
    }

    public override string ToString()
        => base.ToString() + $" | {Host}:{Port}";
}