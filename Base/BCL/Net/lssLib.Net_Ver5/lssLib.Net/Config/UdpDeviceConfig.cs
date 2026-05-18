// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/UdpDeviceConfig.cs  [v5.1]
//  SequenceMode = 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>UDP 통신 장비 설정.</summary>
public sealed class UdpDeviceConfig : NetDeviceConfig
{
    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public override NetTransportType TransportType => NetTransportType.Udp;

    /// <summary>
    /// IP
    /// </summary>
    public string RemoteHost { get; set; }

    /// <summary>
    /// 포트 번호. 일반적으로 0~65535 범위의 값을 사용합니다. 예: 502 (Modbus TCP), 5025 (SCPI), 80 (HTTP) 등.
    /// </summary>
    public int RemotePort { get; set; }

    /// <summary>
    /// 로컬 포트 번호. 0으로 설정하면 시스템이 사용 가능한 포트를 자동으로 할당합니다. 특정 포트를 사용해야 하는 경우 해당 포트 번호를 지정할 수 있습니다.
    /// </summary>
    public int LocalPort { get; set; } = 0;
    public UdpDeviceConfig(int deviceId, string deviceName, string remoteHost, int remotePort)
        : base(deviceId, deviceName)
    {
        RemoteHost = remoteHost;
        RemotePort = remotePort;
        IsRetryEnabled = false;
        RetryTarget = RetryTarget.None;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        RequestTimeout = TimeSpan.FromMilliseconds(500);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | {RemoteHost}:{RemotePort} (local:{LocalPort})";
}
