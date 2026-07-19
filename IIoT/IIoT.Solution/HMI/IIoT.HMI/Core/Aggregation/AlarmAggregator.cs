// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Core/Aggregation/AlarmAggregator.cs
//  역할: 모든 CollectorConnection 이 수신한 "AlarmChanged" 이벤트를
//        하나의 ObservableCollection<AlarmRow> 로 병합한다 (DI 싱글턴).
//        [알람] 탭(AlarmView)이 이 컬렉션을 그대로 바인딩한다.
//        (IIoT.Monitor Core/Aggregation/AlarmAggregator.cs — MN-03 이식.
//         MN-EX-01(TrayNotification) 연동용 이벤트는 이식하지 않음 — HMI에는
//         트레이 상주 기능이 없음. MN-EX-02(SQLite 이력저장)용 AlarmRecorded
//         이벤트는 HM-14 시점엔 범위 밖(사용자 확인: "실시간 목록만")이었으나
//         HM-16에서 추가함 — 아래 참조.)
//  HM-16: AlarmRecorded 이벤트 추가 — 알람이 생성되거나 상태가 바뀔 때마다
//         (Active→Acked→Recovered 전이 포함) 매번 발행된다. AlarmHistoryService 가
//         구독해 SQLite alarm_history 에 매 전이를 기록한다(Monitor MN-EX-02 이식).
//
//  ★ CollectorConnectionManager 와의 결합 방식 — Monitor 와 다르게 선택:
//    Monitor 는 CollectorConnectionManager 생성자가 AlarmAggregator 를 직접
//    주입받아 강결합한다. 반면 HMI 의 CollectorConnectionManager 는 HM-01에서
//    이미 "특정 집계기에 강결합하지 않고 범용 이벤트로만 재발행" 하기로 결정된
//    상태였다(해당 파일 헤더 "MN-01B 패턴 단순화" 주석 참조). 그 결정을 그대로
//    존중해 AlarmAggregator 쪽에서 CollectorConnectionManager.AlarmChanged 를
//    구독하는 방향으로 뒤집었다(LayoutCanvasViewModel 이 이미 동일한 방식으로
//    AlarmChanged 를 구독하고 있어 일관성도 맞는다).
//
//  ★ CollectorName 해석: Monitor 는 RegisterCollectorName() 을 별도로 두어
//    Collector 목록 변경 시마다 실시간으로 이름을 갱신하지만, HMI 의
//    CollectorConnectionManager 에는 그런 훅이 없다(추가하지 않기로 함 —
//    최소 변경 원칙). 대신 알람 수신 시점에 GetConnectedEndpoints() 로 1회
//    조회해 이름을 채운다. 단순화: Collector 이름을 그 이후에 바꿔도 이미
//    생성된 행의 CollectorName 은 갱신되지 않는다(실무 영향 적음 — 이름 변경은
//    드물고, 재연결 시 새 알람 행부터는 새 이름이 반영됨).
//
//  HM-14: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Connection;
using IIoT.HMI.Models;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace IIoT.HMI.Core.Aggregation;

/// <summary>전체 Collector 의 실시간 알람을 하나의 컬렉션으로 병합하는 집계기 (DI 싱글턴).</summary>
public sealed class AlarmAggregator
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorConnectionManager _connectionManager;
    private readonly Dictionary<string, AlarmRow> _index = new();

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 알람 목록 (UI 바인딩 대상)</summary>
    public ObservableCollection<AlarmRow> Rows { get; } = new();

    /// <summary>
    /// ★ HM-16 신규: 알람이 생성되거나 상태가 바뀔 때마다(Active→Acked→Recovered
    /// 전이 포함) 매번 발행되는 이벤트. AlarmHistoryService 가 구독하여 모든
    /// 상태 전이를 SQLite 이력에 기록한다(Monitor MN-EX-02 AlarmRecorded 이식).
    /// </summary>
    public event Action<AlarmRow>? AlarmRecorded;

    // §3 ─ 생성자 ──────────────────────────────────────────

    public AlarmAggregator(CollectorConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _connectionManager.AlarmChanged += OnAlarmChanged;
    }

    // §4 ─ AlarmChanged 수신 처리 ──────────────────────────

    /// <summary>
    /// CollectorConnectionManager 로부터 "AlarmChanged" 이벤트를 전달받아 Rows 에 반영한다.
    /// 같은 (CollectorId, alarmKey) 조합이면 기존 행을 갱신(Active→Acked→Recovered 전이),
    /// 아니면 새 행을 추가한다. payload 스키마는 LayoutCanvasViewModel._OnAlarmChanged()
    /// 와 동일(tagId/plcId/alarmKey/level/status/message/ts) — HM-08에서 이미 확인된 형식.
    /// </summary>
    public void OnAlarmChanged(string collectorId, JsonElement data)
    {
        try
        {
            var alarmKey = data.TryGetProperty("alarmKey", out var ak) ? (ak.GetString() ?? "") : "";
            var tagId    = data.TryGetProperty("tagId",    out var ti) ? (ti.GetString() ?? "") : "";
            var plcId    = data.TryGetProperty("plcId",    out var pi) ? (pi.GetString() ?? "") : "";
            var tagName  = data.TryGetProperty("tagName",  out var tn) ? (tn.GetString() ?? "") : "";
            var level    = data.TryGetProperty("level",    out var lv) ? (lv.GetString() ?? "") : "";
            var status   = data.TryGetProperty("status",   out var st) ? (st.GetString() ?? "Active") : "Active";
            var message  = data.TryGetProperty("message",  out var ms) ? (ms.GetString() ?? "") : "";
            var engValue = data.TryGetProperty("engValue", out var ev) ? ev.GetDouble() : 0.0;
            var occurredAt = data.TryGetProperty("ts", out var tsEl)
                               && DateTimeOffset.TryParse(tsEl.GetString(), out var parsed)
                                  ? parsed
                                  : DateTimeOffset.UtcNow;

            if (string.IsNullOrEmpty(alarmKey))
                return;

            var key = $"{collectorId}:{alarmKey}";

            // ★ BeginInvoke(비동기) — Invoke(동기)는 앱 종료 시 교착상태 유발
            //   (Monitor AlarmAggregator FIX(2026-07-08)와 동일 사유 — 프로젝트 공통 규칙)
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_index.TryGetValue(key, out var row))
                {
                    row.Status     = status;
                    row.Message    = message;
                    row.EngValue   = engValue;
                    row.OccurredAt = occurredAt;

                    // ★ HM-16: 상태 갱신도 이력에 기록
                    AlarmRecorded?.Invoke(row);
                }
                else
                {
                    var collectorName = _connectionManager.GetConnectedEndpoints()
                        .FirstOrDefault(e => e.Id == collectorId)?.Name ?? collectorId;

                    var newRow = new AlarmRow
                    {
                        CollectorId   = collectorId,
                        CollectorName = collectorName,
                        AlarmKey      = alarmKey,
                        TagId         = tagId,
                        PlcId         = plcId,
                        TagName       = tagName,
                        Level         = level,
                        Status        = status,
                        Message       = message,
                        EngValue      = engValue,
                        OccurredAt    = occurredAt
                    };
                    _index[key] = newRow;
                    Rows.Insert(0, newRow); // 최신 알람이 위로 오도록

                    // ★ HM-16: 신규 생성도 이력에 기록
                    AlarmRecorded?.Invoke(newRow);
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("AlarmAggregator", $"AlarmChanged payload 파싱 실패: {ex.Message}");
        }
    }
}
