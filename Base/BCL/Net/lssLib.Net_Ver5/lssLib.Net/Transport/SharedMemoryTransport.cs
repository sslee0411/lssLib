// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/SharedMemoryTransport.cs
//  역할: 공유 메모리 기반 IPC 전송 계층
//
//  메모리 레이아웃: [Flag:4B][Length:4B][Data:NB]
//    Flag=1: 새 데이터 있음 (Writer 가 기록 완료)
//    Flag=0: 읽기 완료 (Reader 가 처리 완료)
// ══════════════════════════════════════════════════════════════════════

using System.IO.MemoryMappedFiles;

namespace lssLib.Net;

/// <summary>
/// 공유 메모리 기반 IPC 전송 계층.
/// </summary>
/// <remarks>
/// <para>
/// <b>메모리 레이아웃:</b>
/// <code>
/// ┌─────────┬──────────┬──────────┐
/// │ Flag 4B │ Length 4B│ Data  NB │
/// └─────────┴──────────┴──────────┘
/// Flag=1: Writer 기록 완료 (Reader 읽기 대기)
/// Flag=0: Reader 처리 완료 (Writer 다음 기록 가능)
/// </code>
/// </para>
/// <para>Writer 는 MemoryMappedFile.CreateOrOpen, Reader 는 OpenExisting 을 사용합니다.</para>
/// <para>Reader 는 5ms 폴링으로 Flag=1 을 감지하고 RaiseDataReceived 를 호출합니다.</para>
/// </remarks>
public sealed class SharedMemoryTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strMapName;
    private readonly SharedMemoryRole _role;
    private readonly long _size;
    private readonly int _pollMs;

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    // 메모리 레이아웃 오프셋
    private const int OFFSET_FLAG = 0;
    private const int OFFSET_LENGTH = 4;
    private const int OFFSET_DATA = 8;

    #endregion

    #region §2 ─ 생성자 / 팩토리

    /// <param name="mapName">공유 메모리 맵 이름. Writer/Reader 간 반드시 동일.</param>
    /// <param name="role">이 인스턴스의 역할 (Writer / Reader)</param>
    /// <param name="size">공유 메모리 크기(bytes). 기본값: 64KB.</param>
    /// <param name="pollIntervalMs">Reader 폴링 간격(ms). 기본값: 5.</param>
    public SharedMemoryTransport(string mapName, SharedMemoryRole role,
        long size = 65536, int pollIntervalMs = 5)
    {
        _strMapName = mapName;
        _role = role;
        _size = size;
        _pollMs = pollIntervalMs;
    }

    /// <summary>SharedMemDeviceConfig 에서 생성합니다. DeviceName → LogSource 자동 주입.</summary>
    public static SharedMemoryTransport FromConfig(SharedMemDeviceConfig cfg)
        => new(cfg.MapName, cfg.Role, cfg.MapSize,
               (int)cfg.PeriodicInterval.TotalMilliseconds)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        // Writer: CreateOrOpen (맵 생성 또는 기존 맵 열기)
        // Reader: OpenExisting (Writer 가 먼저 시작해야 함)
        _mmf = _role == SharedMemoryRole.Writer
            ? MemoryMappedFile.CreateOrOpen(_strMapName, _size)
            : MemoryMappedFile.OpenExisting(_strMapName);

        _accessor = _mmf.CreateViewAccessor();

        if (_role == SharedMemoryRole.Reader)
        {
            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _pollTask = Task.Run(() => PollAsync(_pollCts.Token), _pollCts.Token);
        }
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        _pollCts?.Cancel();
        _accessor?.Dispose();
        _mmf?.Dispose();
        _accessor = null;
        _mmf = null;
        return Task.CompletedTask;
    }

    protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_accessor is null)
            throw new InvalidOperationException($"[{LogSource}] 공유 메모리가 열려있지 않습니다.");

        // [Length:4B][Data:NB] 기록 후 Flag=1 설정 (원자적 시그널)
        _accessor.Write(OFFSET_LENGTH, data.Length);
        _accessor.WriteArray(OFFSET_DATA, data, 0, data.Length);
        _accessor.Write(OFFSET_FLAG, 1);   // 마지막에 Flag 설정 (순서 중요)
        return Task.CompletedTask;
    }

    protected override Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_accessor is null)
            throw new InvalidOperationException($"[{LogSource}] 공유 메모리가 열려있지 않습니다.");

        int dataLen = _accessor.ReadInt32(OFFSET_LENGTH);
        var buf = new byte[dataLen];
        _accessor.ReadArray(OFFSET_DATA, buf, 0, dataLen);
        _accessor.Write(OFFSET_FLAG, 0);   // 읽기 완료 시그널
        return Task.FromResult(buf);
    }

    protected override void DisposeCore()
    {
        _pollCts?.Dispose();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }

    #endregion

    #region §4 ─ Reader 폴링 루프

    /// <summary>
    /// Reader 역할일 때 Flag=1 을 감지하면 데이터를 읽고 RaiseDataReceived 를 호출합니다.
    /// <para>폴링 간격: PeriodicInterval (기본 5ms).</para>
    /// </summary>
    private async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_accessor?.ReadInt32(OFFSET_FLAG) == 1)
            {
                // 데이터 읽기 + Flag=0 초기화
                int dataLen = _accessor.ReadInt32(OFFSET_LENGTH);
                var buf = new byte[dataLen];
                _accessor.ReadArray(OFFSET_DATA, buf, 0, dataLen);
                _accessor.Write(OFFSET_FLAG, 0);
                RaiseDataReceived(buf);
            }
            await Task.Delay(_pollMs, ct).ConfigureAwait(false);
        }
    }

    #endregion
}

/// <summary>공유 메모리 역할.</summary>
public enum SharedMemoryRole
{
    /// <summary>데이터를 기록하는 프로세스.</summary>
    Writer,
    /// <summary>데이터를 읽는 프로세스.</summary>
    Reader
}