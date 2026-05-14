// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/NetDeviceConfig.cs
//  역할: 장비 설정 추상 베이스
//
//  v5 변경사항 (v4 NetDeviceConfigBase 대비):
//    · IDeviceConfig / IRetryConfig / ISequenceConfig / ICommandConfig
//      4개 인터페이스 완전 제거 → 단일 추상 클래스로 통합
//    · WriteCommands 제거 (인프라 내부에서 미사용)
//    · ClearCommands() → ClearReadCommands() 로 명칭 명확화
//    · 외부 API 호환 — 파생 클래스(TcpDeviceConfig 등) 코드 변경 없음
// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Config/NetDeviceConfig.cs  [v5.1]
//  IsSequential(bool) → SequenceMode(int)
//    0  = Parallel   병렬  (Task.WhenAll)
//    1  = Sequential 단일순차 (1개씩)
//    N≥2 = Window(N)  슬라이딩 윈도우 N개 동시
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// lssLib.Net 장비 설정 추상 베이스.
/// </summary>
/// <remarks>
/// <b>파생 클래스 패턴:</b>
/// <code>
/// public sealed class TcpDeviceConfig : NetDeviceConfig
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
/// <b>사용 예시:</b>
/// <code>
/// var cfg = new TcpDeviceConfig(1, "PLC-01", "192.168.1.10", 502)
/// {
///     RetryTarget      = RetryTarget.ConnectAndWrite,
///     MaxRetries       = 5,
///     PeriodicInterval = TimeSpan.FromMilliseconds(100)
/// };
/// cfg.AddReadCommand(modbusReadFrame);
///
/// await using var channel = new RequestResponseChannel(
///     cfg, TcpTransport.FromConfig(cfg), new BinaryProtocol(), autoRegister: true);
/// </code>
/// </remarks>
public abstract class NetDeviceConfig
{
    #region §1 ─ 장비 식별

    /// <summary>장비 고유 정수 ID. 앱 내에서 유일해야 합니다.</summary>
    public int DeviceId { get; }

    /// <summary>장비 표시 이름. LogManager Source 로 자동 적용됩니다.</summary>
    public string DeviceName { get; }

    /// <summary>전송 계층 종류. 파생 클래스에서 override 해서 선언합니다.</summary>
    public abstract NetTransportType TransportType { get; }

    #endregion

    #region §2 ─ 재시도 설정

    /// <summary>재시도 활성화 여부. 기본값: true.</summary>
    public bool IsRetryEnabled { get; set; } = true;

    /// <summary>
    /// 재시도 적용 동작 대상 (<see cref="RetryTarget"/> 플래그 조합).
    /// 기본값: <see cref="RetryTarget.ConnectAndWrite"/>.
    /// </summary>
    public RetryTarget RetryTarget { get; set; } = RetryTarget.ConnectAndWrite;

    /// <summary>최대 재시도 횟수. 0=무제한. 기본값: 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>재시도 기본 대기 시간. 기본값: 200ms.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 지수 백오프 여부.
    /// true=RetryDelay×2^n 증가(최대 60s), false=고정 간격.
    /// 기본값: true.
    /// </summary>
    public bool ReconnectBackoff { get; set; } = true;

    #endregion

    #region §3 ─ SequenceMode (v5.1 신규)

    /// <summary>
    /// ReadCommands 전송 순서 모드.
    /// <list type="table">
    ///   <item><term>0 — Parallel</term>
    ///         <description>모든 커맨드 동시 투입 (Task.WhenAll). TCP/UDP/HTTP/WS/MQTT/Virtual 기본값.</description></item>
    ///   <item><term>1 — Sequential</term>
    ///         <description>1개씩 순서대로. RS-485 / Modbus RTU / NamedPipe 기본값.</description></item>
    ///   <item><term>N ≥ 2 — Window(N)</term>
    ///         <description>슬라이딩 윈도우: N개씩 동시 허용, 전체 순서 유지.</description></item>
    /// </list>
    /// <example><code>
    /// cfg.SequenceMode = SequenceModes.Parallel;    // 0: 병렬
    /// cfg.SequenceMode = SequenceModes.Sequential;  // 1: 단일순차
    /// cfg.SequenceMode = 3;                         // 슬라이딩 윈도우 3개
    /// </code></example>
    /// </summary>
    public int SequenceMode { get; set; } = SequenceModes.Sequential;

    /// <summary>SequenceMode 상수 모음.</summary>
    public static class SequenceModes
    {
        /// <summary>0 — 병렬 (Task.WhenAll). TCP / UDP 권장.</summary>
        public const int Parallel = 0;
        /// <summary>1 — 단일 순차 (1개씩). RS-485 / Modbus RTU 필수.</summary>
        public const int Sequential = 1;
    }

    #endregion

    #region §4 ─ 커맨드 목록

    private readonly List<byte[]> _readCommands = [];

    /// <summary>주기적으로 전송할 읽기 요청 프레임 목록 (읽기 전용).</summary>
    public IReadOnlyList<byte[]> ReadCommands => _readCommands.AsReadOnly();

    /// <summary>읽기 요청 프레임을 추가합니다.</summary>
    /// <param name="command">프로토콜 인코딩 전 원시 페이로드</param>
    public void AddReadCommand(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _readCommands.Add(command);
    }

    /// <summary>모든 ReadCommands 를 제거합니다.</summary>
    public void ClearReadCommands() => _readCommands.Clear();

    #endregion

    #region §5 ─ 채널 동작 설정

    /// <summary>
    /// 주기 Read 간격 (RequestResponse 전용).
    /// <para>Zero=비활성. 기본값: 100ms.</para>
    /// </summary>
    public TimeSpan PeriodicInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>단발 RequestAsync 응답 대기 타임아웃. 기본값: 3s.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Heartbeat 전송 주기.
    /// <para>Zero=비활성. TCP 환경 기본값: 30s (TcpDeviceConfig 생성자에서 설정).</para>
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Heartbeat 전송 후 서버 응답 수신 여부.
    /// <para>false(기본값): 전송만 수행 (Keep-Alive).</para>
    /// <para>true: WriteAsync + ReadAsync → DeviceFrameReceived 이벤트.</para>
    /// </summary>
    public bool IsHeartbeatAcknowledged { get; set; } = false;

    /// <summary>
    /// 수신 Channel 용량.
    /// <para>0=무제한, 양수=초과 시 오래된 항목 자동 제거. 기본값: 0.</para>
    /// </summary>
    public int ReceiveChannelCapacity { get; set; } = 0;

    #endregion

    #region §6 ─ 생성자

    /// <param name="deviceId">장비 고유 ID (앱 내 유일)</param>
    /// <param name="deviceName">장비 이름 (LogManager Source 자동 적용, 파일명 형식 권장)</param>
    /// <exception cref="ArgumentException">deviceName 이 비어있을 경우</exception>
    protected NetDeviceConfig(int deviceId, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("DeviceName 은 비어있을 수 없습니다.", nameof(deviceName));

        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    #endregion

    public override string ToString()
    {
        string strSeq = SequenceMode switch
        {
            SequenceModes.Parallel => "Parallel(0)",
            SequenceModes.Sequential => "Sequential(1)",
            var n => $"Window({n})"
        };
        return $"[{DeviceName}#{DeviceId}] Transport={TransportType} " +
               $"ReadCmd={ReadCommands.Count} SeqMode={strSeq} " +
               $"Retry={IsRetryEnabled}({RetryTarget})";
    }
}