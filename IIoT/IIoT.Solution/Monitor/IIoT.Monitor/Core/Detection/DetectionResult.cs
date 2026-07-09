// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/DetectionResult.cs
//  역할: AbstractDetector.Evaluate() 의 판정 결과를 담는 불변 레코드
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

namespace IIoT.Monitor.Core.Detection;

/// <summary>탐지 심각도.</summary>
public enum DetectionSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// 감지기(Detector) 1회 판정 결과.
/// <para>
/// <see cref="IsTriggered"/> 가 이전 판정과 달라질 때만 Responder 로 통보된다
/// (연속 트리거 방지는 AbstractDetector 가 내부 상태로 책임진다).
/// </para>
/// </summary>
/// <param name="IsTriggered">이번 판정에서 조건을 만족했는지 여부</param>
/// <param name="Reason">트리거 사유(사람이 읽을 수 있는 설명, Cleared 시에는 빈 문자열 가능)</param>
/// <param name="Severity">심각도</param>
/// <param name="TagId">판정 대상 Tag ID (복합 조건인 경우 대표 Tag 또는 빈 문자열)</param>
/// <param name="Timestamp">판정 시각</param>
public sealed record DetectionResult(
    bool IsTriggered,
    string Reason,
    DetectionSeverity Severity,
    string TagId,
    DateTimeOffset Timestamp);
