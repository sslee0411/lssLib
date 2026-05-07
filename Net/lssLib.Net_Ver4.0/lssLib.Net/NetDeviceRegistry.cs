// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetDeviceRegistry.cs
//  역할: 전체 장비 채널 Lazy<T> 싱글톤 관리
// ══════════════════════════════════════════════════════════════════════

using System.Collections.Concurrent;
//using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// 전체 장비 채널을 관리하는 Lazy<T> 싱글톤 레지스트리.
/// </summary>
/// <remarks>
/// <b>기본 사용 패턴:</b>
/// <code>
/// // 등록 (autoRegister=true 로 자동 등록 가능)
/// NetDeviceRegistry.Instance.Register(channel);
/// await channel.StartAsync();
///
/// // DeviceId 로 접근
/// var plc = NetDeviceRegistry.Instance.Get(1);
/// if (plc?.IsConnected == true)
///     await plc.WriteAsync(frame);
///
/// // 전체 브로드캐스트
/// await NetDeviceRegistry.Instance.BroadcastAsync(emergencyStop, NetPriority.Critical);
///
/// // 전체 상태 조회 (WPF DataGrid 바인딩)
/// DgDevices.ItemsSource = NetDeviceRegistry.Instance.GetStatusAll().ToList();
///
/// // 앱 종료
/// await NetDeviceRegistry.Instance.StopAllAsync();
/// </code>
/// </remarks>
public sealed class NetDeviceRegistry
{
    #region §1 ─ Lazy 싱글톤

    private static readonly Lazy<NetDeviceRegistry> _instance =
        new(() => new NetDeviceRegistry(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>레지스트리 전역 인스턴스.</summary>
    public static NetDeviceRegistry Instance => _instance.Value;

    private NetDeviceRegistry() { }

    #endregion

    #region §2 ─ 필드

    private readonly ConcurrentDictionary<int, NetChannelBase> _channels = new();

    #endregion

    #region §3 ─ 등록 / 해제

    /// <summary>채널을 레지스트리에 등록합니다.</summary>
    /// <exception cref="InvalidOperationException">동일 DeviceId 가 이미 등록된 경우.</exception>
    public void Register(NetChannelBase channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!_channels.TryAdd(channel.DeviceId, channel))
        {
            throw new InvalidOperationException(
                $"DeviceId={channel.DeviceId} 는 이미 등록되어 있습니다. " +
                $"기존: '{_channels[channel.DeviceId].DeviceName}'");
        }

        //LogManager.Instance.Info("Registry",  $"[{channel.DeviceName}#{channel.DeviceId}] 등록 (총 {_channels.Count}개)");
    }

    /// <summary>DeviceId 로 채널 등록을 해제합니다.</summary>
    /// <returns>해제 성공 여부</returns>
    public bool Unregister(int deviceId)
    {
        if (!_channels.TryRemove(deviceId, out var ch))
        {
            return false;
        }
        // LogManager.Instance.Info("Registry", $"[{ch.DeviceName}#{deviceId}] 해제 (남은 {_channels.Count}개)");
        return true;
    }

    /// <summary>모든 채널 등록을 해제합니다.</summary>
    public void Clear() => _channels.Clear();

    #endregion

    #region §4 ─ 조회

    /// <summary>DeviceId 로 채널을 조회합니다. 없으면 null.</summary>
    public NetChannelBase? Get(int deviceId)
        => _channels.TryGetValue(deviceId, out var ch) ? ch : null;

    /// <summary>DeviceName 으로 채널을 조회합니다 (대소문자 무시). 없으면 null.</summary>
    public NetChannelBase? GetByName(string deviceName)
        => _channels.Values.FirstOrDefault(c =>
            c.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>DeviceId 로 채널을 조회합니다. 없으면 예외.</summary>
    /// <exception cref="KeyNotFoundException"/>
    public NetChannelBase GetOrThrow(int deviceId)
        => Get(deviceId) ?? throw new KeyNotFoundException(
            $"DeviceId={deviceId} 가 레지스트리에 없습니다.");

    /// <summary>등록된 모든 채널을 열거합니다.</summary>
    public IEnumerable<NetChannelBase> GetAll() => _channels.Values;

    /// <summary>연결된 채널만 열거합니다.</summary>
    public IEnumerable<NetChannelBase> GetConnected() => _channels.Values.Where(c => c.IsConnected);

    /// <summary>연결이 끊긴 채널만 열거합니다.</summary>
    public IEnumerable<NetChannelBase> GetDisconnected() => _channels.Values.Where(c => !c.IsConnected);

    /// <summary>등록된 장비 수.</summary>
    public int Count => _channels.Count;

    /// <summary>연결된 장비 수.</summary>
    public int ConnectedCount => _channels.Values.Count(c => c.IsConnected);

    #endregion

    #region §5 ─ 일괄 제어

    /// <summary>모든 채널을 동시 시작합니다.</summary>
    public Task StartAllAsync(CancellationToken ct = default)
        => Task.WhenAll(_channels.Values.Select(c => c.StartAsync(ct)));

    /// <summary>모든 채널을 동시 정지합니다 (DisposeAsync 포함).</summary>
    public Task StopAllAsync()
        => Task.WhenAll(_channels.Values.Select(c => c.DisposeAsync().AsTask()));

    /// <summary>
    /// 연결된 모든 채널에 동일한 데이터를 동시 전송합니다 (브로드캐스트).
    /// </summary>
    /// <example><code>
    /// // 비상 정지 명령 전체 브로드캐스트
    /// await NetDeviceRegistry.Instance.BroadcastAsync(
    ///     emergencyStopFrame, NetPriority.Critical);
    /// </code></example>
    public Task BroadcastAsync(byte[] data,
        NetPriority priority = NetPriority.Write, CancellationToken ct = default)
        => Task.WhenAll( GetConnected().Select(c => c.WriteAsync(data, priority, ct) ) );

    #endregion

    #region §6 ─ 통계 / 진단

    /// <summary>전체 장비 상태 요약 (WPF DataGrid 바인딩용).</summary>
    public IEnumerable<NetDeviceStatus> GetStatusAll()
        => _channels.Values.Select(c => new NetDeviceStatus(
                c.DeviceId, c.DeviceName, c.State, c.IsConnected,
                c.Statistics.TotalSent, c.Statistics.TotalReceived,
                c.Statistics.TotalErrors, c.Statistics.AvgResponseMs,
                c.Statistics.LastError)
        );

    /// <summary>전체 상태를 LogManager 에 출력합니다.</summary>
    public void LogStatus()
    {
        // LogManager.Instance.Info("Registry", $"총 {Count}개 장비 / 연결 {ConnectedCount}개");
        foreach (var s in GetStatusAll())
        {
            /*
            LogManager.Instance.Info("Registry",
                $"  [{s.DeviceName}#{s.DeviceId}] {s.State} " +
                $"전송={s.TotalSent} 수신={s.TotalReceived} 오류={s.TotalErrors}");*/
        }
    }

    #endregion
}

/// <summary>장비 상태 요약 스냅샷 (읽기 전용 레코드).</summary>
public sealed record NetDeviceStatus(
    int DeviceId,
    string DeviceName,
    NetState State,
    bool IsConnected,
    long TotalSent,
    long TotalReceived,
    long TotalErrors,
    double AvgResponseMs,
    string LastError);