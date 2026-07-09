// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnectionManager.cs
//  역할: 등록된 Collector 목록(CollectorEndpoint[])과 실제 연결(CollectorConnection)을
//        CollectorId 기준 1:1로 동기화 관리한다 (DI 싱글턴).
//        [Collector 관리] 탭에서 목록이 변경될 때마다 SyncFromEndpointsAsync() 호출.
//  MN-01B: 신규
//  MN-02: LiveTagAggregator 연동 — Collector 이름 등록 + TagValue 콜백 연결
//  MN-03: AlarmAggregator 연동 + AcknowledgeAlarmAsync() — 알람 ACK를 발생
//         출처 Collector 로만 라우팅
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-03)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Models;
using lssLib.Log;

namespace IIoT.Monitor.Core.Connection;

/// <summary>
/// CollectorId → CollectorConnection 1:1 매핑 관리자 (DI 싱글턴).
/// <para>
/// 등록된 Collector 목록이 바뀔 때마다(<see cref="SyncFromEndpointsAsync"/>) 호출하면
/// 새로 추가된 항목은 연결을 시작하고, 삭제되거나 비활성화된 항목은 연결을 종료한다.
/// 연결된 각 CollectorConnection 이 수신한 TagValue/AlarmChanged 이벤트는
/// 각각 <see cref="LiveTagAggregator"/>/<see cref="AlarmAggregator"/> 로 전달된다.
/// </para>
/// </summary>
public sealed class CollectorConnectionManager : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly MonitorSettingsLoader _settingsLoader;
    private readonly LiveTagAggregator     _tagAggregator;
    private readonly AlarmAggregator       _alarmAggregator;
    private readonly Dictionary<string, CollectorConnection> _connections = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public CollectorConnectionManager(
        MonitorSettingsLoader settingsLoader,
        LiveTagAggregator     tagAggregator,
        AlarmAggregator       alarmAggregator)
    {
        _settingsLoader  = settingsLoader;
        _tagAggregator   = tagAggregator;
        _alarmAggregator = alarmAggregator;
    }

    // §3 ─ 동기화 ──────────────────────────────────────────

    /// <summary>
    /// 등록된 Collector 목록과 현재 연결 상태를 동기화합니다.
    /// 이미 연결 중인 항목은 그대로 유지한다(재연결하지 않음).
    /// 활성 상태인 모든 항목에 대해 표시 이름을 매번 최신화한다(이름 변경 즉시 반영).
    /// </summary>
    public async Task SyncFromEndpointsAsync(IEnumerable<CollectorEndpoint> endpoints)
    {
        var desired = endpoints.Where(e => e.Enabled).ToDictionary(e => e.Id);

        // ① 목록에서 사라졌거나 비활성화된 연결 정리
        foreach (var key in _connections.Keys.Except(desired.Keys).ToList())
        {
            await _connections[key].StopAsync();
            await _connections[key].DisposeAsync();
            _connections.Remove(key);

            LogManager.Instance.Info("CollectorConnectionManager",
                $"Collector[{key}] 연결 해제 (목록에서 제거되었거나 비활성화됨)");
        }

        // ② 신규 항목 연결 시작 + 표시 이름 최신화
        foreach (var (id, endpoint) in desired)
        {
            _tagAggregator.RegisterCollectorName(id, endpoint.Name);
            _alarmAggregator.RegisterCollectorName(id, endpoint.Name);

            if (_connections.ContainsKey(id))
                continue;

            var conn = new CollectorConnection(
                endpoint,
                onCollectorIdResolved: (oldId, newId) => _OnCollectorIdResolved(oldId, newId, endpoint),
                onTagValue:     _tagAggregator.OnTagValueReceived,
                onAlarmChanged: _alarmAggregator.OnAlarmChanged);

            _connections[id] = conn;

            // fire-and-forget — 개별 Collector 연결 실패가 다른 Collector나 UI를 막지 않도록
            _ = conn.StartAsync();
        }
    }

    /// <summary>
    /// CollectorConnection 이 자동 동기화로 Id 를 변경했을 때 호출됨.
    /// Dictionary 키를 갱신하고, 양쪽 Aggregator 의 이름 매핑도 새 Id 로 등록하며,
    /// monitor.json 을 즉시 저장한다.
    /// </summary>
    private void _OnCollectorIdResolved(string oldId, string newId, CollectorEndpoint endpoint)
    {
        if (_connections.TryGetValue(oldId, out var conn))
        {
            _connections.Remove(oldId);
            _connections[newId] = conn;
        }

        _tagAggregator.RegisterCollectorName(newId, endpoint.Name);
        _alarmAggregator.RegisterCollectorName(newId, endpoint.Name);

        _ = _settingsLoader.SaveAsync();
    }

    // §4 ─ 알람 ACK (MN-03) ────────────────────────────────

    /// <summary>
    /// 지정된 CollectorId 의 연결로만 ACK 요청을 전송한다("발생 출처로만 전송" 원칙).
    /// 해당 Collector 가 현재 연결되어 있지 않으면 아무 동작도 하지 않는다(조용히 무시).
    /// </summary>
    public async Task AcknowledgeAlarmAsync(string collectorId, string alarmKey)
    {
        if (_connections.TryGetValue(collectorId, out var conn))
            await conn.AcknowledgeAsync(alarmKey);
        else
            LogManager.Instance.Warn("CollectorConnectionManager",
                $"ACK 전송 실패 — Collector[{collectorId}] 연결 없음 (alarmKey={alarmKey})");
    }

    // §5 ─ 정리 ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var conn in _connections.Values)
            await conn.DisposeAsync();

        _connections.Clear();
    }
}
