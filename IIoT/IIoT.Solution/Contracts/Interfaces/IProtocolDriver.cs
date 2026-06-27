// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Interfaces/IProtocolDriver.cs
//  역할: 실제 통신 드라이버 인터페이스
//        IProtocolPlugin.CreateDriver() 가 반환하는 실행체
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts;

/// <summary>
/// 프로토콜 드라이버 인터페이스.
/// <para>
/// 각 PLC 제조사 드라이버가 구현하는 통신 실행 인터페이스.
/// ConnectAsync → ReadTagsAsync(폴링) / WriteTagAsync → DisconnectAsync 순서로 사용된다.
/// </para>
/// <example>
/// <code>
/// // Collector 내부 사용 예시
/// IProtocolDriver driver = plugin.CreateDriver();
///
/// bool ok = await driver.ConnectAsync(config, ct);
/// if (!ok) { /* 연결 실패 처리 */ return; }
///
/// // AsyncScheduler 폴링에서
/// var result = await driver.ReadTagsAsync(tags, ct);
/// foreach (var val in result.Values ?? [])
///     EventBus.Instance.Publish(new TagValueUpdatedEvent(val));
/// </code>
/// </example>
/// </summary>
public interface IProtocolDriver : IAsyncDisposable
{
    // §1 ─ 상태 ────────────────────────────────────────────

    /// <summary>드라이버 식별명 (로그·진단 표시용)</summary>
    string DriverName  { get; }

    /// <summary>현재 연결 상태</summary>
    bool   IsConnected { get; }

    // §2 ─ 연결·해제 ───────────────────────────────────────

    /// <summary>
    /// PLC에 비동기 연결합니다.
    /// </summary>
    /// <param name="config">연결 설정 (Host/Port 등 DriverParams 포함)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>연결 성공 여부</returns>
    Task<bool> ConnectAsync(DriverConfig config, CancellationToken ct = default);

    /// <summary>연결을 해제합니다. 이미 끊긴 상태에서도 예외 없음.</summary>
    Task DisconnectAsync();

    // §3 ─ 읽기·쓰기 ───────────────────────────────────────

    /// <summary>
    /// 태그 목록을 일괄 읽습니다 (배치 폴링).
    /// <para>
    /// 드라이버 내부에서 레지스터 범위를 최적화하여 최소 요청으로 읽는 것을 권장.
    /// </para>
    /// </summary>
    /// <param name="tags">읽을 태그 요청 목록</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>읽기 결과 (Values: 각 태그별 값, 오류 시 Error 채움)</returns>
    Task<DriverReadResult> ReadTagsAsync(
        IReadOnlyList<TagReadRequest> tags,
        CancellationToken ct = default);

    /// <summary>
    /// 단일 태그에 값을 씁니다.
    /// </summary>
    /// <param name="tag">쓰기 요청 (주소·값·데이터타입)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>쓰기 결과</returns>
    Task<DriverWriteResult> WriteTagAsync(
        TagWriteRequest tag,
        CancellationToken ct = default);

    // §4 ─ 이벤트 ──────────────────────────────────────────

    /// <summary>연결 성공 시 발생. 파라미터: 드라이버 식별명</summary>
    event Action<string>?         OnConnected;

    /// <summary>오류 발생 시 발생. 파라미터: (드라이버 식별명, 오류 메시지)</summary>
    event Action<string, string>? OnError;
}
