// ══════════════════════════════════════════════════════════
//  IIoT.Shared · Contracts/IProtocolDriver.cs
//  역할: 프로토콜 드라이버 공통 인터페이스
//        Modbus·OPC-UA·TCP·Serial 구현체가 이 인터페이스를 구현
//  V3 Step2: 신규
// ══════════════════════════════════════════════════════════

using IIoT.Shared.Models;

namespace IIoT.Shared.Contracts;

/// <summary>
/// 프로토콜 드라이버 공통 인터페이스.
/// CollectorRuntime의 각 통신 드라이버가 구현합니다.
///
/// <code>
/// // 드라이버 팩토리 패턴
/// IProtocolDriver driver = commType switch {
///     "Modbus"  => new ModbusTcpDriver(config),
///     "OPC-UA"  => new OpcUaDriver(config),
///     "Simul"   => new SimulatorDriver(config),
///     _         => throw new NotSupportedException(commType),
/// };
/// await driver.ConnectAsync();
/// var values = await driver.ReadTagsAsync(tagIds);
/// </code>
/// </summary>
public interface IProtocolDriver : IAsyncDisposable
{
    // §1 ─ 식별 정보 ──────────────────────────────────────────

    /// <summary>드라이버 고유 ID (DeviceName 기반)</summary>
    string DriverId { get; }

    /// <summary>연결 상태</summary>
    bool IsConnected { get; }

    // §2 ─ 연결 ───────────────────────────────────────────────

    /// <summary>장치 연결</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>장치 연결 해제</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    // §3 ─ 읽기/쓰기 ──────────────────────────────────────────

    /// <summary>태그 ID 목록으로 일괄 읽기</summary>
    Task<IReadOnlyList<TagValue>> ReadTagsAsync(
        IReadOnlyList<string> tagIds,
        CancellationToken ct = default);

    /// <summary>단일 태그 쓰기</summary>
    Task WriteTagAsync(string tagId, object value,
        CancellationToken ct = default);

    // §4 ─ 이벤트 ─────────────────────────────────────────────

    /// <summary>연결 상태 변경 이벤트</summary>
    event Action<bool>? ConnectionChanged;
}

/// <summary>
/// DB 태그 이력 저장 인터페이스
/// </summary>
public interface ITagHistoryDb : IAsyncDisposable
{
    Task InsertAsync(TagValue value, CancellationToken ct = default);
    Task InsertBatchAsync(IEnumerable<TagValue> values, CancellationToken ct = default);
    Task<IReadOnlyList<TagValue>> QueryAsync(
        string tagId, DateTime from, DateTime to,
        CancellationToken ct = default);
}
