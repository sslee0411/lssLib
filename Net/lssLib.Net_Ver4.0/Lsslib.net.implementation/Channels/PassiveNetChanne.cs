// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Channels/PassiveNetChannel.cs
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;

namespace lssLib.Net.Implementation;

/// <summary>
/// 수동 수신 채널 (Passive 모드).
/// 장비가 먼저 데이터를 보내오는 환경에 사용합니다.
/// </summary>
/// <remarks>
/// 수신 방법 2가지:
/// <list type="number">
///   <item><description>이벤트: <c>channel.DeviceFrameReceived += (id, frame) => ...</c></description></item>
///   <item><description>비동기 열거: <c>await foreach (var frame in channel.ReadAllAsync(ct))</c></description></item>
/// </list>
///
/// <b>조립 예시 (시리얼 센서):</b>
/// <code>
/// var cfg = new SerialDeviceConfig(3, "Sensor-03", "COM3", 115200)
/// {
///     IsRetryEnabled = true,
///     RetryTarget    = RetryTarget.All,
///     MaxRetries     = 10
/// };
///
/// await using var channel = new PassiveNetChannel(
///     cfg,
///     SerialTransport.FromConfig(cfg),
///     new BinaryProtocol(stx: 0xAA),
///     autoRegister: true);
///
/// channel.DeviceFrameReceived += (id, frame) =>
///     Dispatcher.InvokeAsync(() => UpdateUI(id, frame));   // ⚠ UI 스레드 주의
///
/// channel.DeviceStateChanged += (id, state) =>
///     Dispatcher.InvokeAsync(() => LblState.Content = state.ToString());
///
/// channel.DeviceErrorOccurred += (id, ex) =>
///     LogManager.Instance.Error(cfg.DeviceName, ex.Message);
///
/// await channel.StartAsync();
///
/// // 파생 클래스 패턴 (수신 프레임 추가 처리)
/// // public class TemperatureSensorChannel : PassiveNetChannel
/// // {
/// //     protected override Task OnDeviceFrameReceivedAsync(byte[] frame, CancellationToken ct)
/// //     {
/// //         var result = frame.ToParser().Parse(SensorSchema.Default);
/// //         TemperatureReceived?.Invoke(DeviceId, result.GetFloat("Temperature"));
/// //         return base.OnDeviceFrameReceivedAsync(frame, ct);
/// //     }
/// // }
/// </code>
/// </remarks>
public class PassiveNetChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.Passive;

    /// <summary>Passive 수신 채널을 초기화합니다.</summary>
    /// <param name="config">장비 설정</param>
    /// <param name="transport">전송 계층. XxxTransport.FromConfig(cfg) 권장.</param>
    /// <param name="protocol">프로토콜 계층.</param>
    /// <param name="autoRegister">true 이면 NetDeviceRegistry 자동 등록.</param>
    public PassiveNetChannel(
        NetDeviceConfigBase config,
        INetTransport transport,
        INetProtocol protocol,
        bool autoRegister = false)
        : base(config, transport, protocol, autoRegister) { }
}