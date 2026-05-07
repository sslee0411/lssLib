// ══════════════════════════════════════════════════════════════════════
//  INetTransport
// ══════════════════════════════════════════════════════════════════════

//  ┌─ 역할  ─--------------------─────────────────┐
//  │  INetTransport   ── 물리 연결 / 바이트 전송·수신             │
//  │                    TCP소켓, COM포트, 공유메모리 등              │
//  └─────────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════
namespace lssLib.Net.Base.Contracts;

using lssLib.Net.Base.Core;

/// <summary>
/// 전송 계층 인터페이스.
/// </summary>
/// <remarks>
/// <para>
/// <b>책임 범위:</b>
/// 물리적 연결 관리 및 원시 바이트(raw bytes) 수준의 송수신.
/// 프레임 조립·분해는 이 인터페이스의 책임이 아닙니다.
/// </para>
///
/// <b>구현 클래스 목록:</b>
/// <list type="table">
///   <listheader><term>클래스</term><description>대상 통신</description></listheader>
///   <item><term><c>TcpTransport</c></term><description>TCP 클라이언트 소켓</description></item>
///   <item><term><c>SerialTransport</c></term><description>COM 포트 (RS-232 / RS-485)</description></item>
///   <item><term><c>UdpTransport</c></term><description>UDP 송수신 (예정)</description></item>
///   <item><term><c>SharedMemoryTransport</c></term><description>프로세스 간 공유 메모리 IPC</description></item>
///   <item><term><c>HttpTransport</c></term><description>HTTP/REST HttpClient 래핑 (예정)</description></item>
///   <item><term><c>MqttTransport</c></term><description>MQTT 브로커 연결 (예정)</description></item>
/// </list>
///
/// <b>구현 시 주의사항:</b>
/// <list type="bullet">
///   <item><description>
///     <c>ConnectAsync</c> 성공 시 <see cref="StateChanged"/> 이벤트로 <see cref="NetState.Connected"/> 를 발생시켜야 합니다.
///   </description></item>
///   <item><description>
///     Passive 수신 루프(예: SerialPort.DataReceived, SharedMemory 폴링)에서
///     데이터 수신 시 <see cref="DataReceived"/> 이벤트를 발생시켜야 합니다.
///   </description></item>
///   <item><description>
///     <see cref="NetTransportBase"/> 를 상속하면 상태 관리와 이벤트 발생이 자동으로 처리됩니다.
///     직접 구현보다 상속을 권장합니다.
///   </description></item>
/// </list>
///
/// <b>직접 구현 예시 (커스텀 Transport):</b>
/// <code>
/// public class BluetoothTransport : NetTransportBase
/// {
///     protected override async Task ConnectCoreAsync(CancellationToken ct)
///     {
///         await _btDevice.ConnectAsync(ct);
///         // NetTransportBase 가 State = Connected 로 자동 변경
///     }
///
///     protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
///     {
///         _btDevice.Send(data);
///         return Task.CompletedTask;
///     }
///
///     // 데이터 수신 시 호출 (NetTransportBase 헬퍼 메서드)
///     private void OnBtDataReceived(byte[] data) => RaiseDataReceived(data);
/// }
/// </code>
/// </remarks>
public interface INetTransport : IAsyncDisposable // 비동시적 자원 해제 인터페이스
{
    #region 상태

    /// <summary>
    /// 현재 연결 상태.
    /// <para><see cref="NetChannelBase.IsConnected"/> 는 이 값이 <see cref="NetState.Connected"/> 인지 확인합니다.</para>
    /// </summary>
    NetState State { get; }

    /// <summary>
    /// 연결 상태가 변경될 때 발생합니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="NetChannelBase"/> 가 이 이벤트를 구독하여
    /// <c>DeviceStateChanged</c> 이벤트를 상위로 전파합니다.
    /// </para>
    /// <para>
    /// ⚠ 백그라운드 스레드에서 호출될 수 있습니다.
    /// WPF UI 접근 시 반드시 <c>Dispatcher.InvokeAsync</c> 를 사용하세요.
    /// </para>
    /// </remarks>
    event Action<NetState>? StateChanged;

    /// <summary>
    /// 원시 데이터가 수신되었을 때 발생합니다 (Passive 수신 모드).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전송 계층이 원시 바이트를 올려보내면,
    /// <see cref="NetChannelBase"/> 가 이 이벤트를 구독하여
    /// <see cref="INetProtocol.TryDecode"/> 로 프레임을 디코딩합니다.
    /// </para>
    /// <para>
    /// 프레임 조립은 <see cref="INetProtocol"/> 의 책임입니다.
    /// Transport 는 받은 바이트를 그대로 올려보내야 합니다.
    /// </para>
    /// <para>
    /// ⚠ SerialPort.DataReceived 또는 폴링 루프에서 발생하므로
    /// 백그라운드 스레드에서 호출됩니다.
    /// </para>
    /// </remarks>
    event Action<byte[]>? DataReceived;

    #endregion

    #region 연결 제어

    /// <summary>
    /// 비동기 연결을 시도합니다.
    /// </summary>
    /// <param name="ct">취소 토큰</param>
    /// <exception cref="Exception">연결 실패 시 예외를 throw 합니다 (SocketException, UnauthorizedAccessException 등).</exception>
    /// <remarks>
    /// 성공 시 <see cref="StateChanged"/> 이벤트로 <see cref="NetState.Connected"/> 가 발생합니다.
    /// 실패 시 <see cref="NetState.Error"/> 가 발생하고 예외가 throw 됩니다.
    /// </remarks>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 연결을 해제합니다.
    /// </summary>
    /// <remarks>
    /// 소켓/포트를 닫고 관련 리소스를 해제합니다.
    /// 이미 연결이 끊어진 상태에서 호출해도 예외가 발생하지 않습니다.
    /// </remarks>
    Task DisconnectAsync();

    #endregion

    #region 데이터 송수신

    /// <summary>
    /// 인코딩 완료된 바이트 배열을 전송합니다.
    /// </summary>
    /// <param name="data"><see cref="INetProtocol.Encode"/> 결과 (헤더·CRC 포함)</param>
    /// <param name="ct">취소 토큰</param>
    /// <exception cref="Exception">연결이 끊어졌거나 전송 오류 시 예외를 throw 합니다.</exception>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// 지정 바이트 수만큼 읽습니다.
    /// </summary>
    /// <param name="length">읽을 바이트 수</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>수신된 원시 바이트 (length 이하일 수 있음)</returns>
    /// <remarks>
    /// 응답 길이를 미리 알 때 사용합니다.
    /// 예: Modbus 응답의 고정 길이 부분만 먼저 읽기.
    /// </remarks>
    Task<byte[]> ReadAsync(int length, CancellationToken ct = default);

    /// <summary>
    /// 프레임 1개를 읽습니다.
    /// </summary>
    /// <param name="ct">취소 토큰</param>
    /// <returns>수신된 원시 바이트 (프레임 1개 이상 포함될 수 있음)</returns>
    /// <remarks>
    /// 내부 버퍼 크기만큼 읽습니다 (기본 4096B).
    /// 프레임 경계는 <see cref="INetProtocol.IsFrameComplete"/> 에서 판단합니다.
    /// </remarks>
    Task<byte[]> ReadAsync(CancellationToken ct = default);

    #endregion
}