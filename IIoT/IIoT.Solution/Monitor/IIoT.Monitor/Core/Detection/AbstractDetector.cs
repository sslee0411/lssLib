// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/AbstractDetector.cs
//  역할: 커스텀 "이상작업(감지)" 확장의 추상 베이스 클래스.
//        요구사항 3-1(수집 데이터로 이상작업 진행) / 3-2(상속·확장으로
//        실사용 쉽게 커스텀) 을 구현하는 확장점.
//
//        파생 클래스는 Evaluate() 하나만 구현하면 된다 — 연속 트리거 방지
//        (IsTriggered 상태 관리), Responder 통보는 이 클래스가 전담한다.
//
//        파생 클래스 구현 패턴:
//        public sealed class MyDetector : AbstractDetector
//        {
//            public MyDetector() : base("MyDetector") { }
//            protected override DetectionResult? Evaluate(LiveTagRow tag)
//            {
//                if (tag.TagId != "T001") return null;   // 대상 아님 → 무시
//                var triggered = tag.EngValue > 100;
//                return new DetectionResult(triggered, "값 초과", DetectionSeverity.Warning,
//                                            tag.TagId, tag.UpdatedAt);
//            }
//        }
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;

namespace IIoT.Monitor.Core.Detection;

/// <summary>
/// 커스텀 이상감지(Detector)의 추상 베이스.
/// <para>
/// DetectorHost 가 Tag 값이 갱신될 때마다 <see cref="Process"/> 를 호출한다.
/// 파생 클래스는 <see cref="Evaluate"/> 만 override 하면 되고,
/// 같은 조건이 매번 재트리거되지 않도록 하는 상태 관리는 이 클래스가 책임진다.
/// </para>
/// </summary>
public abstract class AbstractDetector
{
    /// <summary>감지기 식별 이름 (로그·UI 표시용)</summary>
    public string Name { get; }

    /// <summary>현재 트리거(활성) 상태 여부</summary>
    public bool IsTriggered { get; private set; }

    protected AbstractDetector(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 파생 클래스가 구현하는 핵심 판정 로직.
    /// <para>
    /// 이 Tag 가 이 감지기의 대상이 아니면 <c>null</c> 을 반환한다(무시).
    /// 대상이면 <see cref="DetectionResult"/> 를 반환하여 트리거 여부를 알린다.
    /// 복합 조건(여러 Tag 조합)이 필요하면 파생 클래스 내부에 마지막 값들을
    /// 필드로 캐싱해 두고 여기서 함께 평가하면 된다.
    /// </para>
    /// </summary>
    protected abstract DetectionResult? Evaluate(LiveTagRow tag);

    /// <summary>
    /// DetectorHost 가 Tag 값 갱신마다 호출한다.
    /// IsTriggered 상태가 실제로 바뀔 때만 Responder 에 통보한다(중복 알림 방지).
    /// </summary>
    internal void Process(LiveTagRow tag, IReadOnlyList<IDetectionResponder> responders)
    {
        var result = Evaluate(tag);
        if (result is null)
            return;

        if (result.IsTriggered && !IsTriggered)
        {
            IsTriggered = true;
            foreach (var responder in responders)
                responder.OnTriggered(result, this);
        }
        else if (!result.IsTriggered && IsTriggered)
        {
            IsTriggered = false;
            foreach (var responder in responders)
                responder.OnCleared(result, this);
        }
    }
}
