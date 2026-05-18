// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence · Abstractions/SequenceContextBase.cs
//  역할: ISequenceContext 공통 구현 추상 베이스
//        변수 저장소 + 로그 공통 처리, 장치 접근은 파생 클래스가 구현
// ══════════════════════════════════════════════════════════════════════

using System.Collections.Concurrent;

namespace lssLib.Sequence;

/// <summary>
/// <see cref="ISequenceContext"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <para>
/// 파생 클래스는 <see cref="GetDevice"/> 와 <see cref="IsDeviceConnected"/> 만 구현하면 됩니다.
/// 변수 저장소·로그는 이 클래스가 공통 처리합니다.
/// </para>
///
/// <b>파생 클래스 구현 패턴:</b>
/// <code>
/// // lssLib.Net 에서
/// public sealed class NetSequenceContext : SequenceContextBase
/// {
///     protected override object? GetDeviceCore(int deviceId)
///         => NetDeviceRegistry.Instance.Get(deviceId);
///
///     protected override bool IsDeviceConnectedCore(int deviceId)
///     {
///         var ch = NetDeviceRegistry.Instance.Get(deviceId);
///         return ch?.IsConnected == true;
///     }
///
///     protected override void LogCore(string message)
///         => LogManager.Instance.Info("Sequence", message);
///
///     protected override void LogErrorCore(string message)
///         => LogManager.Instance.Error("Sequence", message);
/// }
///
/// // DB 시퀀스에서
/// public sealed class DbSequenceContext : SequenceContextBase
/// {
///     private readonly Dictionary<int, DbConnection> _connections;
///
///     public DbSequenceContext(Dictionary<int, DbConnection> conns)
///         => _connections = conns;
///
///     protected override object? GetDeviceCore(int deviceId)
///         => _connections.TryGetValue(deviceId, out var conn) ? conn : null;
///
///     protected override bool IsDeviceConnectedCore(int deviceId)
///         => _connections.TryGetValue(deviceId, out var c) &amp;&amp;
///            c.State == System.Data.ConnectionState.Open;
/// }
/// </code>
/// </remarks>
public abstract class SequenceContextBase : ISequenceContext
{
    #region §1 ─ 변수 저장소

    /// <summary>스텝 간 데이터를 공유하는 스레드 안전 변수 저장소.</summary>
    private readonly ConcurrentDictionary<string, object?> _variables = new();

    /// <inheritdoc/>
    public void SetVariable(string key, object? value)
        => _variables[key] = value;

    /// <inheritdoc/>
    public T? GetVariable<T>(string key)
    {
        if (_variables.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return default;
    }

    /// <inheritdoc/>
    public bool HasVariable(string key) => _variables.ContainsKey(key);

    /// <inheritdoc/>
    public void ClearVariables() => _variables.Clear();

    #endregion

    #region §2 ─ ISequenceContext 구현 (공개)

    /// <inheritdoc/>
    public object? GetDevice(int deviceId) => GetDeviceCore(deviceId);

    /// <inheritdoc/>
    public bool IsDeviceConnected(int deviceId) => IsDeviceConnectedCore(deviceId);

    /// <inheritdoc/>
    public void Log(string message)
    {
        LogCore(message);
        OnLog?.Invoke(message);
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
        LogErrorCore(message);
        OnLogError?.Invoke(message);
    }

    #endregion

    #region §3 ─ 이벤트 (외부 로그 후킹용)

    /// <summary>Log 호출 시 발생. 외부 UI 또는 추가 로그 처리에 사용합니다.</summary>
    public event Action<string>? OnLog;

    /// <summary>LogError 호출 시 발생.</summary>
    public event Action<string>? OnLogError;

    #endregion

    #region §4 ─ 추상 메서드 (파생 클래스 필수 구현)

    /// <summary>DeviceId 로 장치 객체를 반환합니다. 없으면 null.</summary>
    protected abstract object? GetDeviceCore(int deviceId);

    /// <summary>DeviceId 의 연결 상태를 반환합니다.</summary>
    protected abstract bool IsDeviceConnectedCore(int deviceId);

    #endregion

    #region §5 ─ 가상 메서드 (선택 오버라이드)

    /// <summary>
    /// 정보 로그 출력. 기본: Console.WriteLine.
    /// <para>lssLib.Log 연동 시 override 하세요.</para>
    /// </summary>
    protected virtual void LogCore(string message)
        => Console.WriteLine($"[Sequence] {message}");

    /// <summary>
    /// 오류 로그 출력. 기본: Console.Error.WriteLine.
    /// <para>lssLib.Log 연동 시 override 하세요.</para>
    /// </summary>
    protected virtual void LogErrorCore(string message)
        => Console.Error.WriteLine($"[Sequence][ERROR] {message}");

    #endregion
}