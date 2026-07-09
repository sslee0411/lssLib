// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Aggregation/AlarmAggregator.cs
//  역할: 모든 CollectorConnection 이 수신한 "AlarmChanged" 이벤트를
//        하나의 ObservableCollection<AlarmRow> 로 병합한다 (DI 싱글턴).
//        [알람] 탭(AlarmView)이 이 컬렉션을 그대로 바인딩한다.
//        구조는 LiveTagAggregator(MN-02)와 동일한 패턴.
//  MN-03: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

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

    // §3 ─ Collector 이름 등록 ─────────────────────────────

    /// <summary>CollectorId → 표시 이름 매핑을 등록/갱신한다 (LiveTagAggregator와 동일 패턴).</summary>
    public void RegisterCollectorName(string collectorId, string name)
    {
        _collectorNames[collectorId] = name;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var row in Rows)
                if (row.CollectorId == collectorId)
                    row.CollectorName = name;
        });
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

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_index.TryGetValue(key, out var row))
                {
                    row.Status    = status;
                    row.Message   = message;
                    row.EngValue  = engValue;
                    row.OccurredAt = occurredAt;
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
                }
            });
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("AlarmAggregator", $"AlarmChanged payload 파싱 실패: {ex.Message}");
        }
    }
}
