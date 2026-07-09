// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/IDetectionResponder.cs
//  역할: 감지기(Detector)가 트리거/해제될 때 수행할 "대응" 동작 계약.
//        탐지(판정)와 대응(동작)을 분리하여, 하나의 Detector 결과에
//        여러 Responder(로그·알림·외부호출 등)를 자유롭게 조합할 수 있게 한다.
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

namespace IIoT.Monitor.Core.Detection;

/// <summary>
/// 감지기 트리거/해제 결과에 대한 대응 동작.
/// <para>
/// 구현 예: LogResponder(파일 기록), NotifyResponder(이메일/Webhook — 향후 추가),
/// 사용자 커스텀 Responder(외부 시스템 연동 등).
/// </para>
/// </summary>
public interface IDetectionResponder
{
    /// <summary>감지기가 새로 트리거되었을 때 호출된다 (Cleared→Triggered 전이 1회).</summary>
    void OnTriggered(DetectionResult result, AbstractDetector source);

    /// <summary>감지기 조건이 해제되었을 때 호출된다 (Triggered→Cleared 전이 1회).</summary>
    void OnCleared(DetectionResult result, AbstractDetector source);
}
