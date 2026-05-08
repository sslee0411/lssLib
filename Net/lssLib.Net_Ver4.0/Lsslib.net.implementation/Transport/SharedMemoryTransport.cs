// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/SharedMemoryTransport.cs
// ══════════════════════════════════════════════════════════════════════

using System.IO.MemoryMappedFiles;

using lssLib.Net;

namespace Lsslib.net.implementation;

/// <summary>공유 메모리 기반 IPC 전송 계층.</summary>
/// <remarks>
/// 레이아웃: [Flag:4B][Length:4B][Data:NB]
/// Flag=1: 새 데이터, Flag=0: 읽기 완료
/// </remarks>
public sealed class SharedMemoryTransport : NetTransportBase
{
    private readonly string _mapName;
    private readonly SharedMemoryRole _role;
    private readonly long _size;
    private readonly int _pollMs;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private const int OFFSET_FLAG = 0;
    private const int OFFSET_LENGTH = 4;
    private const int OFFSET_DATA = 8;

    public SharedMemoryTransport(string mapName, SharedMemoryRole role,
        long size = 65536, int pollIntervalMs = 5)
    {
        _mapName = mapName;
        _role = role;
        _size = size;
        _pollMs = pollIntervalMs;
    }

    public static SharedMemoryTransport FromConfig(SharedMemDeviceConfig cfg)
        => new(cfg.MapName, cfg.Role, cfg.MapSize,
               (int)cfg.PeriodicInterval.TotalMilliseconds)
        { LogSource = cfg.DeviceName };

    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        _mmf = _role == SharedMemoryRole.Writer
            ? MemoryMappedFile.CreateOrOpen(_mapName, _size)
            : MemoryMappedFile.OpenExisting(_mapName);
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
        _accessor?.Dispose(); _mmf?.Dispose();
        _accessor = null; _mmf = null;
        return Task.CompletedTask;
    }

    protected override Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        if (_accessor is null){
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
            //throw new InvalidOperationException($"[{LogSource}] 공유 메모리가 열려있지 않습니다.");
        }
        _accessor.Write(OFFSET_LENGTH, data.Length);
        _accessor.WriteArray(OFFSET_DATA, data, 0, data.Length);
        _accessor.Write(OFFSET_FLAG, 1);
        return Task.CompletedTask;
    }

    protected override Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        if (_accessor is null){
            throw new InvalidOperationException($"포트가 열려있지 않습니다.");
           // throw new InvalidOperationException($"[{LogSource}] 공유 메모리가 열려있지 않습니다.");
        }
        int dataLen = _accessor.ReadInt32(OFFSET_LENGTH);
        var buf = new byte[dataLen];
        _accessor.ReadArray(OFFSET_DATA, buf, 0, dataLen);
        _accessor.Write(OFFSET_FLAG, 0);
        return Task.FromResult(buf);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_accessor is not null && _accessor.ReadInt32(OFFSET_FLAG) == 1)
                RaiseDataReceived(await ReadCoreAsync(0, ct).ConfigureAwait(false));
            await Task.Delay(_pollMs, ct).ConfigureAwait(false);
        }
    }

    protected override void DisposeCore()
    {
        _pollCts?.Dispose();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}

/// <summary>공유 메모리 역할.</summary>
public enum SharedMemoryRole { Writer, Reader }