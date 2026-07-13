// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Models/ScheduleEntry.cs
//  역할: 스케줄 1건 정의 (manager.json Schedules[] 직렬화 DTO)
//  MG-07: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)

namespace IIoT.Manager.Models;

/// <summary>스케줄 동작 종류.</summary>
public enum ScheduleAction
{
    /// <summary>▶ 시작</summary>
    Start,

    /// <summary>⏹ 정지</summary>
    Stop,

    /// <summary>🔄 재시작</summary>
    Restart
}

/// <summary>
/// 스케줄 1건 — "지정 요일의 지정 시각에 대상 프로그램을 시작/정지/재시작".
/// </summary>
public sealed class ScheduleEntry
{
    // §1 ─ 속성 ──────────────────────────────────────────────

    /// <summary>고유 ID (자동 생성)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>활성화 여부</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>대상 프로그램 Id (manager.json Processes[].Id — 예: "collector")</summary>
    public string ProcessId { get; set; } = "";

    /// <summary>동작 (Start / Stop / Restart)</summary>
    public ScheduleAction Action { get; set; } = ScheduleAction.Restart;

    /// <summary>실행 시각 "HH:mm" (24시간)</summary>
    public string Time { get; set; } = "06:00";

    /// <summary>
    /// 실행 요일 — (int)DayOfWeek 값 목록 (0=일 … 6=토). 기본: 매일.
    /// </summary>
    public List<int> Days { get; set; } = [0, 1, 2, 3, 4, 5, 6];

    // §2 ─ 표시 도우미 ────────────────────────────────────────

    private static readonly string[] _dayNames = ["일", "월", "화", "수", "목", "금", "토"];

    /// <summary>요일 표시 문구 (예: "매일" / "월·화·수")</summary>
    public string DaysText =>
        Days.Count >= 7
            ? "매일"
            : string.Join("·", Days.OrderBy(d => d)
                                   .Where(d => d is >= 0 and <= 6)
                                   .Select(d => _dayNames[d]));

    /// <summary>동작 표시 문구</summary>
    public string ActionText => Action switch
    {
        ScheduleAction.Start => "시작",
        ScheduleAction.Stop  => "정지",
        _                    => "재시작",
    };
}
