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

/// <summary>장비 설정 추상 베이스.</summary>
public abstract class NetDeviceConfig
{
    #region §1 ─ 장비 식별
    /// <summary>
    /// 디바이스 ID. 장비별 고유 식별자 역할. 양수 권장 (0 이상).
    /// </summary>
    public int DeviceId { get; }

    /// <summary>
    /// 디바이스 이름. 로그 및 디버깅 용도. 비어있을 수 없습니다.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public abstract NetTransportType TransportType { get; }

    #endregion

    #region §2 ─ 재시도

    /// <summary>
    /// 재시도 활성화 여부. 기본값: true.
    /// * 재시도 대상은 RetryTarget 플래그로 지정합니다.
    /// * 재시도 비활성 시 RetryTarget 설정은 무시됩니다.
    /// </summary>
    public bool IsRetryEnabled { get; set; } = true;

    /// <summary>
    /// 재시도 동작 대상 플래그. 기본값: ConnectAndWrite.
    /// </summary>
    public RetryTarget RetryTarget { get; set; } = RetryTarget.ConnectAndWrite;

    /// <summary>
    /// 재시도 최대 횟수. 기본값: 3회.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 재시도 간 지연 시간. 기본값: 200ms.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 재시도 시 재접속 대기 시간 적용 여부. 기본값: true.
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

    #region §4 ─ 커맨드
    /// <summary>
    /// 읽기(Read) 커맨드 목록. 장비로 주기적으로 투입할 커맨드들을 저장합니다.
    /// </summary>
    private readonly List<byte[]> _readCommands = [];

    /// <summary>
    /// 읽기(Read) 커맨드 목록 읽기 전용 인터페이스. 외부에서는 IReadOnlyList<byte[]> 형태로 접근됩니다.
    /// </summary>
    public IReadOnlyList<byte[]> ReadCommands => _readCommands.AsReadOnly();

    /// <summary>
    /// 읽기(Read) 커맨드 추가 메서드. null 입력 시 ArgumentNullException이 발생합니다.
    /// </summary>
    public void AddReadCommand(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _readCommands.Add(command);
    }

    /// <summary>
    /// 읽기(Read) 커맨드 목록 전체 삭제 메서드. ReadCommands 컬렉션은 비워지지만 null이 되지는 않습니다.
    /// </summary>
    public void ClearReadCommands() => _readCommands.Clear();

    #endregion

    #region §5 ─ 채널 동작

    /// <summary>
    /// 주기적(ReadCommands 투입) 간격. 기본값: 100ms.
    /// </summary>
    public TimeSpan PeriodicInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 요청 타임아웃. 장비로부터 응답이 없을 때 대기하는 최대 시간입니다. 기본값: 3초.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 하트비트 간격. 0 또는 음수로 설정 시 하트비트 비활성화. 기본값: 0 (비활성).
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 하트비트 응답 대기 여부. true로 설정 시 하트비트 전송 후 응답을 기다립니다. 기본값: false.
    /// </summary>
    public bool IsHeartbeatAcknowledged { get; set; } = false;

    /// <summary>
    /// ReceiveChannel 용량. 0으로 설정 시 무제한. 양수로 설정 시 최대 대기 메시지 수를 제한하여 메모리 과다 사용 방지. 기본값: 0 (무제한).
    /// </summary>
    public int ReceiveChannelCapacity { get; set; } = 0;

    #endregion

    #region §6 ─ 생성자

    /// <summary>
    /// NetDeviceConfig 생성자. DeviceId와 DeviceName은 필수 매개변수입니다. DeviceName은 null 또는 공백으로 설정할 수 없습니다.
    /// </summary>
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