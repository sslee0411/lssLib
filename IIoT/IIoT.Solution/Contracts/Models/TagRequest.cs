// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Models/TagRequest.cs
//  역할: 태그 읽기·쓰기 요청 + 결과 모델
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

// §1 ─ 태그 읽기 요청 ─────────────────────────────────────

/// <summary>
/// 태그 읽기 요청.
/// IProtocolDriver.ReadTagsAsync() 의 입력으로 사용된다.
/// </summary>
/// <param name="TagId">태그 식별 ID (device.json의 Tag 노드 ID)</param>
/// <param name="Address">레지스터 주소 문자열 (예: "40001", "D100", "X0.1")</param>
/// <param name="DataType">데이터 타입 문자열 (예: "UInt16", "Float32", "Bool")</param>
public sealed record TagReadRequest(
    string TagId,
    string Address,
    string DataType
);

// §2 ─ 태그 쓰기 요청 ─────────────────────────────────────

/// <summary>
/// 태그 쓰기 요청.
/// IProtocolDriver.WriteTagAsync() 의 입력으로 사용된다.
/// </summary>
/// <param name="TagId">태그 식별 ID</param>
/// <param name="Address">레지스터 주소 문자열</param>
/// <param name="DataType">데이터 타입 문자열</param>
/// <param name="Value">쓸 값 (문자열 표현 — 드라이버가 DataType에 맞게 변환)</param>
public sealed record TagWriteRequest(
    string TagId,
    string Address,
    string DataType,
    string Value
);

// §3 ─ 읽기 결과 ──────────────────────────────────────────

/// <summary>태그 단일 읽기 값.</summary>
/// <param name="TagId">태그 ID</param>
/// <param name="RawValue">원시 값 (object — double, bool, string 등)</param>
/// <param name="Quality">품질 코드</param>
/// <param name="Timestamp">수집 시각 (UTC)</param>
public sealed record TagValue(
    string         TagId,
    object?        RawValue,
    TagQuality     Quality,
    DateTimeOffset Timestamp
);

/// <summary>태그 읽기 품질 코드.</summary>
public enum TagQuality
{
    /// <summary>정상 수집</summary>
    Good        = 0,

    /// <summary>값 불량 (통신 오류 응답)</summary>
    Bad         = 1,

    /// <summary>타임아웃</summary>
    Timeout     = 2,

    /// <summary>드라이버 미연결</summary>
    Disconnected = 3
}

/// <summary>
/// IProtocolDriver.ReadTagsAsync() 반환값.
/// Values 와 Error 둘 중 하나가 채워진다.
/// </summary>
public sealed record DriverReadResult
{
    // §3-1 ─ 팩토리 메서드 ────────────────────────────────

    /// <summary>성공 결과 생성.</summary>
    public static DriverReadResult Ok(IReadOnlyList<TagValue> values)
        => new() { Values = values, IsSuccess = true };

    /// <summary>실패 결과 생성.</summary>
    public static DriverReadResult Fail(string error, TagQuality quality = TagQuality.Bad)
        => new() { Error = error, Quality = quality, IsSuccess = false };

    // §3-2 ─ 프로퍼티 ─────────────────────────────────────

    public bool                   IsSuccess { get; init; }
    public IReadOnlyList<TagValue>? Values  { get; init; }
    public string?                Error    { get; init; }
    public TagQuality             Quality  { get; init; }
}

// §4 ─ 쓰기 결과 ──────────────────────────────────────────

/// <summary>
/// IProtocolDriver.WriteTagAsync() 반환값.
/// </summary>
public sealed record DriverWriteResult
{
    /// <summary>성공 결과 생성.</summary>
    public static DriverWriteResult Ok()
        => new() { IsSuccess = true };

    /// <summary>실패 결과 생성.</summary>
    public static DriverWriteResult Fail(string error)
        => new() { IsSuccess = false, Error = error };

    public bool    IsSuccess { get; init; }
    public string? Error     { get; init; }
}
