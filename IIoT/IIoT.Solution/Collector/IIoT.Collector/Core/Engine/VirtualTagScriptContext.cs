// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/VirtualTagScriptContext.cs
//  역할: Function 노드 — 가상 Tag Roslyn C# 고급 스크립트 전역(globals) 컨텍스트
//        VirtualTagEngine 이 CSharpScript.Create<object>(code, ..., globalsType:
//        typeof(VirtualTagScriptContext)) 로 컴파일할 때 이 클래스의 인스턴스를
//        스크립트 실행 시 globals 로 전달한다.
//        스크립트 코드 안에서는 이 클래스의 public 멤버를 한정자 없이 바로 참조
//        가능하다 (Roslyn 스크립팅의 globals 바인딩 규칙).
//
//  ━━━ 스크립트 작성 예시 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Result = Values["T001"] + Values["T002"] * 0.5;
//  if (Values["T001"] < 0) Suppress = true;
//
//  S-Virtual02: 신규
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Engine;

/// <summary>
/// Roslyn C# 스크립트 전역 컨텍스트 (Function 노드 — S-Virtual02).
/// </summary>
public sealed class VirtualTagScriptContext
{
    /// <summary>TagId → 최신 공학값 스냅샷 (실제 + 가상 Tag 전체, 평가 시점 복사본).
    /// 스크립트에서 존재하지 않는 TagId 로 조회하면 예외가 발생하며,
    /// VirtualTagEngine 이 이를 잡아 이번 주기 평가만 건너뛴다(엔진 중단 없음).</summary>
    public IReadOnlyDictionary<string, double> Values { get; init; }
        = new Dictionary<string, double>();

    /// <summary>스크립트가 계산한 최종 출력값 — 이 Tag 의 새 값으로 발행된다.</summary>
    public double Result { get; set; }

    /// <summary>true 로 설정하면 이번 주기 값 발행을 생략한다(이상값 필터링 등 용도).</summary>
    public bool Suppress { get; set; }
}
