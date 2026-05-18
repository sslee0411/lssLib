
namespace lssLib.Net;


/// <summary>가상 Transport 장비 설정.</summary>
/// <example><code>
/// var hub = VirtualTransportHub.Create("sensor-sim");
///
/// // 수신 채널
/// var cfg = new VirtualDeviceConfig(9, "VirtualCh", hub, isServer: false);
/// // cfg.SequenceMode == 0 (Parallel) ← 기본값
///
/// // SequenceMode = 3 으로 변경하면 슬라이딩 윈도우 3개 동시 시뮬레이션
/// cfg.SequenceMode = 3;
/// </code></example>
public sealed class VirtualDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.Virtual;

    public VirtualTransportHub Hub { get; }
    public bool IsServer { get; }

    public VirtualDeviceConfig(int deviceId, string deviceName,
        VirtualTransportHub hub, bool isServer = false)
        : base(deviceId, deviceName)
    {
        Hub = hub;
        IsServer = isServer;
        IsRetryEnabled = false;
        RetryTarget = RetryTarget.None;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        HeartbeatInterval = TimeSpan.Zero;
        PeriodicInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | Hub={Hub.Name} Role={(IsServer ? "Server" : "Client")}";
}