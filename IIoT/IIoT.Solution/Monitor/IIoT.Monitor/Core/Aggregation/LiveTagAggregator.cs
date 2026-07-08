// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Aggregation/LiveTagAggregator.cs
//  역할: 모든 CollectorConnection 이 수신한 "TagValue" 이벤트를
//        하나의 ObservableCollection<LiveTagRow> 로 병합한다 (DI 싱글턴).
//        [태그현황] 탭(LiveTagView)이 이 컬렉션을 그대로 바인딩한다.
//  MN-02: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace IIoT.Monitor.Core.Aggregation;

/// <summary>
/// 전체 Collector 의 실시간 Tag 값을 하나의 컬렉션으로 병합하는 집계기 (DI 싱글턴).
/// <para>
/// CollectorConnection 이 "TagValue" 이벤트를 받을 때마다
/// <see cref="OnTagValueReceived"/> 를 호출하며, 이 클래스는
/// (CollectorId, PlcId, TagId) 조합을 키로 기존 행을 갱신하거나 새 행을 추가한다.
/// </para>
/// <para>
/// UI 스레드 접근: WPF ObservableCollection 은 변경 알림이 UI 스레드에서 발생해야
/// 하므로 <see cref="Application.Current.Dispatcher"/> 로 마샬링한다.
/// </para>
/// </summary>
public sealed class LiveTagAggregator
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly Dictionary<string, LiveTagRow> _index = new();
    private readonly Dictionary<string, string>      _collectorNames = new();

    // §2 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 Tag 목록 (UI 바인딩 대상)</summary>
    public ObservableCollection<LiveTagRow> Rows { get; } = new();

    // §3 ─ Collector 이름 등록 ─────────────────────────────

    /// <summary>
    /// CollectorId → 표시 이름 매핑을 등록/갱신한다.
    /// CollectorConnectionManager 가 Sync 시마다 호출하여 최신 이름을 유지한다.
    /// </summary>
    public void RegisterCollectorName(string collectorId, string name)
    {
        _collectorNames[collectorId] = name;

        // 이미 생성된 행이 있다면 표시 이름도 함께 갱신 (그룹 헤더 즉시 반영)
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var row in Rows)
                if (row.CollectorId == collectorId)
                    row.CollectorName = name;
        });
    }

    // §4 ─ TagValue 수신 처리 ──────────────────────────────

    /// <summary>
    /// CollectorConnection 으로부터 "TagValue" 이벤트를 전달받아 Rows 에 반영한다.
    /// </summary>
    /// <param name="collectorId">발신 HubConnection 의 CollectorId (연결 기준 태깅)</param>
    /// <param name="data">SignalR payload (tagId/plcId/rawValue/engValue/unit/quality/ts)</param>
    public void OnTagValueReceived(string collectorId, JsonElement data)
    {
        try
        {
            var tagId    = data.GetProperty("tagId").GetString() ?? "";
            var plcId    = data.GetProperty("plcId").GetString() ?? "";
            var rawValue = data.TryGetProperty("rawValue", out var rv) ? rv.GetDouble() : 0.0;
            var engValue = data.TryGetProperty("engValue", out var ev) ? ev.GetDouble() : 0.0;
            var unit     = data.TryGetProperty("unit",     out var u)  ? (u.GetString()  ?? "") : "";
            var quality  = data.TryGetProperty("quality",  out var q)  ? (q.GetString()  ?? "Good") : "Good";
            var updatedAt = data.TryGetProperty("ts", out var tsEl)
                             && DateTimeOffset.TryParse(tsEl.GetString(), out var parsed)
                                ? parsed
                                : DateTimeOffset.UtcNow;

            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(plcId))
                return;

            var key = $"{collectorId}:{plcId}:{tagId}";

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_index.TryGetValue(key, out var row))
                {
                    row.RawValue  = rawValue;
                    row.EngValue  = engValue;
                    row.Unit      = unit;
                    row.Quality   = quality;
                    row.UpdatedAt = updatedAt;
                }
                else
                {
                    var newRow = new LiveTagRow
                    {
                        CollectorId   = collectorId,
                        CollectorName = _collectorNames.GetValueOrDefault(collectorId, collectorId),
                        PlcId         = plcId,
                        TagId         = tagId,
                        RawValue      = rawValue,
                        EngValue      = engValue,
                        Unit          = unit,
                        Quality       = quality,
                        UpdatedAt     = updatedAt
                    };
                    _index[key] = newRow;
                    Rows.Add(newRow);
                }
            });
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("LiveTagAggregator", $"TagValue payload 파싱 실패: {ex.Message}");
        }
    }
}
