// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetDeviceRegistry.cs
//  역할: 전체 장비 채널 Lazy<T> 싱글톤 관리
// ══════════════════════════════════════════════════════════════════════

using System.Collections.Concurrent;

namespace lssLib.Net;

/// <summary>
/// 전체 장비 채널을 관리하는 <see cref="Lazy{T}"/> 싱글톤 레지스트리.
/// </summary>
/// <remarks>
/// <b>기본 사용 패턴:</b>
/// <code>
/// // autoRegister=true 로 생성 시 자동 등록
/// await using var channel = new RequestResponseChannel(
///     cfg, transport, protocol, autoRegister: true);
/// await channel.StartAsync();
///
/// // DeviceId 로 어디서든 접근
/// var plc = NetDeviceRegistry.Instance.Get(1);
/// if (plc?.IsConnected == true)
///     await plc.WriteAsync(frame);
///
/// // 연결된 전체 브로드캐스트 (비상 정지)
/// await NetDeviceRegistry.Instance.BroadcastAsync(emergencyStop, NetPriority.Critical);
///
/// // 전체 상태 조회 (WPF DataGrid 바인딩)
/// DgDevices.ItemsSource = NetDeviceRegistry.Instance.GetStatusAll().ToList();
///
/// // 앱 종료 시 일괄 정지
/// await NetDeviceRegistry.Instance.StopAllAsync();
/// NetDeviceRegistry.Instance.Clear();
/// </code>
/// </remarks>
public sealed class NetDeviceRegistry
{
    #region §1 ─ Lazy 싱글톤

    private static readonly Lazy<NetDeviceRegistry> _instance =
        new(() => new NetDeviceRegistry(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>레지스트리 전역 인스턴스. 처음 접근하는 순간 단 한 번 생성됩니다.</summary>
    public static NetDeviceRegistry Instance => _instance.Value;

    private NetDeviceRegistry() { }

    #endregion

    #region §2 ─ 필드

    private readonly ConcurrentDictionary<int, NetChannelBase> _channels = new();

    #endregion

    #region §3 ─ 등록 / 해제

    /// <summary>
    /// 채널을 레지스트리에 등록합니다.
    /// </summary>
    /// <param name="channel">등록할 채널</param>
    /// <exception cref="InvalidOperationException">동일 DeviceId 가 이미 등록된 경우.</exception>
    public void Register(NetChannelBase channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!_channels.TryAdd(channel.DeviceId, channel))
            throw new InvalidOperationException(
                $"DeviceId={channel.DeviceId} 는 이미 등록되어 있습니다. " +
                $"기존: '{_channels[channel.DeviceId].DeviceName}'");
    }

    /// <summary>DeviceId 로 채널 등록을 해제합니다.</summary>
    /// <returns>해제 성공 여부</returns>
    public bool Unregister(int deviceId) => _channels.TryRemove(deviceId, out _);

    /// <summary>모든 채널 등록을 해제합니다. (StopAllAsync 호출 후 사용 권장)</summary>
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

    /// <summary>등록된 모든 채널을 열거합니다.</summary>
    public IEnumerable<NetChannelBase> GetAll() => _channels.Values;

    /// <summary>연결된 채널만 열거합니다.</summary>
    public IEnumerable<NetChannelBase> GetConnected()
        => _channels.Values.Where(c => c.IsConnected);

    /// <summary>등록된 장비 수.</summary>
    public int Count => _channels.Count;

    /// <summary>연결된 장비 수.</summary>
    public int ConnectedCount => _channels.Values.Count(c => c.IsConnected);

    #endregion

    #region §5 ─ 일괄 제어

    /// <summary>모든 채널을 동시 시작합니다.</summary>
    public Task StartAllAsync(CancellationToken ct = default)
        => Task.WhenAll(_channels.Values.Select(c => c.StartAsync(ct)));

    /// <summary>
    /// 모든 채널을 동시 정지하고 Dispose 합니다.
    /// <para>⚠ StopAllAsync 후 Clear() 를 호출하여 레지스트리를 정리하세요.</para>
    /// </summary>
    public Task StopAllAsync()
        => Task.WhenAll(_channels.Values.Select(c => c.DisposeAsync().AsTask()));

    /// <summary>
    /// 연결된 모든 채널에 동일한 데이터를 동시 전송합니다 (브로드캐스트).
    /// </summary>
    /// <param name="data">전송 페이로드</param>
    /// <param name="priority">큐 우선순위. 기본: Write(1).</param>
    /// <param name="ct">취소 토큰</param>
    /// <example><code>
    /// // 비상 정지 명령 전체 브로드캐스트 (최우선)
    /// await NetDeviceRegistry.Instance.BroadcastAsync(
    ///     emergencyStopFrame, NetPriority.Critical);
    /// </code></example>
    public Task BroadcastAsync(byte[] data,
        NetPriority priority = NetPriority.Write, CancellationToken ct = default)
        => Task.WhenAll(GetConnected()
            .Select(c => c.WriteAsync(data, priority, false, ct)));

    #endregion

    #region §6 ─ 통계 / 진단

    /// <summary>
    /// 전체 장비 상태 요약을 반환합니다.
    /// <para>WPF DataGrid 바인딩에 활용합니다.</para>
    /// <code>
    /// DgDevices.ItemsSource = NetDeviceRegistry.Instance.GetStatusAll().ToList();
    /// </code>
    /// </summary>
    public IEnumerable<NetDeviceStatus> GetStatusAll()
        => _channels.Values.Select(c => new NetDeviceStatus(
            c.DeviceId, c.DeviceName, c.State, c.IsConnected,
            c.Statistics.TotalSent, c.Statistics.TotalReceived,
            c.Statistics.TotalErrors, c.Statistics.AvgResponseMs,
            c.Statistics.LastError));

    #endregion
}

/// <summary>
/// 장비 상태 요약 스냅샷 (읽기 전용 레코드).
/// <para>WPF DataGrid 바인딩용 단순 데이터 클래스입니다.</para>
/// </summary>
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