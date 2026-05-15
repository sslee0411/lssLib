// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/UdpDeviceConfig.cs  [v5.1]
//  SequenceMode = 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>UDP 통신 장비 설정.</summary>
public sealed class UdpDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Udp;

    public string RemoteHost { get; set; }
    public int RemotePort { get; set; }
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
