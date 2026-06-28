// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/DeviceConfigDto.cs
//  역할: device.json 직렬화 전용 DTO
//  S-10: 초기 구현
//  S-23: DeviceNodeDto에 Memo 필드 추가
//  S-25: DeviceNodeDto에 IsEnabled 필드 추가
//  S-27: DeviceConfigRoot에 ChangeMemo + SaveHistory 추가
//  S-28: DeviceNodeDto에 CommEntryId 필드 추가
//  Studio-P02: DeviceNodeDto에 DriverId / DriverParams 추가
//  생성: 2026-06-17 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Studio.Core.Config;

// §1 ─ 루트 컨테이너 ──────────────────────────────────────

public sealed class DeviceConfigRoot
{
    public string   Version   { get; set; } = "1.0";
    public DateTime SavedAt   { get; set; } = DateTime.Now;
    public string   Sha256    { get; set; } = string.Empty;

    // ★ S-27: 저장 메모 + 이력
    public string               ChangeMemo  { get; set; } = string.Empty;
    public List<SaveHistoryDto> SaveHistory { get; set; } = new();

    public List<DeviceNodeDto> Tree         { get; set; } = new();
    public List<ScaleEntryDto> ScaleLibrary { get; set; } = new();
    public List<AlarmEntryDto> AlarmLibrary { get; set; } = new();
    public List<CommEntryDto>  CommLibrary  { get; set; } = new();
}

// §1-1 ─ 저장 이력 DTO ────────────────────────────────────

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

    // ★ S-28: PLC 통신 라이브러리 참조 ID (null = 직접 입력)
    public string? CommEntryId { get; set; }

    // ★ Studio-P02: 플러그인 드라이버 ID
    //   null / "" = 레거시 CommType 방식 (CommTypeMigrator 변환 대상)
    //   값 있음   = 플러그인 driverId 직접 지정 (신규 방식)
    public string? DriverId { get; set; }

    // ★ Studio-P02: 드라이버 파라미터
    //   null = 파라미터 없음 (JSON에서 생략됨)
    //   로드 시: null → PlcTreeNode.DriverParams = new()
    public Dictionary<string, string>? DriverParams { get; set; }

    // ── Tag 전용 ─────────────────────────────────────────
    public string? Address      { get; set; }
    public string? DataType     { get; set; }
    public string? Unit         { get; set; }
    public string? ScaleEntryId { get; set; }
    public string? AlarmEntryId { get; set; }

    // ★ S-23: Tag 메모 필드
    public string? Memo { get; set; }

    // ★ S-25: Tag 수집 활성 여부 (null = true 기본값, JSON 생략)
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
}

// §5 ─ 통신 DTO ───────────────────────────────────────────

public sealed class CommEntryDto
{
    public string Id          { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type        { get; set; } = "ModbusTcp";

    // Modbus TCP
    public string Host    { get; set; } = string.Empty;
    public int    Port    { get; set; } = 502;
    public int    SlaveId { get; set; } = 1;

    // Serial
    public string ComPort  { get; set; } = string.Empty;
    public int    BaudRate { get; set; } = 9600;
    public string Parity   { get; set; } = "None";
    public int    DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";

    // MQTT
    public string BrokerHost   { get; set; } = string.Empty;
    public int    BrokerPort   { get; set; } = 1883;
    public string ClientId     { get; set; } = string.Empty;
    public string Topic        { get; set; } = string.Empty;
    public bool   UseTls       { get; set; }
    public string MqttUser     { get; set; } = string.Empty;
    public string MqttPassword { get; set; } = string.Empty;

    // OPC-UA
    public string EndpointUrl { get; set; } = string.Empty;
    public string OpcUser     { get; set; } = string.Empty;
    public string OpcPassword { get; set; } = string.Empty;

    // 공통
    public int PollMs          { get; set; } = 1000;
    public int TimeoutMs       { get; set; } = 3000;
    public int RetryIntervalMs { get; set; } = 5000;
}
