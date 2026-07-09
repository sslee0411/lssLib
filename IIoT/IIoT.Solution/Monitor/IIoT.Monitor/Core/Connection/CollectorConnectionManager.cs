// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnectionManager.cs
//  역할: 등록된 Collector 목록(CollectorEndpoint[])과 실제 연결(CollectorConnection)을
//        CollectorId 기준 1:1로 동기화 관리한다 (DI 싱글턴).
//        [Collector 관리] 탭에서 목록이 변경될 때마다 SyncFromEndpointsAsync() 호출.
//  MN-01B: 신규
//  MN-02: LiveTagAggregator 연동 — Collector 이름 등록 + TagValue 콜백 연결
//  MN-03: AlarmAggregator 연동 + AcknowledgeAlarmAsync() — 알람 ACK를 발생
//         출처 Collector 로만 라우팅
//  FIX(2026-07-07): Host/Port 편집 후 저장해도 기존 연결이 재시작되지 않던
//                   문제 수정 — CollectorConnection.StartedHubUrl 과 비교하여
//                   변경 시 기존 연결 종료 후 재생성
//  FIX(2): 중복 CollectorId 로 인해 ToDictionary() 가 예외를 던지며 전체
//          동기화(다른 정상 Collector 포함)가 조용히 중단되던 심각한 버그 수정.
//          SyncFromEndpointsAsync() 가 이제 발생한 오류 메시지 목록을 반환하며,
//          호출부(CollectorManageViewModel)가 StatusText 로 노출한다.
//  MN-EX-08: TrayNotificationService 주입 — CollectorConnection 의 디바운스된
//            연결 끊김/복구 콜백을 트레이 알림 + 로그로 연결
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-08)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Core.Notification;
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

    private readonly MonitorSettingsLoader   _settingsLoader;
    private readonly LiveTagAggregator       _tagAggregator;
    private readonly AlarmAggregator         _alarmAggregator;
    private readonly TrayNotificationService _trayService;
    private readonly Dictionary<string, CollectorConnection> _connections = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public CollectorConnectionManager(
        MonitorSettingsLoader   settingsLoader,
        LiveTagAggregator       tagAggregator,
        AlarmAggregator         alarmAggregator,
        TrayNotificationService trayService)
    {
        _settingsLoader  = settingsLoader;
        _tagAggregator   = tagAggregator;
        _alarmAggregator = alarmAggregator;
        _trayService     = trayService;
    }

    // §3 ─ 동기화 ──────────────────────────────────────────

    /// <summary>
    /// 등록된 Collector 목록과 현재 연결 상태를 동기화합니다.
    /// 이미 연결 중인 항목은 그대로 유지한다(재연결하지 않음).
    /// 활성 상태인 모든 항목에 대해 표시 이름을 매번 최신화한다(이름 변경 즉시 반영).
    /// </summary>
    /// <returns>
    /// 동기화 중 발생한 예외 메시지 목록 (정상이면 빈 목록).
    /// 호출부(CollectorManageViewModel)가 StatusText 등으로 사용자에게 노출할 수 있다.
    /// </returns>
    public async Task<List<string>> SyncFromEndpointsAsync(IEnumerable<CollectorEndpoint> endpoints)
    {
        var errors = new List<string>();

        // ★ FIX: ToDictionary(e => e.Id) 는 Id 가 중복되면 ArgumentException 을 던지며
        //   그 즉시 전체 동기화가 중단된다 — 문제 항목뿐 아니라 나머지 정상 Collector 의
        //   연결 시도까지 전부 무산되는 심각한 연쇄 실패였다. 중복 시 첫 항목만 채택하고
        //   경고 로그 + errors 목록에 사유를 남기도록 안전하게 변경.
        var duplicateIds = endpoints
            .Where(e => e.Enabled)
            .GroupBy(e => e.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dupId in duplicateIds)
        {
            var msg = $"CollectorId 중복 감지: '{dupId}' — 첫 항목만 연결하고 나머지는 건너뜁니다. " +
                      "[Collector 관리] 탭에서 ID를 서로 다르게 수정해 주세요.";
            LogManager.Instance.Error("CollectorConnectionManager", msg);
            errors.Add(msg);
        }

        var desired = endpoints
            .Where(e => e.Enabled)
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First());

        try
        {
            // ① 목록에서 사라졌거나 비활성화된 연결 정리
            foreach (var key in _connections.Keys.Except(desired.Keys).ToList())
            {
                await _connections[key].StopAsync();
                await _connections[key].DisposeAsync();
                _connections.Remove(key);

                LogManager.Instance.Info("CollectorConnectionManager",
                    $"Collector[{key}] 연결 해제 (목록에서 제거되었거나 비활성화됨)");
            }

            // ② 신규 항목 연결 시작 + 표시 이름 최신화 + Host/Port 변경 시 재시작
            foreach (var (id, endpoint) in desired)
            {
                _tagAggregator.RegisterCollectorName(id, endpoint.Name);
                _alarmAggregator.RegisterCollectorName(id, endpoint.Name);

                if (_connections.TryGetValue(id, out var existing))
                {
                    // ★ FIX: Host/Port 가 편집된 경우 기존(구주소) 연결을 그대로 두면
                    //    영원히 이전 주소로만 접속을 시도하게 된다 — 재시작 필요.
                    if (existing.StartedHubUrl == endpoint.HubUrl)
                        continue; // 변경 없음 — 기존 연결 유지

                    await existing.StopAsync();
                    await existing.DisposeAsync();
                    _connections.Remove(id);

                    LogManager.Instance.Info("CollectorConnectionManager",
                        $"Collector[{id}] Host/Port 변경 감지 → 연결 재시작 ({endpoint.HubUrl})");
                }

                var conn = new CollectorConnection(
                    endpoint,
                    onCollectorIdResolved: (oldId, newId) => _OnCollectorIdResolved(oldId, newId, endpoint),
                    onTagValue:     _tagAggregator.OnTagValueReceived,
                    onAlarmChanged: _alarmAggregator.OnAlarmChanged,
                    onConnectionIssue: ep =>
                    {
                        var msg = $"Collector [{ep.Name}] 연결 끊김";
                        LogManager.Instance.Warn("CollectorConnectionManager", msg);
                        _trayService.NotifyConnectionEvent("⚠ Collector 연결 끊김", msg, isRecovery: false);
                    },
                    onConnectionRecovered: ep =>
                    {
                        var msg = $"Collector [{ep.Name}] 연결 복구됨";
                        LogManager.Instance.Info("CollectorConnectionManager", msg);
                        _trayService.NotifyConnectionEvent("✔ Collector 연결 복구", msg, isRecovery: true);
                    });

                _connections[id] = conn;

                // fire-and-forget — 개별 Collector 연결 실패가 다른 Collector나 UI를 막지 않도록
                _ = conn.StartAsync();
            }
        }
        catch (Exception ex)
        {
            // ★ FIX: 이전에는 여기서 예외가 나면 SaveCommand 호출부까지 조용히 삼켜져
            //   "저장을 눌러도 미연결에서 전혀 안 바뀌는" 증상으로 나타났다.
            //   이제는 반드시 로그로 남기고 errors 로 호출부에 전달한다.
            var msg = $"Collector 연결 동기화 중 예외 발생: {ex.Message}";
            LogManager.Instance.Error("CollectorConnectionManager", msg);
            errors.Add(msg);
        }

        return errors;
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
