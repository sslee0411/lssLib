// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Config/DeviceConfigDto.cs
//  역할: device.json 역직렬화 전용 DTO
//        Studio 의 DeviceConfigDto.cs 와 필드 1:1 동일 (읽기 전용 사본)
//        ★ Collector 는 Studio 프로젝트를 직접 참조하지 않으므로
//          (서로 독립 실행 프로그램) DTO 를 동일하게 복제 보관한다.
//          필드 추가/변경 시 Studio DeviceConfigDto.cs 와 반드시 동기화.
//  C-01: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Core.Config;

// §1 ─ 루트 컨테이너 ──────────────────────────────────────

public sealed class DeviceConfigRoot
{
    public string   Version   { get; set; } = "1.0";
    public DateTime SavedAt   { get; set; } = DateTime.Now;
    public string   Sha256    { get; set; } = string.Empty;

    public string               ChangeMemo  { get; set; } = string.Empty;
    public List<SaveHistoryDto> SaveHistory { get; set; } = new();

    public List<DeviceNodeDto> Tree         { get; set; } = new();
    public List<ScaleEntryDto> ScaleLibrary { get; set; } = new();
    public List<AlarmEntryDto> AlarmLibrary { get; set; } = new();
    public List<CommEntryDto>  CommLibrary  { get; set; } = new();
}

public sealed class SaveHistoryDto
{
    public string SavedAt { get; set; } = string.Empty;
    public string Memo    { get; set; } = string.Empty;
}

// §2 ─ 트리 노드 DTO ──────────────────────────────────────

public sealed class DeviceNodeDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  NodeType    { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;

    // ── 장비(Device) 전용 ────────────────────────────────
    public string? Model        { get; set; }
    public string? Manufacturer { get; set; }
    public string? Location     { get; set; }

    // ── 통신 (Device / PLC 공통) ─────────────────────────
    public string? CommType { get; set; }
    public string? Host     { get; set; }
    public int?    Port     { get; set; }
    public int?    PollMs   { get; set; }

    /// <summary>PLC 통신 라이브러리 참조 ID (null = 직접 입력)</summary>
    public string? CommEntryId { get; set; }

    /// <summary>
    /// 플러그인 드라이버 ID.
    /// null/"" = 레거시 CommType 방식 (CommTypeMigrator 변환 대상)
    /// 값 있음  = 플러그인 driverId 직접 지정
    /// </summary>
    public string? DriverId { get; set; }

    /// <summary>드라이버 파라미터 (null = 파라미터 없음)</summary>
    public Dictionary<string, string>? DriverParams { get; set; }

    // ── Tag 전용 ─────────────────────────────────────────
    public string? Address      { get; set; }
    public string? DataType     { get; set; }
    public string? Unit         { get; set; }
    public string? ScaleEntryId { get; set; }
    public string? AlarmEntryId { get; set; }
    public string? Memo         { get; set; }

    /// <summary>Tag 수집 활성 여부 (null = true 기본값)</summary>
    public bool? IsEnabled { get; set; }

    public List<DeviceNodeDto> Children { get; set; } = new();
}

// §3 ─ 스케일 DTO ─────────────────────────────────────────

public sealed class ScaleEntryDto
{
    public string Id            { get; set; } = string.Empty;
    public string Name          { get; set; } = string.Empty;
    public string Mode          { get; set; } = "Linear";
    public double RawMin        { get; set; }
    public double RawMax        { get; set; } = 100;
    public double EngMin        { get; set; }
    public double EngMax        { get; set; } = 100;
    public string Expression    { get; set; } = string.Empty;
    public string Unit          { get; set; } = string.Empty;
    public int    DecimalPlaces { get; set; } = 2;
}

// §4 ─ 알람 DTO ───────────────────────────────────────────

public sealed class AlarmEntryDto
{
    public string Id          { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool   HhEnabled { get; set; }
    public double HhValue   { get; set; }
    public string HhMessage { get; set; } = string.Empty;
    public bool   HEnabled  { get; set; }
    public double HValue    { get; set; }
    public string HMessage  { get; set; } = string.Empty;
    public bool   LEnabled  { get; set; }
    public double LValue    { get; set; }
    public string LMessage  { get; set; } = string.Empty;
    public bool   LlEnabled { get; set; }
    public double LlValue   { get; set; }
    public string LlMessage { get; set; } = string.Empty;

    public int DelayMs         { get; set; }
    public int RecoveryDelayMs { get; set; }
    // ★ C-14 신규 — 알림/에스컬레이션 (Studio AlarmEntryDto 와 필드명·순서 동일해야 함)
    public string NotifyEmail { get; set; } = string.Empty;
    public string NotifyPhone { get; set; } = string.Empty;
    public int EscalateMinutes { get; set; }
}

// §5 ─ 통신 DTO ───────────────────────────────────────────

public sealed class CommEntryDto
{
    public string Id          { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type        { get; set; } = "ModbusTcp";

    public string Host    { get; set; } = string.Empty;
    public int    Port    { get; set; } = 502;
}
