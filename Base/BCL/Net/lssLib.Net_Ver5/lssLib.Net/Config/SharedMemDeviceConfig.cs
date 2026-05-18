// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/SharedMemDeviceConfig.cs  [v5.1]
//  SequenceMode = 0 (Parallel)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>공유 메모리 IPC 장비 설정.</summary>
public sealed class SharedMemDeviceConfig : NetDeviceConfig
{
    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public override NetTransportType TransportType => NetTransportType.SharedMemory;

    /// <summary>
    /// 공유 메모리 맵 이름. Writer/Reader 간 반드시 동일해야 합니다. 예: "MySharedMemMap".
    /// </summary>
    public string MapName { get; set; }

    /// <summary>
    /// 이 인스턴스의 역할을 지정합니다. Writer는 데이터를 기록하는 역할, Reader는 데이터를 읽는 역할을 합니다. Writer/Reader 간 역할이 일치해야 통신이 정상적으로 이루어집니다.
    /// </summary>
    public SharedMemoryRole Role { get; set; }

    /// <summary>
    /// 공유 메모리 크기(bytes). 기본값은 65536 (64KB)입니다. Writer와 Reader 모두 동일한 크기로 설정해야 합니다. 
    /// 크기가 너무 작으면 데이터 손실이 발생할 수 있고, 너무 크면 시스템 자원을 불필요하게 사용할 수 있습니다.
    /// </summary>
    public long MapSize { get; set; } = 65536;

    /// <summary>
    /// 생성자. deviceId와 deviceName은 NetDeviceConfig의 필수 매개변수입니다. mapName과 role은 SharedMemoryTransport에서 통신을 위해 반드시 필요합니다.
    /// </summary>
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