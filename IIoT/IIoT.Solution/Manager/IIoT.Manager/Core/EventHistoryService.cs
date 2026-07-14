// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/EventHistoryService.cs
//  역할: 프로세스 이벤트 이력 수집 (시작/정지/재시작/자동복구/상태변경)
//        대시보드 [최근 이벤트] 목록의 데이터 소스
//  MG-05: 신규
//  MG-EX-02: 심각도(EventSeverity) + Recorded 이벤트 추가 —
//            Warning 이벤트는 App 에서 트레이 풍선+사운드 알림으로 연결.
//            Warning 은 로그 레벨도 Warn 으로 기록.
//  설계 메모:
//    - 모든 기록 호출은 UI 스레드에서 발생 (커맨드·DispatcherTimer 갱신 경로)
//      → ObservableCollection 직접 조작 안전 (마샬링 불필요)
//    - 최대 200건 유지 (초과 시 오래된 것부터 제거)
//    - 이력은 메모리 전용 — 영구 저장은 MG-EX-04 에서 SQLite 연동 예정
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-02)
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using System.Collections.ObjectModel;

namespace IIoT.Manager.Core;

/// <summary>★ MG-EX-02: 이벤트 심각도 — Warning 은 트레이 알림 대상.</summary>
public enum EventSeverity
{
    /// <summary>정보 (기록만 — 알림 없음)</summary>
    Info,

    /// <summary>경고 (기록 + 트레이 풍선/사운드 알림)</summary>
    Warning
}

/// <summary>프로세스 이벤트 이력 서비스 (DI 싱글턴).</summary>
public sealed class EventHistoryService
{
    // §1 ─ 상수 ──────────────────────────────────────────────

    private const int _maxEvents = 200;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>이벤트 이력 (최신이 맨 위 — Insert(0))</summary>
    public ObservableCollection<EventRow> Events { get; } = [];

    // §3 ─ 이벤트 (MG-EX-02) ──────────────────────────────────

    /// <summary>이벤트 기록 시 발행 (알림 연결용 — App 에서 구독). UI 스레드.</summary>
    public event Action<EventRow, EventSeverity>? Recorded;

    // §4 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>이벤트 1건을 기록한다 (UI 스레드에서 호출할 것).</summary>
    /// <param name="program">프로그램 이름</param>
    /// <param name="text">이벤트 내용</param>
    /// <param name="severity">★ MG-EX-02: 심각도 (Warning = 트레이 알림 대상)</param>
    public void Record(string program, string text, EventSeverity severity = EventSeverity.Info)
    {
        var row = new EventRow(DateTime.Now.ToString("HH:mm:ss"), program, text);
        Events.Insert(0, row);

        while (Events.Count > _maxEvents)
            Events.RemoveAt(Events.Count - 1);

        // 로그에도 남긴다 (통합 로그 탭·파일에서 함께 추적 가능)
        if (severity == EventSeverity.Warning)
            lssLib.Log.LogManager.Instance.Warn("Event", $"{program} — {text}");
        else
            lssLib.Log.LogManager.Instance.Info("Event", $"{program} — {text}");

        // ★ MG-EX-02: 알림 연결 (App 에서 Warning 만 트레이 알림으로 전달)
        Recorded?.Invoke(row, severity);
    }
}
