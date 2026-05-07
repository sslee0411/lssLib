// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · NetConfig.cs
//  역할: 채널 공통 설정 (재접속 / Heartbeat / 우선순위 큐 / 타임아웃)
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// <see cref="NetChannelBase"/> 공통 설정 클래스.
/// 파생 채널(TcpChannel, SerialChannel 등)에서 필요에 따라 확장하여 사용합니다.
/// </summary>
/// <example><code>
/// var config = new NetConfig
/// {
///     AutoReconnect        = true,
///     MaxReconnectAttempts = 5,
///     ReconnectDelay       = TimeSpan.FromSeconds(2),
///     HeartbeatInterval    = TimeSpan.FromSeconds(10),
///     PeriodicReadInterval = TimeSpan.FromMilliseconds(100),
///     RequestTimeout       = TimeSpan.FromSeconds(3),
///     MaxWriteRetries      = 3
/// };
/// </code></example>
public class NetConfig
{
    #region §1 ─ 재접속

    /// <summary>연결 끊김 시 자동 재접속 여부. 기본값: <c>true</c>.</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>최대 재접속 시도 횟수. 0=무제한. 기본값: 5.</summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>재접속 기본 대기 시간 (지수 백오프 기준값). 기본값: 2초.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>재접속 백오프 사용 여부. true=지수 증가, false=고정. 기본값: <c>true</c>.</summary>
    public bool ReconnectBackoff { get; set; } = true;

    #endregion

    #region §2 ─ Heartbeat

    /// <summary>
    /// Heartbeat 전송 주기. <see cref="TimeSpan.Zero"/> 이면 비활성화. 기본값: 비활성.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.Zero;

    #endregion

    #region §3 ─ 요청-응답 모드 (RequestResponse)

    /// <summary>
    /// 주기적 Read 요청 간격 (<see cref="NetMode.RequestResponse"/> 전용).
    /// <see cref="TimeSpan.Zero"/> 이면 주기 Read 비활성. 기본값: 100ms.
    /// </summary>
    public TimeSpan PeriodicReadInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>단일 요청에 대한 응답 대기 타임아웃. 기본값: 3초.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);

    #endregion

    #region §4 ─ 재전송

    /// <summary>Write 실패 시 최대 재전송 횟수. 0=재전송 없음. 기본값: 3.</summary>
    public int MaxWriteRetries { get; set; } = 3;

    /// <summary>재전송 대기 시간. 기본값: 50ms.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    #endregion

    #region §5 ─ 채널 용량

    /// <summary>
    /// 내부 수신 Channel 용량. 0=무제한. 기본값: 0.
    /// 양수로 설정 시 초과분은 가장 오래된 항목부터 제거됩니다.
    /// </summary>
    public int ReceiveChannelCapacity { get; set; } = 0;

    #endregion

    #region §6 ─ 환경별 프리셋

    /// <summary>시리얼 포트 / 저속 산업 통신 권장 설정.</summary>
    public static NetConfig Serial => new()
    {
        AutoReconnect = true,
        MaxReconnectAttempts = 10,
        ReconnectDelay = TimeSpan.FromMilliseconds(500),
        ReconnectBackoff = false,        // 고정 간격 (장치 응답 주기 맞춤)
        HeartbeatInterval = TimeSpan.Zero,
        PeriodicReadInterval = TimeSpan.FromMilliseconds(50),
        RequestTimeout = TimeSpan.FromSeconds(1),
        MaxWriteRetries = 3
    };

    /// <summary>TCP 클라이언트 권장 설정.</summary>
    public static NetConfig Tcp => new()
    {
        AutoReconnect = true,
        MaxReconnectAttempts = 5,
        ReconnectDelay = TimeSpan.FromSeconds(2),
        ReconnectBackoff = true,
        HeartbeatInterval = TimeSpan.FromSeconds(30),
        PeriodicReadInterval = TimeSpan.FromMilliseconds(100),
        RequestTimeout = TimeSpan.FromSeconds(5),
        MaxWriteRetries = 3
    };

    /// <summary>UDP 비연결형 통신 권장 설정.</summary>
    public static NetConfig Udp => new()
    {
        AutoReconnect = false,        // UDP 는 연결 개념 없음
        MaxReconnectAttempts = 0,
        HeartbeatInterval = TimeSpan.Zero,
        PeriodicReadInterval = TimeSpan.FromMilliseconds(50),
        RequestTimeout = TimeSpan.FromMilliseconds(500),
        MaxWriteRetries = 0             // UDP 는 재전송 미보장
    };

    /// <summary>공유 메모리 IPC 권장 설정.</summary>
    public static NetConfig SharedMemory => new()
    {
        AutoReconnect = false,
        HeartbeatInterval = TimeSpan.Zero,
        PeriodicReadInterval = TimeSpan.FromMilliseconds(10),  // 고속 폴링
        RequestTimeout = TimeSpan.FromMilliseconds(200),
        MaxWriteRetries = 1
    };

    #endregion
}