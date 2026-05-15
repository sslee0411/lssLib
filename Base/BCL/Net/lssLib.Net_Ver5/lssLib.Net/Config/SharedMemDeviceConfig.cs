// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/SharedMemDeviceConfig.cs  [v5.1]
//  SequenceMode = 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>공유 메모리 IPC 장비 설정.</summary>
public sealed class SharedMemDeviceConfig : NetDeviceConfig
{
    public override NetTransportType TransportType => NetTransportType.SharedMemory;

    public string MapName { get; set; }
    public SharedMemoryRole Role { get; set; }
    public long MapSize { get; set; } = 65536;

    public SharedMemDeviceConfig(int deviceId, string deviceName,
        string mapName, SharedMemoryRole role)
        : base(deviceId, deviceName)
    {
        MapName = mapName;
        Role = role;
        IsRetryEnabled = false;
        RetryTarget = RetryTarget.None;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        PeriodicInterval = TimeSpan.FromMilliseconds(5);
        RequestTimeout = TimeSpan.FromMilliseconds(200);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | [{Role}] {MapName} ({MapSize / 1024}KB)";
}