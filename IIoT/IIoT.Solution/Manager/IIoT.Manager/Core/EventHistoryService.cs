// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/EventHistoryService.cs
//  역할: 프로세스 이벤트 이력 수집 (시작/정지/재시작/자동복구/상태변경)
//        대시보드 [최근 이벤트] 목록의 데이터 소스
//  MG-05: 신규
//  설계 메모:
//    - 모든 기록 호출은 UI 스레드에서 발생 (커맨드·DispatcherTimer 갱신 경로)
//      → ObservableCollection 직접 조작 안전 (마샬링 불필요)
//    - 최대 200건 유지 (초과 시 오래된 것부터 제거)
//    - 이력은 메모리 전용 — 영구 저장이 필요해지면 후속 Step 에서
//      lssLib.DB.Sqlite 연동 (Monitor MN-EX-02 AlarmHistoryService 패턴)
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using System.Collections.ObjectModel;

namespace IIoT.Manager.Core;

/// <summary>프로세스 이벤트 이력 서비스 (DI 싱글턴).</summary>
public sealed class EventHistoryService
{
    // §1 ─ 상수 ──────────────────────────────────────────────

    private const int _maxEvents = 200;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>이벤트 이력 (최신이 맨 위 — Insert(0))</summary>
    public ObservableCollection<EventRow> Events { get; } = [];

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>이벤트 1건을 기록한다 (UI 스레드에서 호출할 것).</summary>
    public void Record(string program, string text)
    {
        Events.Insert(0, new EventRow(DateTime.Now.ToString("HH:mm:ss"), program, text));

        while (Events.Count > _maxEvents)
            Events.RemoveAt(Events.Count - 1);

        // 로그에도 남긴다 (통합 로그 탭·파일에서 함께 추적 가능)
        lssLib.Log.LogManager.Instance.Info("Event", $"{program} — {text}");
    }
}
