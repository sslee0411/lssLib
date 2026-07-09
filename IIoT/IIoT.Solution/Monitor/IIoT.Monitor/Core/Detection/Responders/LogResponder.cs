// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/Responders/LogResponder.cs
//  역할: IDetectionResponder 예시 구현체 — 트리거/해제를 lssLib.Log 파일에 기록.
//        NotifyResponder(이메일/Webhook) 등 다른 대응은 동일한 인터페이스로
//        추가하면 되며, 여러 Responder 를 동시에 등록해 조합할 수 있다.
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using lssLib.Log;

namespace IIoT.Monitor.Core.Detection.Responders;

/// <summary>감지 결과를 파일 로그(lssLib.Log)에 기록하는 대응 동작.</summary>
public sealed class LogResponder : IDetectionResponder
{
    public void OnTriggered(DetectionResult result, AbstractDetector source)
    {
        var message = $"[{source.Name}] 트리거 — {result.Reason}";

        switch (result.Severity)
        {
            case DetectionSeverity.Critical:
                LogManager.Instance.Error("Detector", message);
                break;
            case DetectionSeverity.Warning:
                LogManager.Instance.Warn("Detector", message);
                break;
            default:
                LogManager.Instance.Info("Detector", message);
                break;
        }
    }

    public void OnCleared(DetectionResult result, AbstractDetector source)
    {
        LogManager.Instance.Info("Detector", $"[{source.Name}] 해제됨 (Tag={result.TagId})");
    }
}
