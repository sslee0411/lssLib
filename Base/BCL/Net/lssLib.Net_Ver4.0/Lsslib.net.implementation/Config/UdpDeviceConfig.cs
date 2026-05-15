// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/UdpDeviceConfig.cs
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>UDP 통신 장비 설정.</summary>
public sealed class UdpDeviceConfig : NetDeviceConfigBase
{
    /// <inheritdoc/>
    public override NetTransportType TransportType => NetTransportType.Udp;

    /// <summary>원격 호스트 IP 또는 브로드캐스트 주소.</summary>
    public string RemoteHost { get; set; }

    /// <summary>원격 포트 번호.</summary>
    public int RemotePort { get; set; }

    /// <summary>로컬 수신 포트. 0=OS 자동 할당.</summary>
    public int LocalPort { get; set; } = 0;

    public UdpDeviceConfig(int deviceId, string deviceName, string remoteHost, int remotePort)
        : base(deviceId, deviceName)
    {
        RemoteHost = remoteHost;
        RemotePort = remotePort;
        // UDP 환경 기본값
        IsRetryEnabled = false;
        RetryTarget = RetryTarget.None;
        IsSequential = false;
        RequestTimeout = TimeSpan.FromMilliseconds(500);
        HeartbeatInterval = TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public override string ToString()
        => base.ToString() + $" | {RemoteHost}:{RemotePort} (local:{LocalPort})";
}