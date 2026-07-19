// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Models/ForceWriteResult.cs
//  역할: Collector IIoTHub.ForceWrite(plcId,tagId,value,apiKey) 원격 호출의
//        응답 DTO. Collector 측 Core/Engine/ForceWriteService.cs 의
//        "public sealed record ForceWriteResult(bool IsSuccess, string? Error)"
//        와 동일한 필드 구조 — SignalR JsonHubProtocol 기본 직렬화(camelCase)를
//        그대로 왕복하므로 별도 JsonPropertyName 지정 없이 그대로 매핑된다.
//  HM-09: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

namespace IIoT.HMI.Models;

/// <summary>Collector ForceWrite 원격 호출 결과.</summary>
public sealed record ForceWriteResult(bool IsSuccess, string? Error);
