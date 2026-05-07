// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Abstractions/NetDeviceConfigBase.cs
//  역할: 4개 Config 인터페이스 브릭 조립 추상 베이스
//        파생 클래스는 전송 계층 고유 파라미터만 추가합니다.
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// lssLib.Net 장비 설정 추상 베이스.
/// Config 인터페이스 4개를 조합하여 
/// 통신 채널 설정을 위한 공통 속성과 
/// 메서드를 제공
/// </summary>
/// <remarks>
/// <b>4개 인터페이스 브릭 조립:</b>
/// <list type="table">
///   <listheader><term>인터페이스</term><description>역할</description></listheader>
///   <item><term><see cref="IDeviceConfig"/></term><description>DeviceId, DeviceName (장비 식별)</description></item>
///   <item><term><see cref="IRetryConfig"/></term><description>재시도 정책</description></item>
///   <item><term><see cref="ISequenceConfig"/></term><description>순차/병렬 처리</description></item>
///   <item><term><see cref="ICommandConfig"/></term><description>ReadCommands / WriteCommands</description></item>
/// </list>
///
/// <b>파생 클래스 패턴:</b>
/// <code>
/// public sealed class TcpDeviceConfig : NetDeviceConfigBase
/// {
///     public override NetTransportType TransportType => NetTransportType.Tcp;
///     public string Host { get; set; }
///     public int    Port { get; set; }
///
///     public TcpDeviceConfig(int id, string name, string host, int port)
///         : base(id, name) { Host = host; Port = port; }
/// }
/// </code>
///
/// <b>조립 + 사용 예시:</b>
/// <code>
/// var cfg = new TcpDeviceConfig(1, "PLC-01", "192.168.1.10", 502)
/// {
///     IsRetryEnabled   = true,
///     RetryTarget      = RetryTarget.Connect | RetryTarget.Write,
///     MaxRetries       = 5,
///     IsSequential     = false,
///     PeriodicInterval = TimeSpan.FromMilliseconds(100)
/// };
/// cfg.AddReadCommand(modbusReadFrame);
///
/// await using var channel = new RequestResponseChannel(
///     cfg,
///     TcpTransport.FromConfig(cfg),
///     new BinaryProtocol(),
///     autoRegister: true);
/// </code>
/// </remarks>
public abstract class NetDeviceConfigBase
    : IDeviceConfig, IRetryConfig, ISequenceConfig, ICommandConfig
{
    #region §1 ─ IDeviceConfig

    /// <inheritdoc/>
    public int DeviceId { get; }

    /// <inheritdoc/>
    public string DeviceName { get; }

    #endregion

    #region §2 ─ IRetryConfig

    /// <inheritdoc/>
    public bool IsRetryEnabled { get; set; } = true;

    /// <inheritdoc/>
    public RetryTarget RetryTarget { get; set; } = RetryTarget.All;

    /// <inheritdoc/>
    public int MaxRetries { get; set; } = 3;

    /// <summary>지수 백오프 여부. true=RetryDelay×2^n 증가(최대 60s), false=고정.</summary>
    public bool ReconnectBackoff { get; set; } = true;

    /// <inheritdoc/>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    #endregion

    #region §3 ─ ISequenceConfig

    /// <inheritdoc/>
    public bool IsSequential { get; set; } = true;

    #endregion

    #region §4 ─ ICommandConfig

    private readonly List<byte[]> _readCommands = [];
    private readonly List<byte[]> _writeCommands = [];

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> ReadCommands => _readCommands.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> WriteCommands => _writeCommands.AsReadOnly();

    /// <inheritdoc/>
    public void AddReadCommand(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _readCommands.Add(command);
    }

    /// <inheritdoc/>
    public void AddWriteCommand(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _writeCommands.Add(command);
    }

    /// <inheritdoc/>
    public void ClearCommands()
    {
        _readCommands.Clear();
        _writeCommands.Clear();
    }

    #endregion

    #region §5 ─ 공통 채널 동작 설정

    /// <summary>주기 Read 간격 (RequestResponse 전용). Zero=비활성. 기본값: 100ms.</summary>
    public TimeSpan PeriodicInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>단발 RequestAsync 응답 대기 타임아웃. 기본값: 3s.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Heartbeat 전송 주기. Zero=비활성. 기본값: Zero.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.Zero;

    /// <summary>수신 Channel 용량. 0=무제한. 양수=초과 시 오래된 항목 제거. 기본값: 0.</summary>
    public int ReceiveChannelCapacity { get; set; } = 0;

    #endregion

    #region §6 ─ 전송 계층 종류 (파생 클래스 선언 필수)

    /// <summary>전송 계층 종류. 파생 클래스에서 override 해서 선언합니다.</summary>
    public abstract NetTransportType TransportType { get; }

    #endregion

    #region §7 ─ 생성자

    /// <param name="deviceId">장비 고유 ID (앱 내 유일)</param>
    /// <param name="deviceName">장비 이름 (LogManager Source 자동 적용, 파일명 형식 권장)</param>
    protected NetDeviceConfigBase(int deviceId, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("DeviceName 은 비어있을 수 없습니다.", nameof(deviceName));
        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    #endregion

    /// <inheritdoc/>
    public override string ToString()
        => $"[{DeviceName}#{DeviceId}] Transport={TransportType} " +
           $"Read={ReadCommands.Count} Write={WriteCommands.Count} " +
           $"Retry={IsRetryEnabled}({RetryTarget}) Sequential={IsSequential}";
}