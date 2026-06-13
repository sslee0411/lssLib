// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Protocols/ModbusTcpDriver.cs
//  수정: Phase 8R
//    ① TcpDeviceConfig — timeoutMs 파라미터 없음 → 제거
//    ② RequestAsync — timeoutMs / retries 파라미터 없음
//       → CancellationToken 만 전달 (타임아웃은 Config에서 관리)
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Net;

namespace IIoT.CollectorRuntime.Protocols;

public sealed record ModbusTcpConfig(
    string DriverId,
    string Host,
    int    Port      = 502,
    byte   UnitId    = 1,
    int    TimeoutMs = 2000,
    int    RetryCount = 2);

public sealed class ModbusTcpDriver : IProtocolDriver
{
    private const string LogSrc = "ModbusTcpDriver";

    private readonly ModbusTcpConfig _cfg;
    private RequestResponseChannel?  _channel;
    private ushort _transactionId;
    private bool   _disposed;

    public string DriverId    => _cfg.DriverId;
    public bool   IsConnected => _channel is not null;

    public ModbusTcpDriver(ModbusTcpConfig cfg) => _cfg = cfg;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // ★ TcpDeviceConfig(int deviceId, string deviceName, string host, int port)
            var tcpCfg = new TcpDeviceConfig(
                deviceId  : 1,
                deviceName: _cfg.DriverId,
                host      : _cfg.Host,
                port      : _cfg.Port);

            var transport = TcpTransport.FromConfig(tcpCfg);
            _channel = new RequestResponseChannel(
                tcpCfg, transport, new RawProtocol(),
                autoRegister: false);

            await _channel.StartAsync(ct);

            LogManager.Instance.Info(LogSrc,
                $"[{_cfg.DriverId}] 연결 성공 → {_cfg.Host}:{_cfg.Port}");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error(LogSrc,
                $"[{_cfg.DriverId}] 연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_channel is null) return;
        await _channel.StopAsync();
        _channel = null;
        LogManager.Instance.Info(LogSrc, $"[{_cfg.DriverId}] 연결 해제");
    }

    public async Task<TagReadResult> ReadAsync(TagAddressDef tag, CancellationToken ct = default)
    {
        var batch = await ReadBatchAsync([tag], ct);
        return batch.IsSuccess && batch.Values.TryGetValue(tag.TagId, out var val)
            ? TagReadResult.Ok(tag.TagId, val)
            : TagReadResult.Fail(tag.TagId, batch.ErrorMsg);
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags, CancellationToken ct = default)
    {
        if (_channel is null)
            return BatchReadResult.Fail("연결되지 않음");

        try
        {
            var tagList = tags.ToList();
            var groups  = _GroupConsecutiveRegisters(tagList);
            var values  = new Dictionary<string, double>(tagList.Count);

            foreach (var (_, group) in groups)
            {
                var (fc, baseAddr) = _ParseFirstAddress(group[0].Address);
                int quantity       = group.Count;

                byte[] request = _BuildReadRequest(fc, (ushort)baseAddr, (ushort)quantity);

                // ★ RequestAsync(byte[] request, TimeSpan? timeout, CancellationToken ct)
                var result = await _channel.RequestAsync(
                    request,
                    TimeSpan.FromMilliseconds(_cfg.TimeoutMs),
                    ct);

                if (!result.IsOk || result.Data is null)
                {
                    foreach (var t in group)
                        values[t.TagId] = double.NaN;
                    continue;
                }

                var data       = result.Data;
                int dataOffset = 9;
                for (int i = 0; i < group.Count; i++)
                {
                    int byteIdx = dataOffset + i * 2;
                    if (byteIdx + 1 >= data.Length) break;
                    ushort raw = (ushort)((data[byteIdx] << 8) | data[byteIdx + 1]);
                    values[group[i].TagId] = raw;
                }
            }

            return BatchReadResult.Ok(values);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc, $"[{_cfg.DriverId}] 배치 읽기 오류: {ex.Message}");
            return BatchReadResult.Fail(ex.Message);
        }
    }

    private byte[] _BuildReadRequest(byte fc, ushort startAddr, ushort quantity)
    {
        var tid = ++_transactionId;
        return
        [
            (byte)(tid >> 8), (byte)(tid & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            _cfg.UnitId,
            fc,
            (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),
            (byte)(quantity  >> 8), (byte)(quantity  & 0xFF),
        ];
    }

    private static (byte fc, int addr) _ParseFirstAddress(string address)
    {
        if (int.TryParse(address, out int iecAddr))
        {
            if (iecAddr >= 40001) return (0x03, iecAddr - 40001);
            if (iecAddr >= 30001) return (0x04, iecAddr - 30001);
            if (iecAddr >= 10001) return (0x02, iecAddr - 10001);
            if (iecAddr >= 1)     return (0x01, iecAddr - 1);
        }
        if (address.StartsWith("R") && int.TryParse(address[1..], out int rAddr))
            return (0x03, rAddr);
        return (0x03, 0);
    }

    private static List<(int startAddr, List<TagAddressDef> tags)>
        _GroupConsecutiveRegisters(List<TagAddressDef> tags)
    {
        var sorted = tags
            .Select(t => (tag: t, addr: _ParseFirstAddress(t.Address).addr))
            .OrderBy(x => x.addr)
            .ToList();

        var groups = new List<(int, List<TagAddressDef>)>();
        List<TagAddressDef>? current = null;
        int lastAddr = -2, groupStart = 0;

        foreach (var (tag, addr) in sorted)
        {
            if (current is null || addr > lastAddr + 1 || current.Count >= 125)
            {
                current    = [tag];
                groupStart = addr;
                groups.Add((groupStart, current));
            }
            else { current.Add(tag); }
            lastAddr = addr;
        }
        return groups;
    }

    private static double _ApplyScaling(ushort raw, TagAddressDef tag) => raw;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
    }
}
