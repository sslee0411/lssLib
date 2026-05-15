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

    public int DeviceId { get; }
    public string DeviceName { get; }
    public abstract NetTransportType TransportType { get; }

    #endregion

    #region §2 ─ 재시도

    public bool IsRetryEnabled { get; set; } = true;
    public RetryTarget RetryTarget { get; set; } = RetryTarget.ConnectAndWrite;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
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

    private readonly List<byte[]> _readCommands = [];
    public IReadOnlyList<byte[]> ReadCommands => _readCommands.AsReadOnly();

    public void AddReadCommand(byte[] command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _readCommands.Add(command);
    }

    public void ClearReadCommands() => _readCommands.Clear();

    #endregion

    #region §5 ─ 채널 동작

    public TimeSpan PeriodicInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.Zero;
    public bool IsHeartbeatAcknowledged { get; set; } = false;
    public int ReceiveChannelCapacity { get; set; } = 0;

    #endregion

    #region §6 ─ 생성자

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