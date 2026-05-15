// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/SharedMemDeviceConfig.cs
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>공유 메모리 IPC 장비 설정.</summary>
/// <example><code>
/// // Writer 프로세스
/// var txCfg = new SharedMemDeviceConfig(10, "IPC-Writer", "lssLib_Ch01", SharedMemoryRole.Writer);
/// await using var tx = new PassiveNetChannel(
///     txCfg, SharedMemoryTransport.FromConfig(txCfg), new RawProtocol());
/// await tx.StartAsync();
/// await tx.WriteAsync(sensorFrame);
///
/// // Reader 프로세스
/// var rxCfg = new SharedMemDeviceConfig(11, "IPC-Reader", "lssLib_Ch01", SharedMemoryRole.Reader)
/// { MapSize = 65536, PeriodicInterval = TimeSpan.FromMilliseconds(5) };
/// await using var rx = new PassiveNetChannel(
///     rxCfg, SharedMemoryTransport.FromConfig(rxCfg), new RawProtocol());
/// rx.DeviceFrameReceived += (id, frame) => ProcessFrame(frame);
/// await rx.StartAsync();
/// </code></example>
public sealed class SharedMemDeviceConfig : NetDeviceConfigBase
{
    /// <inheritdoc/>
    public override NetTransportType TransportType => NetTransportType.SharedMemory;

    /// <summary>공유 메모리 맵 이름. Writer/Reader 간 반드시 동일해야 합니다.</summary>
    public string MapName { get; set; }

    /// <summary>이 프로세스의 역할 (Writer / Reader).</summary>
    public SharedMemoryRole Role { get; set; }

    /// <summary>공유 메모리 크기(bytes). 기본값: 64KB.</summary>
    public long MapSize { get; set; } = 65536;

    public SharedMemDeviceConfig(int deviceId, string deviceName,
        string mapName, SharedMemoryRole role)
        : base(deviceId, deviceName)
    {
        MapName = mapName;
        Role = role;
        // IPC 환경 기본값
        IsRetryEnabled = false;
        RetryTarget = RetryTarget.None;
        IsSequential = false;
        PeriodicInterval = TimeSpan.FromMilliseconds(5);
        RequestTimeout = TimeSpan.FromMilliseconds(200);
        HeartbeatInterval = TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public override string ToString()
        => base.ToString() + $" | [{Role}] {MapName} ({MapSize / 1024}KB)";
}