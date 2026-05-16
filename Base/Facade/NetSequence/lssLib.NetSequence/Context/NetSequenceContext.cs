// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence · Context/NetSequenceContext.cs
//  역할: lssLib.Sequence.SequenceContextBase 의 Net 구현체
//        NetDeviceRegistry → ISequenceContext 브리지
// ══════════════════════════════════════════════════════════════════════

using lssLib.Net;
using lssLib.Sequence;

namespace lssLib.NetSequence;

/// <summary>
/// lssLib.Net 전용 시퀀스 실행 컨텍스트.
/// <see cref="SequenceContextBase"/> 를 상속하고
/// <see cref="NetDeviceRegistry"/> 를 통해 장치 접근을 구현합니다.
/// </summary>
/// <remarks>
/// <b>기본 사용 (Console 로그):</b>
/// <code>
/// var ctx = new NetSequenceContext();
/// </code>
///
/// <b>lssLib.Log 연동:</b>
/// <code>
/// var ctx = new NetSequenceContext(
///     logAction:      msg => LogManager.Instance.Info("Sequence", msg),
///     logErrorAction: msg => LogManager.Instance.Error("Sequence", msg));
/// </code>
///
/// <b>WPF UI 로그 후킹 (OnLog 이벤트 활용):</b>
/// <code>
/// var ctx = new NetSequenceContext();
/// ctx.OnLog      += msg => Dispatcher.InvokeAsync(() => TxtLog.Text = msg);
/// ctx.OnLogError += msg => Dispatcher.InvokeAsync(() => TxtErr.Text = msg);
/// </code>
/// </remarks>
public sealed class NetSequenceContext : SequenceContextBase
{
    #region §1 ─ 필드

    private readonly Action<string>? _logAction;
    private readonly Action<string>? _logErrorAction;

    #endregion

    #region §2 ─ 생성자

    /// <param name="logAction">
    /// 정보 로그 출력 액션.
    /// null = Console.WriteLine.
    /// lssLib.Log: <c>msg => LogManager.Instance.Info("Sequence", msg)</c>
    /// </param>
    /// <param name="logErrorAction">
    /// 오류 로그 출력 액션.
    /// null = Console.Error.WriteLine.
    /// </param>
    public NetSequenceContext(
        Action<string>? logAction = null,
        Action<string>? logErrorAction = null)
    {
        _logAction = logAction;
        _logErrorAction = logErrorAction;
    }

    #endregion

    #region §3 ─ SequenceContextBase 구현

    /// <inheritdoc/>
    protected override object? GetDeviceCore(int deviceId)
        => NetDeviceRegistry.Instance.Get(deviceId);

    /// <inheritdoc/>
    protected override bool IsDeviceConnectedCore(int deviceId)
    {
        var ch = NetDeviceRegistry.Instance.Get(deviceId);
        return ch?.IsConnected == true;
    }

    /// <inheritdoc/>
    protected override void LogCore(string message)
    {
        if (_logAction is not null)
            _logAction(message);
        else
            Console.WriteLine($"[Sequence] {DateTime.Now:HH:mm:ss.fff} {message}");
    }

    /// <inheritdoc/>
    protected override void LogErrorCore(string message)
    {
        if (_logErrorAction is not null)
            _logErrorAction(message);
        else
            Console.Error.WriteLine(
                $"[Sequence][ERR] {DateTime.Now:HH:mm:ss.fff} {message}");
    }

    #endregion
}