// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/TagTemplate.cs
//  역할: OPC 스타일 BufSchema 기반 태그 템플릿 모델
//        여러 Tag를 하나의 버퍼 스키마로 묶어
//        시작주소만 입력하면 주소 자동 계산
//  S-13B: 초기 구현
//  생성: 2026-06-18
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Models;

// §1 ─ 템플릿 정의 ────────────────────────────────────────

/// <summary>
/// 태그 템플릿 — 여러 Tag를 하나의 BufSchema 블록으로 정의.
/// 예: "펌프 상태 패킷" = 운전상태(UInt16) + 토출압력(FloatLE) + 유량(FloatLE)
/// </summary>
public sealed class TagTemplate
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>총 버퍼 바이트 수 (자동 계산 가능)</summary>
    public int TotalBytes     { get; set; }

    public List<TagTemplateItem> Items { get; set; } = new();

    /// <summary>총 Modbus 레지스터 수 (TotalBytes / 2, 올림)</summary>
    public int TotalRegisters => (TotalBytes + 1) / 2;
}

// §2 ─ 템플릿 항목 ────────────────────────────────────────

/// <summary>
/// 템플릿 내 단일 Tag 항목.
/// ByteOffset + BufType으로 버퍼 내 위치와 파싱 방법을 정의.
/// </summary>
public sealed class TagTemplateItem
{
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>버퍼 내 시작 바이트 오프셋 (0-based)</summary>
    public int    ByteOffset  { get; set; }

    /// <summary>lssLib BufType 이름 (UInt16, FloatLE, Int16LE 등)</summary>
    public string BufType     { get; set; } = "FloatLE";

    /// <summary>공학단위 (예: bar, °C, m³/h)</summary>
    public string Unit        { get; set; } = string.Empty;

    /// <summary>스케일 최솟값 (Raw→공학단위 변환용)</summary>
    public double ScaleMin    { get; set; }

    /// <summary>스케일 최댓값</summary>
    public double ScaleMax    { get; set; } = 100;

    // §2-1 ─ 주소 계산 헬퍼 ──────────────────────────────────

    /// <summary>
    /// Modbus 레지스터 바이트 크기.
    /// BufType에 따라 결정: UInt16/Int16=2, Float/Int32=4, Double=8
    /// </summary>
    public int ByteSize => BufType switch
    {
        "UInt16" or "Int16" or "UInt16BE" or "Int16BE"
            or "UInt16LE" or "Int16LE"       => 2,
        "FloatLE" or "FloatBE"
            or "Int32LE" or "Int32BE"
            or "UInt32LE" or "UInt32BE"      => 4,
        "DoubleBE" or "DoubleLE"             => 8,
        "Bool" or "UInt8" or "Int8"          => 2,  // Modbus 최소 1레지스터
        _                                    => 2
    };

    /// <summary>
    /// 시작주소 기준 이 항목의 Modbus 레지스터 주소.
    /// 예) 시작주소=40001, ByteOffset=4 → 40001 + 4/2 = 40003
    /// </summary>
    public int CalcAddress(int startAddress) =>
        startAddress + ByteOffset / 2;
}
