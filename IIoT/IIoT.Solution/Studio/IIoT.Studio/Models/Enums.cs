// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/Enums.cs
//  역할: 프로젝트 전체 Enum + 확장 메서드 통합 관리
//
//  포함 Enum 목록:
//    ① NodeCommType  — 트리 노드(장비·PLC) 통신 방식
//    ② ScaleMode     — 스케일 변환 모드
//    ③ PlcVendor     — [S-21A] PLC 제조사 (PlcTreeNode에 설정)
//    ④ RegisterType  — [S-21A] 레지스터 종류 (TagTreeNode에 설정, 제조사 공용)
//
//  설계 원칙:
//    PlcVendor × RegisterType 조합 → 주소 형식 자동 결정
//    예) Mitsubishi + Word  → "D{n}"
//        Mitsubishi + BitX  → "X{n}.{b}"
//        Siemens    + Word  → "DB{db}.DBW{n}"
//        Modbus     + Word  → "{n}" (40001 등)
//
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Models;

// §1 ─ 통신 방식 Enum ─────────────────────────────────────

public enum NodeCommType
{
    None, ModbusTcp, Serial, Mqtt, OpcUa
}

public static class NodeCommTypeExtensions
{
    public static string ToLabel(this NodeCommType t) => t switch
    {
        NodeCommType.None      => "없음",
        NodeCommType.ModbusTcp => "Modbus TCP",
        NodeCommType.Serial    => "Serial",
        NodeCommType.Mqtt      => "MQTT",
        NodeCommType.OpcUa     => "OPC-UA",
        _                      => t.ToString()
    };
}

// §2 ─ 스케일 변환 모드 Enum ──────────────────────────────

public enum ScaleMode
{
    Linear,
    Expression
}

// §3 ─ ★ S-21A: PLC 제조사 Enum ──────────────────────────

/// <summary>
/// PLC 제조사 — PlcTreeNode 에 설정.
/// 제조사 선택에 따라 Tag 편집기의 레지스터 종류 목록이 필터링됨.
/// </summary>
public enum PlcVendor
{
    /// <summary>Modbus 프로토콜 (범용) — 40001, 30001 등</summary>
    Modbus,

    /// <summary>미쓰비시 MELSEC — D, X, Y, W, M, T, C 등</summary>
    Mitsubishi,

    /// <summary>LS산전 XGK/XGI/XGR — %MW, %QX, %IX 등</summary>
    LsSanXgt,

    /// <summary>지멘스 S7/TIA Portal — DB.DBW, DB.DBD 등</summary>
    Siemens,

    /// <summary>오므론 CJ/CS/NX — D, W, H, A, T, C 등</summary>
    Omron,

    /// <summary>자유 입력 — 기타 PLC, 형식 제한 없음</summary>
    Free
}

public static class PlcVendorExtensions
{
    public static string ToLabel(this PlcVendor v) => v switch
    {
        PlcVendor.Modbus    => "Modbus (범용)",
        PlcVendor.Mitsubishi => "미쓰비시 MELSEC",
        PlcVendor.LsSanXgt  => "LS산전 XGT",
        PlcVendor.Siemens   => "지멘스 S7",
        PlcVendor.Omron     => "오므론",
        PlcVendor.Free      => "자유 입력",
        _                   => v.ToString()
    };
}

// §4 ─ ★ S-21A: 레지스터 종류 Enum ───────────────────────

/// <summary>
/// 레지스터 종류 — TagTreeNode 에 설정.
/// 제조사와 독립적인 공용 분류 — PLC 종류에 따라 실제 표기법만 달라짐.
///
/// 지원 조합 예시:
///   Modbus    + HoldingReg → 40001
///   Modbus    + InputReg   → 30001
///   Modbus    + Coil       → 10001
///   Mitsubishi+ Word       → D100
///   Mitsubishi+ BitX       → X0.0
///   Mitsubishi+ BitY       → Y0.0
///   Mitsubishi+ BitM       → M100
///   Mitsubishi+ Timer      → T100
///   Mitsubishi+ Counter    → C100
///   LsSanXgt  + Word       → %MW100
///   LsSanXgt  + BitOut     → %QX0.0
///   LsSanXgt  + BitIn      → %IX0.0
///   Siemens   + Word       → DB1.DBW0
///   Siemens   + DWord      → DB1.DBD0
///   Siemens   + Bit        → DB1.DBX0.0
///   Omron     + Word       → D00100
///   Omron     + Work       → W00100
///   Omron     + Holding    → H00100
///   Free      + Free       → 직접 입력
/// </summary>
public enum RegisterType
{
    // ── 워드/더블워드 (공용) ─────────────────────────────

    /// <summary>
    /// 워드 레지스터 (범용).
    /// Modbus=Holding(40001), Mitsubishi=D, LsSanXgt=%MW, Siemens=DBW, Omron=D
    /// </summary>
    Word,

    /// <summary>
    /// 더블워드 레지스터 (32bit).
    /// Siemens=DBD, Omron=D(float)
    /// </summary>
    DWord,

    // ── 비트 주소 ────────────────────────────────────────

    /// <summary>
    /// 입력 비트.
    /// Mitsubishi=X, LsSanXgt=%IX, Modbus=Input(10001)
    /// </summary>
    BitInput,

    /// <summary>
    /// 출력 비트.
    /// Mitsubishi=Y, LsSanXgt=%QX, Modbus=Coil(00001)
    /// </summary>
    BitOutput,

    /// <summary>
    /// 내부 보조 릴레이/메모리 비트.
    /// Mitsubishi=M, LsSanXgt=%MX, Siemens=DBX
    /// </summary>
    BitMemory,

    // ── 특수 레지스터 ────────────────────────────────────

    /// <summary>링크/공유 메모리 워드. Mitsubishi=W, LsSanXgt=%LW</summary>
    LinkWord,

    /// <summary>타이머 현재값. Mitsubishi=TN, Omron=TIM</summary>
    Timer,

    /// <summary>카운터 현재값. Mitsubishi=CN, Omron=CNT</summary>
    Counter,

    // ── Modbus 전용 ───────────────────────────────────────

    /// <summary>Modbus Holding Register — 40001~49999</summary>
    HoldingReg,

    /// <summary>Modbus Input Register — 30001~39999</summary>
    InputReg,

    /// <summary>Modbus Coil (출력 비트) — 00001~09999</summary>
    Coil,

    /// <summary>Modbus Discrete Input (입력 비트) — 10001~19999</summary>
    DiscreteInput,

    // ── 자유 입력 ────────────────────────────────────────

    /// <summary>자유 입력 — 형식 제한 없음</summary>
    Free
}

public static class RegisterTypeExtensions
{
    /// <summary>레지스터 종류 표시 레이블</summary>
    public static string ToLabel(this RegisterType r) => r switch
    {
        RegisterType.Word         => "워드 (Word)",
        RegisterType.DWord        => "더블워드 (DWord)",
        RegisterType.BitInput     => "입력 비트",
        RegisterType.BitOutput    => "출력 비트",
        RegisterType.BitMemory    => "내부 비트 (M/MX)",
        RegisterType.LinkWord     => "링크 워드 (W)",
        RegisterType.Timer        => "타이머 (T)",
        RegisterType.Counter      => "카운터 (C)",
        RegisterType.HoldingReg   => "Holding Register (4x)",
        RegisterType.InputReg     => "Input Register (3x)",
        RegisterType.Coil         => "Coil (0x)",
        RegisterType.DiscreteInput=> "Discrete Input (1x)",
        RegisterType.Free         => "자유 입력",
        _                         => r.ToString()
    };

    /// <summary>
    /// 해당 제조사에서 지원하는 레지스터 종류 목록.
    /// Tag 편집기 ComboBox에서 제조사별 필터링에 사용.
    /// </summary>
    public static IReadOnlyList<RegisterType> ForVendor(PlcVendor vendor) => vendor switch
    {
        PlcVendor.Modbus => new[]
        {
            RegisterType.HoldingReg, RegisterType.InputReg,
            RegisterType.Coil, RegisterType.DiscreteInput
        },
        PlcVendor.Mitsubishi => new[]
        {
            RegisterType.Word, RegisterType.DWord,
            RegisterType.BitInput, RegisterType.BitOutput, RegisterType.BitMemory,
            RegisterType.LinkWord, RegisterType.Timer, RegisterType.Counter
        },
        PlcVendor.LsSanXgt => new[]
        {
            RegisterType.Word, RegisterType.DWord,
            RegisterType.BitInput, RegisterType.BitOutput, RegisterType.BitMemory,
            RegisterType.LinkWord
        },
        PlcVendor.Siemens => new[]
        {
            RegisterType.Word, RegisterType.DWord, RegisterType.BitMemory
        },
        PlcVendor.Omron => new[]
        {
            RegisterType.Word, RegisterType.DWord,
            RegisterType.Timer, RegisterType.Counter, RegisterType.LinkWord
        },
        _ => new[] { RegisterType.Free }
    };

    /// <summary>비트 주소 여부 (Next() 시 번지.비트 자동 전환)</summary>
    public static bool IsBit(this RegisterType r) =>
        r is RegisterType.BitInput
          or RegisterType.BitOutput
          or RegisterType.BitMemory
          or RegisterType.DiscreteInput
          or RegisterType.Coil;

    /// <summary>일괄 주소 부여 시 기본 간격</summary>
    public static int DefaultStep(this RegisterType r, PlcVendor vendor) => r switch
    {
        RegisterType.HoldingReg or RegisterType.InputReg => 2,  // FloatLE = 2 레지스터
        RegisterType.Word  when vendor == PlcVendor.Mitsubishi => 2, // Float = 2 워드
        RegisterType.Word  when vendor == PlcVendor.Siemens    => 2, // DBW float
        RegisterType.Word  when vendor == PlcVendor.LsSanXgt   => 2,
        RegisterType.DWord => 1, // 더블워드 자체가 32bit
        _ => 1
    };
}
