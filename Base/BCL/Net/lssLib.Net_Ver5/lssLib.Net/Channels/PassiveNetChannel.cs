// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Channels/PassiveNetChannel.cs
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// 수동 수신 채널 (Passive 모드).
/// 장비가 먼저 데이터를 보내오는 환경에 사용합니다.
/// </summary>
/// <remarks>
/// <b>수신 방법 2가지:</b>
/// <list type="number">
///   <item>
///     <description>
///       이벤트: <c>channel.DeviceFrameReceived += (id, frame) => ...</c>
///     </description>
///   </item>
///   <item>
///     <description>
///       비동기 열거: <c>await foreach (var frame in channel.ReadAllAsync(ct))</c>
///     </description>
///   </item>
/// </list>
///
/// <b>조립 예시 — TCP Push 서버:</b>
/// <code>
/// var cfg = new TcpDeviceConfig(1, "PushSensor", "192.168.1.50", 5000)
/// {
///     MaxRetries        = 0,    // 무제한 재시도
///     HeartbeatInterval = TimeSpan.FromSeconds(30)
///     // SequenceMode = 0 (Parallel) ← TCP 기본값
/// };
///
/// // ★ enablePassiveReceive: true 필수
/// await using var channel = new PassiveNetChannel(
///     cfg,
///     TcpTransport.FromConfig(cfg, enablePassiveReceive: true),
///     new BinaryProtocol(stx: 0xAA),
///     autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) =>
///     Dispatcher.InvokeAsync(() => UpdateUI(id, frame));   // ⚠ WPF: Dispatcher 필수
///
/// channel.DeviceStateChanged  += (id, state) =>
///     Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());
///
/// channel.DeviceErrorOccurred += (id, ex) =>
///     LogManager.Instance.Error(cfg.DeviceName, ex.Message);
///
/// await channel.StartAsync();
/// </code>
///
/// <b>조립 예시 — Serial 센서:</b>
/// <code>
/// var cfg = new SerialDeviceConfig(2, "Sensor-COM3", "COM3", 115200)
/// {
///     // SequenceMode = 1 (Sequential) ← Serial 기본값
/// };
///
/// await using var channel = new PassiveNetChannel(
///     cfg,
///     SerialTransport.FromConfig(cfg),
///     new BinaryProtocol(stx: 0xAA),
///     autoRegister: true);
/// </code>
///
/// <b>파생 클래스 패턴 (수신 프레임 추가 처리):</b>
/// <code>
/// public class TemperatureSensorChannel : PassiveNetChannel
/// {
///     public event Action&lt;float&gt;? TemperatureReceived;
///
///     public TemperatureSensorChannel(SerialDeviceConfig cfg)
///         : base(cfg, SerialTransport.FromConfig(cfg),
///                new BinaryProtocol(stx: 0xAA), autoRegister: true)
///     {
///         DeviceFrameReceived += (_, frame) =>
///         {
///             float temp = BitConverter.ToSingle(frame, 4);
///             TemperatureReceived?.Invoke(temp);
///         };
///     }
/// }
/// </code>
/// </remarks>
public class PassiveNetChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.Passive;

    /// <summary>Passive 수신 채널을 초기화합니다.</summary>
    /// <param name="config">장비 설정</param>
    /// <param name="transport">
    /// 전송 계층. TCP Push 서버 연동 시 반드시
    /// <c>TcpTransport.FromConfig(cfg, enablePassiveReceive: true)</c> 사용.
    /// </param>
    /// <param name="protocol">프로토콜 계층.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 자동 등록.</param>
    public PassiveNetChannel(
        NetDeviceConfig config,
        INetTransport transport,
        INetProtocol protocol,
        bool autoRegister = false)
        : base(config, transport, protocol, autoRegister) { }
}