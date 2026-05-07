// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Infrastructure/NetScheduler.cs
//  역할: 주기 Read + Heartbeat 루프 전담 (Pause/Resume 제어 가능)
// ══════════════════════════════════════════════════════════════════════

// using lssLib.Log;

namespace lssLib.Net;

/// <summary>
/// 주기 Read 루프와 Heartbeat 루프를 전담합니다.
/// </summary>
/// <remarks>
/// <para><see cref="Pause"/> / <see cref="Resume"/> 로 루프를 일시 정지·재개합니다.</para>
/// <para>재접속 시 <see cref="NetConnectionManager.Reconnected"/> 이벤트에서 <see cref="Resume"/> 이 자동 호출됩니다.</para>
/// </remarks>
internal sealed class NetScheduler
{
    #region §1 ─ 필드

    private readonly NetDeviceConfigBase _cfg;
    private readonly NetDispatchPipeline _pipeline;
    private readonly INetProtocol _protocol;
    private readonly Func<bool> _isConnected;

    private volatile bool _paused;
    private Task? _runTask;

    #endregion

    #region §2 ─ 생성자

    internal NetScheduler(NetDeviceConfigBase cfg, NetDispatchPipeline pipeline,
        INetProtocol protocol, Func<bool> isConnected)
    {
        _cfg = cfg;
        _pipeline = pipeline;
        _protocol = protocol;
        _isConnected = isConnected;
    }

    #endregion

    #region §3 ─ 제어

    /// <summary>스케줄러를 시작합니다.</summary>
    internal void Start(CancellationToken ct)
        => _runTask = Task.Run(() => RunAsync(ct), ct);

    /// <summary>
    /// 스케줄러를 일시 정지합니다.
    /// <para>재접속 시작 시 NetChannelBase.OnStateChanged 에서 자동 호출됩니다.</para>
    /// </summary>
    internal void Pause()
    {
        _paused = true;
        //LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] 일시 정지");
    }

    /// <summary>
    /// 스케줄러를 재개합니다.
    /// <para>재접속 성공 후 NetConnectionManager.Reconnected 이벤트에서 자동 호출됩니다.</para>
    /// </summary>
    internal void Resume()
    {
        _paused = false;
        //LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] 재개");
    }

    /// <summary>루프 종료를 기다립니다.</summary>
    internal Task WaitAsync() => _runTask ?? Task.CompletedTask;

    #endregion

    #region §4 ─ 루프

    /// <summary>
    /// 주기 Read 루프와 Heartbeat 루프를 병렬로 실행합니다.
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
        => await Task.WhenAll(
            RunPeriodicReadAsync(ct),
            RunHeartbeatAsync(ct))
        .ConfigureAwait(false);

    /// <summary>
    /// 주기 Read 루프입니다.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task RunPeriodicReadAsync(CancellationToken ct)
    {
        if (_cfg.PeriodicInterval == TimeSpan.Zero) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cfg.PeriodicInterval, ct).ConfigureAwait(false);

                if (_paused || !_isConnected())
                {
                    //LogManager.Instance.Debug(_cfg.DeviceName, $"[Scheduler] 주기 Read 스킵 — Paused={_paused}");
                    continue;
                }

                var cmds = _cfg.ReadCommands;
                if (cmds.Count == 0) continue;

                if (_cfg.IsSequential)
                {
                    foreach (var cmd in cmds){
                        await EnqueueReadAsync(cmd, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    var tasks = cmds.Select(cmd =>
                        _pipeline.EnqueueAsync(
                            NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct).AsTask());
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ex.Message.ToString();
               // LogManager.Instance.Warn(_cfg.DeviceName, $"[Scheduler] 주기 Read 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Heartbeat 루프입니다.
    /// </summary>
    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        if (_cfg.HeartbeatInterval == TimeSpan.Zero) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cfg.HeartbeatInterval, ct).ConfigureAwait(false);

                if (_paused || !_isConnected())
                {
                    // LogManager.Instance.Debug(_cfg.DeviceName, "[Scheduler] Heartbeat 스킵");
                    continue;
                }

                var hb = _protocol.BuildHeartbeat();
                if (hb is null) continue;

                await _pipeline.EnqueueAsync(
                    NetPacket.CreateWrite(hb, NetPriority.Low, ct), ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ex.Message.ToString();
                // LogManager.Instance.Warn(_cfg.DeviceName, $"[Scheduler] Heartbeat 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 주기 Read 명령을 패킷으로 만들어 우선순위 채널에 투입합니다.
    /// </summary>
    private async Task EnqueueReadAsync(byte[] cmd, CancellationToken ct)
        => await _pipeline.EnqueueAsync(
                    NetPacket.CreatePeriodicRead(_protocol.Encode(cmd), ct), ct)
                .ConfigureAwait(false);

    #endregion
}