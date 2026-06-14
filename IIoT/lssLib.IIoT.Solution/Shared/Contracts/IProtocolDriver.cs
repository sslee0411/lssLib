// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Contracts/IProtocolDriver.cs
//  역할: 프로토콜 드라이버·DB 공통 인터페이스
//  Phase C: 완성본
// ══════════════════════════════════════════════════════════

using IIoT.Shared.Models;

namespace IIoT.Shared.Contracts;

// §1 ─ 배치 읽기 결과 ─────────────────────────────────────
public sealed class BatchReadResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMsg { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, double> Values { get; init; }
        = new Dictionary<string, double>();

    public static BatchReadResult Ok(Dictionary<string, double> values) =>
        new() { IsSuccess = true, Values = values };
    public static BatchReadResult Fail(string error) =>
        new() { IsSuccess = false, ErrorMsg = error };
}

// §2 ─ 태그 주소 정의 ─────────────────────────────────────
/// <summary>드라이버에 전달할 단일 태그 읽기 요청</summary>
public sealed record TagAddressDef(
    string TagId,
    string Address,          // "1:40001" (DeviceId:Register)
    string DataType = "FloatBE",
    string Unit = "",
    int PollMs = 1000);

// §3 ─ 프로토콜 드라이버 인터페이스 ──────────────────────
/// <summary>
/// 프로토콜 드라이버 공통 인터페이스.
/// 구현체: ModbusTcpDriver / VirtualDriver (Phase A)
/// </summary>
public interface IProtocolDriver : IAsyncDisposable
{
    // §3-1 ─ 식별 / 상태 ──────────────────────────────────
    string DriverId { get; }
    bool IsConnected { get; }

    // §3-2 ─ 연결 ─────────────────────────────────────────
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // §3-3 ─ 읽기/쓰기 ────────────────────────────────────
    Task<BatchReadResult> ReadBatchAsync(
        IReadOnlyList<TagAddressDef> tagDefs,
        CancellationToken ct = default);

    Task WriteTagAsync(string tagId, object value,
        CancellationToken ct = default);

    // §3-4 ─ 이벤트 ───────────────────────────────────────
    event Action<bool>? ConnectionChanged;
}

// §4 ─ DB 인터페이스 ──────────────────────────────────────
/// <summary>태그 이력 DB 저장·조회 인터페이스 (SQLite 구현)</summary>
public interface ITagHistoryDb : IAsyncDisposable
{
    Task InsertAsync(TagValue value, CancellationToken ct = default);
    Task InsertBatchAsync(IEnumerable<TagValue> values, CancellationToken ct = default);
    Task<IReadOnlyList<TagValue>> QueryAsync(
        string tagId, DateTime from, DateTime to,
        CancellationToken ct = default);
    Task<int> GetCountAsync(string tagId, CancellationToken ct = default);
}