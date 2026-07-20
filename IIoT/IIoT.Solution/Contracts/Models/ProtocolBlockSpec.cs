// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Models/ProtocolBlockSpec.cs
//  역할: 프로토콜 블록(읽기/쓰기 N개) 실행용 스펙 모델
//        Studio 프로토콜 라이브러리(ProtocolEntry/ProtocolBlock/ProtocolField)를
//        Collector 가 실행 가능한 형태로 평탄화한 결과 — IBlockProtocolDriver 입력
//  S-프로토콜01 Step B: 신규
//  S-프로토콜01 Step B 후속(2026-07-20): ScaleEntryId 추가 — CollectorConfigLoader 가
//    합성하는 placeholder TagRuntimeConfig 에 그대로 전달되어 FlowEngine 이 일반
//    Tag 와 동일하게 ScaleEngine.Apply() 로 Raw→공학단위 변환을 적용할 수 있게 함.
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

// §1 ─ 필드 스펙 ──────────────────────────────────────────

/// <summary>
/// 블록 안의 개별 필드(값) 1건. 버퍼 내 위치와 파싱 방법을 정의한다.
/// </summary>
/// <param name="Id">필드 고유 ID (device.json ProtocolFieldDto.Id) — 값 매핑 키로 사용</param>
/// <param name="Name">필드 표시 이름</param>
/// <param name="ByteOffset">버퍼 내 오프셋 — 표준 블록은 워드(2바이트) 오프셋, 커스텀 프레임은 바이트 오프셋</param>
/// <param name="BufType">데이터 타입 (예: "UInt16", "Float32")</param>
/// <param name="Unit">공학 단위 (표시용)</param>
/// <param name="ScaleEntryId">스케일 라이브러리 참조 ID(문자열, GUID) — null/빈 값이면 Raw 그대로 발행</param>
public sealed record ProtocolFieldSpec(
    string  Id,
    string  Name,
    int     ByteOffset,
    string  BufType,
    string  Unit,
    string? ScaleEntryId = null
);

// §2 ─ 블록 스펙 ──────────────────────────────────────────

/// <summary>
/// 읽기/쓰기 블록 1건의 실행 스펙.
/// <para>
/// CmdCode 가 비어있으면 표준 주소범위 블록(기존 드라이버가 StartAddress/Length
/// 로 레지스터를 직접 읽음), 채워져 있으면 커스텀 프레임 블록(STX/LEN/CMD/CRC
/// 프레임을 구성해 통신하는 IIoT.Driver.RawFrame 전용)이다.
/// </para>
/// <para>
/// UseFraming/StxHex/HasLengthField/CrcType 은 원래 ProtocolEntry(프로토콜 항목)
/// 레벨 설정이지만, 드라이버가 매번 상위 Entry 를 조회하지 않도록 블록 단위로
/// 평탄화(비정규화)하여 담아 전달한다.
/// </para>
/// </summary>
public sealed record ProtocolBlockSpec(
    string                          Id,
    string                          Name,
    string                          StartAddress,
    int                             Length,
    string                          CmdCode,
    IReadOnlyList<ProtocolFieldSpec> Fields,
    bool                            UseFraming     = false,
    string                          StxHex         = "AA",
    bool                            HasLengthField = true,
    string                          CrcType        = "None"
)
{
    /// <summary>CmdCode 가 비어있으면 표준 주소범위 블록.</summary>
    public bool IsStandardBlock => string.IsNullOrWhiteSpace(CmdCode);
}

// §3 ─ 블록 읽기/쓰기 결과 ────────────────────────────────

/// <summary>
/// IBlockProtocolDriver.ReadBlockAsync() 반환값.
/// FieldValues 는 ProtocolFieldSpec.Id → 파싱된 원시값(raw) 매핑.
/// </summary>
public sealed record BlockReadResult
{
    public static BlockReadResult Ok(IReadOnlyDictionary<string, object?> values)
        => new() { IsSuccess = true, FieldValues = values };

    public static BlockReadResult Fail(string error)
        => new() { IsSuccess = false, Error = error };

    public bool                                IsSuccess    { get; init; }
    public IReadOnlyDictionary<string, object?>? FieldValues { get; init; }
    public string?                              Error        { get; init; }
}

/// <summary>
/// IBlockProtocolDriver.WriteBlockAsync() 반환값.
/// </summary>
public sealed record BlockWriteResult
{
    public static BlockWriteResult Ok()
        => new() { IsSuccess = true };

    public static BlockWriteResult Fail(string error)
        => new() { IsSuccess = false, Error = error };

    public bool    IsSuccess { get; init; }
    public string? Error     { get; init; }
}
