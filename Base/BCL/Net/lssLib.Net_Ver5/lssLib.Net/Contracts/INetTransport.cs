// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Contracts/INetTransport.cs
//  역할: 전송 계층 인터페이스
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 전송 계층 인터페이스.
/// TCP / UDP / Serial / 공유 메모리 / NamedPipe / HTTP / WebSocket / MQTT / Virtual
/// 모든 통신 리소스는 이 인터페이스를 구현합니다.
/// </summary>
/// <remarks>
/// <para>
/// 직접 구현보다 <see cref="NetTransportBase"/> 상속을 권장합니다.
/// 상태 관리·이벤트·Dispose 처리가 자동으로 처리됩니다.
/// </para>
/// <para>
/// <b>새 Transport 추가 방법:</b>
/// <list type="number">
///   <item><description>NetTransportBase 를 상속하는 클래스 생성</description></item>
///   <item><description>ConnectCoreAsync / DisconnectCoreAsync / WriteCoreAsync / ReadCoreAsync 구현</description></item>
///   <item><description>대응하는 XxxDeviceConfig 클래스 생성 (NetDeviceConfig 상속)</description></item>
///   <item><description>NetTransportType 열거형에 새 타입 추가</description></item>
/// </list>
/// </para>
/// </remarks>
public interface INetTransport : IAsyncDisposable
{
    /// <summary>현재 연결 상태.</summary>
    NetState State { get; }

    /// <summary>
    /// 연결 상태 변경 시 발생.
    /// <para>⚠ 백그라운드 스레드 — WPF UI 접근 시 Dispatcher.InvokeAsync 필요.</para>
    /// </summary>
    event Action<NetState>? StateChanged;

    /// <summary>
    /// 원시 데이터 수신 시 발생 (Passive 수신 모드).
    /// <para>프레임 조립·CRC 검증은 <see cref="INetProtocol"/> 에서 담당합니다.</para>
    /// </summary>
    event Action<byte[]>? DataReceived;

    /// <summary>
    /// 비동기 연결을 시도합니다.
    /// </summary>
    /// <param name="ct">취소 토큰</param>
    /// <exception cref="Exception">연결 실패 시 예외 throw.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 연결을 해제합니다.
    /// <para>이미 끊어진 상태에서도 예외 없음.</para>
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 인코딩 완료된 바이트 배열을 전송합니다.
    /// <para>프로토콜 인코딩은 <see cref="INetProtocol.Encode"/> 에서 사전 처리됩니다.</para>
    /// </summary>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// 고정 길이 바이트를 수신합니다.
    /// </summary>
    /// <param name="length">수신할 바이트 수. 0 이면 내부 기본 버퍼 크기 사용.</param>
    Task<byte[]> ReadAsync(int length, CancellationToken ct = default);

    /// <summary>
    /// 프레임 1개를 수신합니다 (기본: 내부 버퍼 크기만큼 읽기).
    /// </summary>
    Task<byte[]> ReadAsync(CancellationToken ct = default);
}