// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Models/ProtocolFieldTagId.cs
//  역할: 프로토콜 블록 필드 → 합성 TagId 생성 규칙 (단일 지점)
//        CollectorConfigLoader(placeholder Tag 생성)와 FlowEngine(값 발행)이
//        동일한 규칙으로 TagId 를 만들어야 서로 매칭되므로 공용 헬퍼로 분리.
//  S-프로토콜01 Step B: 신규
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Models;

/// <summary>프로토콜 블록 필드의 합성 TagId 생성 규칙.</summary>
public static class ProtocolFieldTagId
{
    /// <summary>plcId/blockId/fieldId 조합으로 결정적(deterministic) TagId 를 만든다.</summary>
    public static string Make(string plcId, string blockId, string fieldId)
        => $"proto:{plcId}:{blockId}:{fieldId}";
}
