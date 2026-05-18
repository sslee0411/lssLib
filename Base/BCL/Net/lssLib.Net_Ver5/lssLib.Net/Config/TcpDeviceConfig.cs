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
    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public override NetTransportType TransportType => NetTransportType.Tcp;

    /// <summary>
    /// IP 주소
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 포트 번호. 일반적으로 0~65535 범위의 값을 사용합니다. 예: 502 (Modbus TCP), 5025 (SCPI), 80 (HTTP) 등.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// TCP 연결 시 타임아웃 시간. 기본값은 5초입니다. 이 시간 내에 연결이 수립되지 않으면 연결 시도가 실패로 간주됩니다.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 생성자. deviceId와 deviceName은 NetDeviceConfig의 필수 매개변수입니다. host와 port는 TCP 통신을 위해 반드시 필요합니다.
    /// </summary>
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