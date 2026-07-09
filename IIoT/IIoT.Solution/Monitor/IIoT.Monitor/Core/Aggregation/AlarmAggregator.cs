// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Aggregation/AlarmAggregator.cs
//  역할: 모든 CollectorConnection 이 수신한 "AlarmChanged" 이벤트를
//        하나의 ObservableCollection<AlarmRow> 로 병합한다 (DI 싱글턴).
//        [알람] 탭(AlarmView)이 이 컬렉션을 그대로 바인딩한다.
//        구조는 LiveTagAggregator(MN-02)와 동일한 패턴.
//  MN-03: 신규
//  FIX(2026-07-08): Dispatcher.Invoke(동기·블로킹) → Dispatcher.BeginInvoke(비동기)
//                   로 변경 — LiveTagAggregator.cs 와 동일한 사유(앱 종료 시
//                   교착상태로 디버깅이 멈추지 않던 문제).
//  MN-EX-01: NewAlarmCreated 이벤트 추가 — TrayNotificationService 연동용
//  MN-EX-02: AlarmRecorded 이벤트 추가 — AlarmHistoryService(SQLite 이력저장) 연동용
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-02)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace IIoT.Monitor.Core.Aggregation;

/// <summary>전체 Collector 의 실시간 알람을 하나의 컬렉션으로 병합하는 집계기 (DI 싱글턴).</summary>
public sealed class AlarmAggregator
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly Dictionary<string, AlarmRow> _index = new();
    private readonly Dictionary<string, string>    _collectorNames = new();

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 알람 목록 (UI 바인딩 대상)</summary>
    public ObservableCollection<AlarmRow> Rows { get; } = new();

    /// <summary>
    /// ★ MN-EX-01 신규: 새 알람이 "처음" 생성될 때만 발행되는 이벤트
    /// (기존 알람의 상태 갱신 시에는 발행되지 않음 — 중복 알림 방지).
    /// TrayNotificationService 가 이 이벤트를 구독해 사운드+트레이 알림을 표시한다.
    /// UI 스레드(Dispatcher.BeginInvoke 콜백 내부)에서 발행되므로 구독자는
    /// 별도 스레드 마샬링 없이 UI 요소를 안전하게 다룰 수 있다.
    /// </summary>
    public event Action<AlarmRow>? NewAlarmCreated;

    /// <summary>
    /// ★ MN-EX-02 신규: 알람이 생성되거나 상태가 바뀔 때마다(Active→Acked→Recovered
    /// 전이 포함) 매번 발행되는 이벤트. AlarmHistoryService 가 구독하여 모든
    /// 상태 전이를 SQLite 이력에 기록한다 (NewAlarmCreated 와 달리 갱신 시에도 발행).
    /// </summary>
    public event Action<AlarmRow>? AlarmRecorded;

    // §3 ─ Collector 이름 등록 ─────────────────────────────

    /// <summary>CollectorId → 표시 이름 매핑을 등록/갱신한다 (LiveTagAggregator와 동일 패턴).</summary>
    public void RegisterCollectorName(string collectorId, string name)
    {
        _collectorNames[collectorId] = name;

        // ★ FIX: BeginInvoke(비동기) — Invoke(동기)는 앱 종료 시 교착상태 유발
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            foreach (var row in Rows)
                if (row.CollectorId == collectorId)
                    row.CollectorName = name;
        }, DispatcherPriority.Background);
    }

    // §4 ─ AlarmChanged 수신 처리 ──────────────────────────

    /// <summary>
    /// CollectorConnection 으로부터 "AlarmChanged" 이벤트를 전달받아 Rows 에 반영한다.
    /// 같은 (CollectorId, alarmKey) 조합이면 기존 행을 갱신(Active→Acked→Recovered 전이),
    /// 아니면 새 행을 추가한다.
    /// </summary>
    public void OnAlarmChanged(string collectorId, JsonElement data)
    {
        try
        {
            var alarmKey = data.GetProperty("alarmKey").GetString() ?? "";
            var tagId    = data.GetProperty("tagId").GetString() ?? "";
            var plcId    = data.GetProperty("plcId").GetString() ?? "";
            var tagName  = data.TryGetProperty("tagName", out var tn) ? (tn.GetString() ?? "") : "";
            var level    = data.TryGetProperty("level",   out var lv) ? (lv.GetString() ?? "") : "";
            var status   = data.TryGetProperty("status",  out var st) ? (st.GetString() ?? "Active") : "Active";
            var message  = data.TryGetProperty("message", out var ms) ? (ms.GetString() ?? "") : "";
            var engValue = data.TryGetProperty("engValue", out var ev) ? ev.GetDouble() : 0.0;
            var occurredAt = data.TryGetProperty("ts", out var tsEl)
                               && DateTimeOffset.TryParse(tsEl.GetString(), out var parsed)
                                  ? parsed
                                  : DateTimeOffset.UtcNow;

            if (string.IsNullOrEmpty(alarmKey))
                return;

            var key = $"{collectorId}:{alarmKey}";

            // ★ FIX: BeginInvoke(비동기) — Invoke(동기)는 앱 종료 시 교착상태 유발
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_index.TryGetValue(key, out var row))
                {
                    row.Status    = status;
                    row.Message   = message;
                    row.EngValue  = engValue;
                    row.OccurredAt = occurredAt;

                    // ★ MN-EX-02: 상태 갱신도 이력에 기록
                    AlarmRecorded?.Invoke(row);
                }
                else
                {
                    var newRow = new AlarmRow
                    {
                        CollectorId   = collectorId,
                        CollectorName = _collectorNames.GetValueOrDefault(collectorId, collectorId),
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

                    // ★ MN-EX-01: 신규 알람 생성 시에만 알림 이벤트 발행 (갱신 시엔 발행 안 함)
                    NewAlarmCreated?.Invoke(newRow);
                    // ★ MN-EX-02: 신규 생성도 이력에 기록
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
