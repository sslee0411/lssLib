// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Models/EventRow.cs
//  역할: 대시보드 "최근 이벤트" 1행 모델
//  MG-05: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

namespace IIoT.Manager.Models;

/// <summary>대시보드 이벤트 이력 1건.</summary>
/// <param name="TimeText">발생 시각 (HH:mm:ss)</param>
/// <param name="Program">프로그램 이름</param>
/// <param name="Text">이벤트 내용 (예: "수동 시작", "상태 변경: 실행 중 → 정지")</param>
public sealed record EventRow(string TimeText, string Program, string Text);
