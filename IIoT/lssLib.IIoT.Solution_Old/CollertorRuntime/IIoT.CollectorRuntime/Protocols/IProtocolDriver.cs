// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Protocols/IProtocolDriver.cs
//  역할: 프로토콜 드라이버 공통 인터페이스
//        Modbus TCP / RTU / Virtual 등 모든 드라이버가 구현
//  Phase 8: 신규
// ══════════════════════════════════════════════════════════

namespace IIoT.CollectorRuntime.Protocols;

// ── 드라이버 결과 ─────────────────────────────────────────

/// <summary>단일 태그 읽기 결과</summary>
public sealed class TagReadResult
{
    public bool     IsSuccess  { get; init; }
    public string   TagId      { get; init; } = string.Empty;
    public double   Value      { get; init; }
    public DateTime Timestamp  { get; init; } = DateTime.Now;
    public string   ErrorMsg   { get; init; } = string.Empty;

    public static TagReadResult Ok(string tagId, double value) =>
        new() { IsSuccess = true, TagId = tagId, Value = value };

    public static TagReadResult Fail(string tagId, string error) =>
        new() { IsSuccess = false, TagId = tagId, ErrorMsg = error };
}

/// <summary>배치(다중 태그) 읽기 결과</summary>
public sealed class BatchReadResult
{
    public bool   IsSuccess { get; init; }
    public string ErrorMsg  { get; init; } = string.Empty;

    /// <summary>tagId → 값 딕셔너리 (성공한 태그만 포함)</summary>
    public IReadOnlyDictionary<string, double> Values { get; init; }
        = new Dictionary<string, double>();

    public static BatchReadResult Ok(Dictionary<string, double> values) =>
        new() { IsSuccess = true, Values = values };

    public static BatchReadResult Fail(string error) =>
        new() { IsSuccess = false, ErrorMsg = error };
}

// ── 태그 주소 정의 ────────────────────────────────────────

/// <summary>
/// 드라이버에 전달할 단일 태그 읽기 요청.
/// 프로토콜마다 Address 해석이 다릅니다:
///   Modbus TCP/RTU : "40001" → Holding Register 1번
///                    "10001" → Input Register 1번 (Coil은 "00001")
///   OPC-UA         : "ns=2;s=Channel1.Device1.Tag1"
///   Virtual        : "sim:SIN/100/10"  (sin파, 주기100s, 진폭10)
/// </summary>
public sealed record TagAddressDef(
    string TagId,
    string Address,
    string Unit     = "",
    int    PollMs   = 1000);

// ── 인터페이스 ────────────────────────────────────────────

/// <summary>
/// 프로토콜 드라이버 공통 인터페이스.
///
/// 구현 클래스:
///   · ModbusTcpDriver  — lssLib.Net TCP Transport
///   · ModbusRtuDriver  — lssLib.Net Serial Transport
///   · VirtualDriver    — 시뮬레이터 (테스트·오프라인)
///
/// CollectionEngine → IProtocolDriver.ReadBatchAsync() → TagValue 수집
/// </summary>
public interface IProtocolDriver : IAsyncDisposable
{
    /// <summary>드라이버 식별자 (장비 ID)</summary>
    string DriverId { get; }

    /// <summary>현재 연결 상태</summary>
    bool IsConnected { get; }

    /// <summary>연결 (수집 시작 전 호출)</summary>
    Task<bool> ConnectAsync(CancellationToken ct = default);

    /// <summary>연결 해제</summary>
    Task DisconnectAsync();

    /// <summary>
    /// 단일 태그 읽기.
    /// 기본 구현은 ReadBatchAsync 로 위임합니다.
    /// </summary>
    Task<TagReadResult> ReadAsync(TagAddressDef tag, CancellationToken ct = default);

    /// <summary>
    /// 배치(다중 태그) 읽기 — 1회 통신으로 여러 태그 수집.
    /// Modbus 의 경우 연속 레지스터를 단일 요청으로 묶어 전송합니다.
    /// </summary>
    Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags,
        CancellationToken ct = default);
}
