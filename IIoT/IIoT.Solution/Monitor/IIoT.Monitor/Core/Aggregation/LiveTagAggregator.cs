// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Aggregation/LiveTagAggregator.cs
//  역할: 모든 CollectorConnection 이 수신한 "TagValue" 이벤트를
//        하나의 ObservableCollection<LiveTagRow> 로 병합한다 (DI 싱글턴).
//        [태그현황] 탭(LiveTagView)이 이 컬렉션을 그대로 바인딩한다.
//  MN-02: 신규
//  FIX(2026-07-08): Dispatcher.Invoke(동기·블로킹) → Dispatcher.BeginInvoke(비동기)
//                   로 변경. 앱 종료 시 UI 스레드가 App.OnExit()의 블로킹 대기
//                   (CollectorConnectionManager.DisposeAsync().GetAwaiter()
//                   .GetResult())로 막혀있는 상태에서, Hub 종료 중 마지막
//                   TagValue 이벤트가 이 메서드를 호출하며 Dispatcher.Invoke로
//                   또 UI 스레드를 기다리면 서로 영원히 기다리는 교착상태
//                   (디버깅 중 창을 닫아도 프로세스가 종료되지 않는 증상)가
//                   발생했다. BeginInvoke는 UI 스레드가 비어있을 때 처리되도록
//                   큐에 넣고 즉시 반환되므로 이 교착상태가 원천적으로 발생하지 않는다.
//  MN-EX-05: FavoriteTagService 주입 — 신규 행 생성 시 저장된 즐겨찾기 상태 복원
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-05)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Favorites;
using IIoT.Monitor.Models;
using lssLib.Log;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

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
/// ★ 반드시 BeginInvoke(비동기) 사용 — Invoke(동기)는 앱 종료 시 교착상태를 유발함.
/// </para>
/// </summary>
public sealed class LiveTagAggregator
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly Dictionary<string, LiveTagRow> _index = new();
    private readonly Dictionary<string, string>      _collectorNames = new();
    private readonly FavoriteTagService               _favoriteService;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public LiveTagAggregator(FavoriteTagService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    // §3 ─ 공개 상태 ───────────────────────────────────────

    /// <summary>전체 Collector 통합 실시간 Tag 목록 (UI 바인딩 대상)</summary>
    public ObservableCollection<LiveTagRow> Rows { get; } = new();

    // §4 ─ Collector 이름 등록 ─────────────────────────────

    /// <summary>
    /// CollectorId → 표시 이름 매핑을 등록/갱신한다.
    /// CollectorConnectionManager 가 Sync 시마다 호출하여 최신 이름을 유지한다.
    /// </summary>
    public void RegisterCollectorName(string collectorId, string name)
    {
        _collectorNames[collectorId] = name;

        // 이미 생성된 행이 있다면 표시 이름도 함께 갱신 (그룹 헤더 즉시 반영)
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            foreach (var row in Rows)
                if (row.CollectorId == collectorId)
                    row.CollectorName = name;
        }, DispatcherPriority.Background);
    }

    // §5 ─ TagValue 수신 처리 ──────────────────────────────

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

            // ★ FIX: BeginInvoke(비동기) — Invoke(동기)는 앱 종료 시 교착상태 유발
            Application.Current?.Dispatcher.BeginInvoke(() =>
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

                    // ★ MN-EX-05: 저장된 즐겨찾기 상태 복원 (Add 하기 전에 적용해도 무방)
                    _favoriteService.ApplyFavoriteState(newRow);

                    Rows.Add(newRow);
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("LiveTagAggregator", $"TagValue payload 파싱 실패: {ex.Message}");
        }
    }
}
