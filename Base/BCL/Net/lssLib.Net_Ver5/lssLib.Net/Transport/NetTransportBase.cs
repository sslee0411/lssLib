// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/NetTransportBase.cs
//  역할: INetTransport 공통 구현 추상 베이스
//        파생 클래스는 ConnectCore/WriteCore/ReadCore 만 구현합니다.
// ══════════════════════════════════════════════════════════════════════

namespace lssLib.Net;

/// <summary>
/// <see cref="INetTransport"/> 공통 구현 추상 베이스.
/// </summary>
/// <remarks>
/// <para>
/// <b>직접 구현보다 이 클래스 상속을 권장합니다.</b>
/// 상태 관리(State), 이벤트(StateChanged/DataReceived), Dispose 처리가 자동으로 처리됩니다.
/// </para>
///
/// <b>파생 클래스 구현 목록:</b>
/// <c>TcpTransport</c> / <c>SerialTransport</c> / <c>UdpTransport</c> /
/// <c>SharedMemoryTransport</c> / <c>NamedPipeTransport</c> /
/// <c>HttpTransport</c> / <c>WebSocketTransport</c> / <c>MqttTransport</c> /
/// <c>VirtualTransport</c>
///
/// <b>파생 클래스가 반드시 구현해야 할 메서드:</b>
/// <list type="table">
///   <listheader><term>메서드</term><description>내용</description></listheader>
///   <item><term><see cref="ConnectCoreAsync"/></term>
///         <description>소켓/포트 연결. 실패 시 예외 throw.</description></item>
///   <item><term><see cref="DisconnectCoreAsync"/></term>
///         <description>소켓/포트 닫기. 이미 닫혀있어도 예외 없어야 함.</description></item>
///   <item><term><see cref="WriteCoreAsync"/></term>
///         <description>바이트 전송.</description></item>
///   <item><term><see cref="ReadCoreAsync(int,CancellationToken)"/></term>
///         <description>고정 길이 또는 버퍼 크기만큼 수신.</description></item>
/// </list>
///
/// <b>선택적 오버라이드 메서드:</b>
/// <list type="table">
///   <item><term><see cref="ReadFrameCoreAsync"/></term>
///         <description>프레임 단위 수신. 기본: 4096B 읽기.</description></item>
///   <item><term><see cref="DisposeCore"/></term>
///         <description>관리 리소스 해제 (소켓, 핸들, CTS 등).</description></item>
/// </list>
///
/// <b>Passive 수신 시 파생 클래스에서 호출:</b>
/// <code>
/// // SerialPort.DataReceived 핸들러 내부
/// _port.DataReceived += (_, _) => {
///     var buf = new byte[_port.BytesToRead];
///     int read = _port.Read(buf, 0, buf.Length);
///     if (read > 0) RaiseDataReceived(buf[..read]);  // ← 상위로 전달
/// };
/// </code>
/// </remarks>
public abstract class NetTransportBase : INetTransport
{
    #region §1 ─ 필드

    private NetState _state = NetState.Disconnected;
    private volatile bool _disposed;

    /// <summary>
    /// 로그 Source 식별자.
    /// <para><c>XxxTransport.FromConfig(cfg)</c> 팩토리 사용 시 cfg.DeviceName 이 자동 주입됩니다.</para>
    /// </summary>
    public string LogSource { get; set; } = "Net.Transport";

    #endregion

    #region §2 ─ INetTransport 구현

    /// <inheritdoc/>
    public NetState State
    {
        get => _state;
        protected set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(value);
        }
    }

    /// <inheritdoc/>
    public event Action<NetState>? StateChanged;

    /// <inheritdoc/>
    public event Action<byte[]>? DataReceived;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        State = NetState.Connecting;
        try
        {
            await ConnectCoreAsync(ct).ConfigureAwait(false);
            State = NetState.Connected;
        }
        catch
        {
            State = NetState.Error;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        try
        {
            await DisconnectCoreAsync(ct).ConfigureAwait(false);
            State = NetState.Disconnected;
        }
        catch { /* 해제 중 오류 무시 */ }
    }

    /// <inheritdoc/>
    public Task WriteAsync(byte[] data, CancellationToken ct = default)
        => WriteCoreAsync(data, ct);

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(int length, CancellationToken ct = default)
        => ReadCoreAsync(length, ct);

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(CancellationToken ct = default)
        => ReadFrameCoreAsync(ct);

    #endregion

    #region §3 ─ 추상 메서드 (파생 클래스 필수 구현)

    /// <summary>실제 연결 로직. 성공 시 반환, 실패 시 예외 throw.</summary>
    protected abstract Task ConnectCoreAsync(CancellationToken ct);

    /// <summary>실제 연결 해제 로직. 이미 닫혀 있어도 예외 없어야 합니다.</summary>
    protected abstract Task DisconnectCoreAsync(CancellationToken ct);

    /// <summary>데이터 전송 로직.</summary>
    protected abstract Task WriteCoreAsync(byte[] data, CancellationToken ct);

    /// <summary>데이터 수신 로직 (고정 길이 또는 가용 바이트만큼).</summary>
    protected abstract Task<byte[]> ReadCoreAsync(int length, CancellationToken ct);

    #endregion

    #region §4 ─ 가상 메서드 (선택 오버라이드)

    /// <summary>
    /// 프레임 단위 수신. 기본 구현: 4096B 읽기.
    /// <para>프레임 경계 인식이 필요한 경우 재정의하세요.</para>
    /// </summary>
    protected virtual Task<byte[]> ReadFrameCoreAsync(CancellationToken ct)
        => ReadCoreAsync(4096, ct);

    /// <summary>파생 클래스 리소스 정리 (소켓, 핸들, CTS 등).</summary>
    protected virtual void DisposeCore() { }

    #endregion

    #region §5 ─ 헬퍼 (파생 클래스에서 호출)

    /// <summary>
    /// 수신 데이터를 NetChannelBase 로 전달합니다.
    /// <para>Passive 수신 루프(SerialPort.DataReceived, SharedMemory 폴링, TCP PassiveReceiveLoop 등)에서 호출합니다.</para>
    /// </summary>
    protected void RaiseDataReceived(byte[] data) => DataReceived?.Invoke(data);

    #endregion

    #region §6 ─ IAsyncDisposable

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        State = NetState.Disposed;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    #endregion
}