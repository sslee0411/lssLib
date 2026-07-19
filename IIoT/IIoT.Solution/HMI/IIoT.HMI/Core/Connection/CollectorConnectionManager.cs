// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Connection/CollectorConnectionManager.cs
//  역할: 등록된 Collector 목록(CollectorEndpoint[])과 실제 연결(CollectorConnection)을
//        CollectorId 기준 1:1로 동기화 관리한다 (DI 싱글턴).
//        [Collector 관리] 탭에서 목록이 변경될 때마다 SyncFromEndpointsAsync() 호출.
//        (IIoT.Monitor Core/Connection/CollectorConnectionManager.cs — MN-01B/
//         MN-02/MN-03/MN-EX-08 이식)
//
//  ★ "MN-01B 패턴 단순화" 결정(사용자 확인, 2026-07-16): Monitor 는 수신한
//    TagValue/AlarmChanged 를 LiveTagAggregator/AlarmAggregator(그리드용 집계
//    클래스)로 직접 연결하지만, HMI 는 아직 그런 집계 화면이 없다(HM-04
//    아이콘↔Tag 바인딩, HM-08 알람 오버레이 단계에서 필요해짐). 그래서 이
//    Manager 는 수신 이벤트를 특정 집계기에 강결합하지 않고 범용 이벤트
//    (TagValueReceived/AlarmChanged)로 재발행만 한다 — 이후 Step에서 구독자를
//    추가하면 된다. 연결 끊김/복구 알림도 트레이 서비스 없이 로그로만 남긴다
//    (트레이 상주 기능이 HMI Step 맵에 아직 없음 — 필요 시 후속 Step에서 추가).
//
//  HM-01: 신규
//  HM-05: GetConnectedEndpoints()/GetSnapshotAsync() 추가 — [레이아웃 편집] 탭의
//         Tag 바인딩 선택기(LayoutCanvasViewModel)가 "현재 연결된 Collector 목록"과
//         "선택한 Collector 의 Device/Tag 트리"를 조회할 때 사용한다.
//  HM-09: ForceWriteAsync(collectorId,plcId,tagId,value,apiKey) 추가 — AcknowledgeAlarmAsync와
//         동일하게 "발생 출처로만 전송" 원칙을 따르며, CollectorConnection.ForceWriteAsync()로
//         위임한다(아이콘 더블클릭 → 값 입력 다이얼로그가 사용).
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Config;
using IIoT.HMI.Models;
using lssLib.Log;
using System.Linq;
using System.Text.Json;

namespace IIoT.HMI.Core.Connection;

/// <summary>
/// CollectorId → CollectorConnection 1:1 매핑 관리자 (DI 싱글턴).
/// <para>
/// 등록된 Collector 목록이 바뀔 때마다(<see cref="SyncFromEndpointsAsync"/>) 호출하면
/// 새로 추가된 항목은 연결을 시작하고, 삭제되거나 비활성화된 항목은 연결을 종료한다.
/// 연결된 각 CollectorConnection 이 수신한 TagValue/AlarmChanged 는
/// <see cref="TagValueReceived"/>/<see cref="AlarmChanged"/> 이벤트로 재발행된다.
/// </para>
/// </summary>
public sealed class CollectorConnectionManager : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly HmiSettingsLoader _settingsLoader;
    private readonly Dictionary<string, CollectorConnection> _connections = new();

    // §2 ─ 이벤트 (HM-04/HM-08 등 후속 Step 구독용) ────────

    /// <summary>Collector 로부터 "TagValue" 수신 시 발행 (collectorId, payload)</summary>
    public event Action<string, JsonElement>? TagValueReceived;

    /// <summary>Collector 로부터 "AlarmChanged" 수신 시 발행 (collectorId, payload)</summary>
    public event Action<string, JsonElement>? AlarmChanged;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public CollectorConnectionManager(HmiSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §4 ─ 동기화 ──────────────────────────────────────────

    /// <summary>
    /// 등록된 Collector 목록과 현재 연결 상태를 동기화합니다.
    /// 이미 연결 중인 항목은 그대로 유지한다(재연결하지 않음).
    /// </summary>
    /// <returns>
    /// 동기화 중 발생한 예외 메시지 목록 (정상이면 빈 목록).
    /// 호출부(CollectorManageViewModel)가 StatusText 등으로 사용자에게 노출할 수 있다.
    /// </returns>
    public async Task<List<string>> SyncFromEndpointsAsync(IEnumerable<CollectorEndpoint> endpoints)
    {
        var errors = new List<string>();

        // ★ ToDictionary(e => e.Id) 는 Id 가 중복되면 ArgumentException 을 던지며
        //   그 즉시 전체 동기화가 중단된다 — 문제 항목뿐 아니라 나머지 정상 Collector 의
        //   연결 시도까지 전부 무산되는 심각한 연쇄 실패다(Monitor FIX(2) 교훈).
        //   중복 시 첫 항목만 채택하고 경고 로그 + errors 목록에 사유를 남긴다.
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

            // ② 신규 항목 연결 시작 + Host/Port 변경 시 재시작
            foreach (var (id, endpoint) in desired)
            {
                if (_connections.TryGetValue(id, out var existing))
                {
                    // ★ Host/Port 가 편집된 경우 기존(구주소) 연결을 그대로 두면
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
                    onCollectorIdResolved: (oldId, newId) => _OnCollectorIdResolved(oldId, newId),
                    onTagValue:     (collectorId, payload) => TagValueReceived?.Invoke(collectorId, payload),
                    onAlarmChanged: (collectorId, payload) => AlarmChanged?.Invoke(collectorId, payload),
                    onConnectionIssue: ep =>
                        LogManager.Instance.Warn("CollectorConnectionManager", $"Collector [{ep.Name}] 연결 끊김"),
                    onConnectionRecovered: ep =>
                        LogManager.Instance.Info("CollectorConnectionManager", $"Collector [{ep.Name}] 연결 복구됨"));

                _connections[id] = conn;

                // fire-and-forget — 개별 Collector 연결 실패가 다른 Collector나 UI를 막지 않도록
                _ = conn.StartAsync();
            }
        }
        catch (Exception ex)
        {
            var msg = $"Collector 연결 동기화 중 예외 발생: {ex.Message}";
            LogManager.Instance.Error("CollectorConnectionManager", msg);
            errors.Add(msg);
        }

        return errors;
    }

    /// <summary>
    /// CollectorConnection 이 자동 동기화로 Id 를 변경했을 때 호출됨.
    /// Dictionary 키를 갱신하고, hmi.json 을 즉시 저장한다.
    /// </summary>
    private void _OnCollectorIdResolved(string oldId, string newId)
    {
        if (_connections.TryGetValue(oldId, out var conn))
        {
            _connections.Remove(oldId);
            _connections[newId] = conn;
        }

        _ = _settingsLoader.SaveAsync();
    }

    // §4-1 ─ HM-05: Tag 바인딩 선택기 지원 ──────────────────

    /// <summary>현재 연결(연결 시도 포함)된 Collector 의 접속 정보 목록.</summary>
    public IReadOnlyList<CollectorEndpoint> GetConnectedEndpoints()
        => _connections.Values.Select(c => c.Endpoint).ToList();

    /// <summary>
    /// 지정된 Collector 의 Device/Tag 스냅샷을 조회한다.
    /// 연결이 없거나 조회에 실패하면 빈 목록을 반환한다(예외 없음).
    /// </summary>
    public async Task<List<DeviceSnapshotDto>> GetSnapshotAsync(string collectorId)
    {
        if (_connections.TryGetValue(collectorId, out var conn))
            return await conn.FetchSnapshotAsync() ?? new();

        return new();
    }

    // §5 ─ 알람 ACK (HM-08) ─────────────────────────────────

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

    // §5-1 ─ HM-09: ForceWrite 원격 호출 ────────────────────

    /// <summary>
    /// 지정된 CollectorId 의 연결로만 ForceWrite 요청을 전송한다("발생 출처로만
    /// 전송" 원칙 — ACK 와 동일). 해당 Collector 가 현재 연결되어 있지 않으면
    /// 즉시 실패 결과(ForceWriteResult.Fail 상당)를 반환한다(예외 없음).
    /// </summary>
    public async Task<ForceWriteResult> ForceWriteAsync(string collectorId, string plcId, string tagId, string value, string apiKey)
    {
        if (_connections.TryGetValue(collectorId, out var conn))
            return await conn.ForceWriteAsync(plcId, tagId, value, apiKey);

        var msg = $"ForceWrite 전송 실패 — Collector[{collectorId}] 연결 없음";
        LogManager.Instance.Warn("CollectorConnectionManager", msg);
        return new ForceWriteResult(false, msg);
    }

    // §6 ─ 정리 ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var conn in _connections.Values)
            await conn.DisposeAsync();

        _connections.Clear();
    }
}
