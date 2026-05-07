namespace lssLib.Net.Base.Core;
/// <summary>
/// 전송 계층 연결 상태.
/// </summary>
/// <remarks>
/// <para>
/// 상태 전이 흐름:
/// <code>
/// Disconnected
///     │ ConnectAsync 호출
///     ▼
/// Connecting
///     │ 성공
///     ▼
/// Connected ◄──────────────────── 재접속 성공
///     │ 통신 오류 발생
///     ▼
/// Reconnecting (재접속 루프 실행 중)
///     │ 최대 횟수 초과
///     ▼
/// Error
///     │ DisposeAsync 호출
///     ▼
/// Disposed
/// </code>
/// </para>
///
/// <para>
/// WPF 연동 시 <c>DeviceStateChanged</c> 이벤트를 구독하여
/// 상태에 따라 UI를 갱신하는 것을 권장합니다.
/// </para>
///
/// <b>사용 예시:</b>
/// <code>
/// channel.DeviceStateChanged += (deviceId, state) =>
/// {
///     Dispatcher.InvokeAsync(() =>
///     {
///         TxtState.Text        = state.ToString();
///         BtnSend.IsEnabled    = (state == NetState.Connected);
///         BtnConnect.IsEnabled = (state == NetState.Disconnected);
///
///         ImgStatus.Source = state switch
///         {
///             NetState.Connected    => GreenIcon,
///             NetState.Reconnecting => YellowIcon,
///             _                     => RedIcon
///         };
///     });
/// };
/// </code>
/// </remarks>
public enum NetState
{
    /// <summary>
    /// 연결 끊김.
    /// <para>초기 상태 또는 <c>DisconnectAsync</c> / <c>StopAsync</c> 명시적 호출 후.</para>
    /// <para>이 상태에서는 <c>channel.IsConnected</c> 가 <c>false</c> 입니다.</para>
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// 연결 시도 중.
    /// <para><c>ConnectAsync</c> 가 호출되어 소켓/포트를 여는 중입니다.</para>
    /// <para>이 상태에서는 모든 통신 동작이 스킵됩니다.</para>
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// 연결됨 — 정상 통신 가능.
    /// <para><c>channel.IsConnected</c> 가 <c>true</c> 인 유일한 상태입니다.</para>
    /// <para>Write / Read / Request / Heartbeat 모두 정상 동작합니다.</para>
    /// </summary>
    Connected = 2,

    /// <summary>
    /// 재접속 대기 중.
    /// <para>통신 오류 발생 후 <c>TriggerReconnectAsync</c> 가 재접속을 시도하는 중입니다.</para>
    /// <para>지수 백오프 대기 중에는 이 상태가 유지됩니다.</para>
    /// <para>재접속 성공 시 → <see cref="Connected"/>, 한도 초과 시 → <see cref="Error"/>.</para>
    /// </summary>
    Reconnecting = 3,

    /// <summary>
    /// 오류 상태.
    /// <para>재접속 한도 초과 또는 복구 불가 오류 발생 후.</para>
    /// <para><c>DeviceErrorOccurred</c> 이벤트가 먼저 발생합니다.</para>
    /// <para>이 상태에서는 <c>StopAsync</c> 후 채널을 재생성해야 합니다.</para>
    /// </summary>
    Error = 4,

    /// <summary>
    /// Dispose 완료.
    /// <para><c>DisposeAsync</c> 가 완료된 후의 최종 상태입니다.</para>
    /// <para>이 이후 채널을 사용하면 <see cref="ObjectDisposedException"/> 이 발생합니다.</para>
    /// </summary>
    Disposed = 5
}
