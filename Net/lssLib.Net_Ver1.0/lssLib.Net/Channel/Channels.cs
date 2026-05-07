// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · PassiveNetChannel.cs
//  역할: 형태 1 — 수동 수신 채널 (장치가 먼저 데이터를 보내옴)
// ══════════════════════════════════════════════════════════════════════

//using lssLib.Log;

namespace lssLib.Net.Channel;

/// <summary>
/// 수동 수신 채널 (Passive 모드).
/// 장치에서 일방적으로 데이터를 전송하는 환경에 사용합니다.
/// </summary>
/// <remarks>
/// 사용 예시 (시리얼 센서 수신):
/// <code>
/// var transport = new SerialTransport("COM3", 115200);
/// var protocol  = new BinaryProtocol(stx: 0xAA);
/// var channel   = new PassiveNetChannel(transport, protocol, NetConfig.Serial);
///
/// channel.FrameReceived += frame =>
/// {
///     var result = frame.ToParser().Parse(SensorSchema.Default);
///     float temp = result.GetFloat("Temperature");
///     Dispatcher.InvokeAsync(() => TxtTemp.Text = $"{temp:F2}°C");
/// };
///
/// await channel.StartAsync();
///
/// // 또는 비동기 열거
/// await foreach (var frame in channel.ReadAllAsync(ct))
///     Process(frame);
/// </code>
/// </remarks>
public sealed class PassiveNetChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.Passive;

    /// <param name="transport">전송 계층 (SerialTransport, TcpTransport 등)</param>
    /// <param name="protocol">프로토콜 (BinaryProtocol, RawProtocol 등)</param>
    /// <param name="config">채널 설정 (<see cref="NetConfig.Serial"/> 등 프리셋 사용 가능)</param>
    public PassiveNetChannel(INetTransport transport, INetProtocol protocol, NetConfig? config = null)
        : base(transport, protocol, config ?? new NetConfig()) { }
}


// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · RequestResponseChannel.cs
//  역할: 형태 2 — 요청-응답 채널 (우리가 요청하고 응답을 받음)
//        주기적 Read + Write 우선 처리
// ══════════════════════════════════════════════════════════════════════

//namespace lssLib.Net.Channel;

/// <summary>
/// 요청-응답 채널 (RequestResponse 모드).
/// 주기적으로 Read 요청을 보내되, Write 가 들어오면 즉시 우선 처리합니다.
/// </summary>
/// <remarks>
/// 우선순위 동작:
/// <list type="bullet">
///   <item><description>외부 <c>WriteAsync</c> → <see cref="NetPriority.Write"/> — 즉시 큐 선점</description></item>
///   <item><description>주기적 Read → <see cref="NetPriority.Read"/> — Write 없을 때 처리</description></item>
///   <item><description>Write 실패 재전송 → <see cref="NetPriority.Critical"/> — 최우선</description></item>
/// </list>
///
/// 사용 예시 (Modbus RTU 폴링):
/// <code>
/// public class ModbusChannel : RequestResponseChannel
/// {
///     // 주기적으로 보낼 읽기 요청 프레임 정의
///     protected override Task&lt;byte[]?&gt; BuildReadRequestAsync(CancellationToken ct)
///     {
///         // Modbus FC=0x03 (Read Holding Registers)
///         byte[] req = BufferWriter.Create()
///             .WriteUInt8(0x01)       // Device ID
///             .WriteUInt8(0x03)       // FC
///             .WriteUInt16BE(0x0000)  // Start address
///             .WriteUInt16BE(0x000A)  // Count
///             .AppendCrc16Modbus()
///             .ToArray();
///         return Task.FromResult&lt;byte[]?&gt;(req);
///     }
/// }
///
/// var channel = new ModbusChannel(new SerialTransport("COM3", 9600), new RawProtocol(),
///     NetConfig.Serial with { PeriodicReadInterval = TimeSpan.FromMilliseconds(50) });
///
/// await channel.StartAsync();
///
/// // 쓰기 (Write 가 주기 Read 보다 우선 처리됨)
/// await channel.WriteAsync(setpointFrame, NetPriority.Write);
///
/// // 단발 요청-응답
/// NetResult r = await channel.RequestAsync(queryFrame, timeout: TimeSpan.FromSeconds(1));
/// if (r.IsOk)
///     var parsed = r.Data!.ToParser().Parse(MySchema.Default);
/// </code>
/// </remarks>
public class RequestResponseChannel : NetChannelBase
{
    /// <inheritdoc/>
    public override NetMode Mode => NetMode.RequestResponse;

    /// <param name="transport">전송 계층</param>
    /// <param name="protocol">프로토콜</param>
    /// <param name="config">채널 설정 (<see cref="NetConfig.Serial"/> 또는 <see cref="NetConfig.Tcp"/> 추천)</param>
    public RequestResponseChannel(INetTransport transport, INetProtocol protocol, NetConfig? config = null)
        : base(transport, protocol, config ?? NetConfig.Serial) { }

    /// <summary>
    /// 주기적 Read 요청 프레임을 생성합니다.
    /// 파생 클래스에서 장치별 요청 프레임을 반환하도록 재정의합니다.
    /// </summary>
    protected override Task<byte[]?> BuildReadRequestAsync(CancellationToken ct)
        => Task.FromResult<byte[]?>(null);  // 기본: 주기 Read 없음
}