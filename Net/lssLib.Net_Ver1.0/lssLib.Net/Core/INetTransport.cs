// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · INetTransport.cs
//  역할: 전송 계층 추상화 (TCP / UDP / Serial / SharedMemory 교체 가능)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 전송 계층 인터페이스.
/// TCP, UDP, Serial, 공유 메모리 등 모든 통신 리소스는 이 인터페이스를 구현합니다.
/// </summary>
/// <remarks>
/// 구현 클래스 목록:
/// <list type="bullet">
///   <item><description><c>TcpTransport</c> — TCP 클라이언트/서버</description></item>
///   <item><description><c>UdpTransport</c> — UDP 송수신</description></item>
///   <item><description><c>SerialTransport</c> — COM 포트</description></item>
///   <item><description><c>SharedMemoryTransport</c> — 프로세스 간 공유 메모리 IPC</description></item>
///   <item><description><c>HttpTransport</c> — HTTP/REST (옵션)</description></item>
///   <item><description><c>MqttTransport</c> — MQTT (옵션)</description></item>
/// </list>
/// </remarks>
public interface INetTransport : IAsyncDisposable
{
    #region 상태

    /// <summary>현재 연결 상태.</summary>
    NetState State { get; }

    /// <summary>
    /// 연결 상태가 변경될 때 발생.
    /// <para>※ 백그라운드 스레드에서 호출될 수 있으므로 UI 접근 시 Dispatcher 필요.</para>
    /// </summary>
    event Action<NetState>? StateChanged;

    /// <summary>
    /// 데이터가 수신되었을 때 발생 (Passive 수신 모드).
    /// <para>전송 계층에서 완전한 원시 바이트 덩어리를 올려보냅니다.
    /// 프레임 조립은 <see cref="INetProtocol"/> 에서 담당합니다.</para>
    /// </summary>
    event Action<byte[]>? DataReceived;

    #endregion

    #region 연결 제어

    /// <summary>비동기 연결을 시도합니다.</summary>
    /// <param name="ct">취소 토큰</param>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>연결을 해제합니다.</summary>
    Task DisconnectAsync();

    #endregion

    #region 데이터 송수신

    /// <summary>
    /// 바이트 배열을 전송합니다.
    /// </summary>
    /// <param name="data">전송할 데이터 (이미 인코딩 완료된 상태)</param>
    /// <param name="ct">취소 토큰</param>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// 지정 바이트 수만큼 읽습니다 (RequestResponse 모드 고정 길이 응답 수신).
    /// </summary>
    /// <param name="length">읽을 바이트 수</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>수신된 원시 바이트</returns>
    Task<byte[]> ReadAsync(int length, CancellationToken ct = default);

    /// <summary>
    /// 프레임 1개를 읽습니다.
    /// <para>구현체 내부에서 프레임 완성 여부를 판단합니다 (STX/ETX, Length 필드 등).</para>
    /// </summary>
    /// <param name="ct">취소 토큰</param>
    /// <returns>수신된 원시 바이트 (프레임 1개)</returns>
    Task<byte[]> ReadAsync(CancellationToken ct = default);

    #endregion
}