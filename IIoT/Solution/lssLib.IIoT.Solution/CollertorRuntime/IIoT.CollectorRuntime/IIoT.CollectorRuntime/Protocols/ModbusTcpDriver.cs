// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Protocols/ModbusTcpDriver.cs
//  역할: Modbus TCP 프로토콜 드라이버
//        lssLib.Net TcpDeviceConfig + RequestResponseChannel 사용
//  Phase 8: 신규
//
//  Modbus TCP 패킷 구조 (Function Code 03 — Read Holding Registers):
//    [Transaction ID 2B][Protocol ID 2B][Length 2B][Unit ID 1B]
//    [FC 1B][Start Addr 2B][Quantity 2B]
//    응답: [...MBAP...][FC 1B][Byte Count 1B][Data N*2B]
//
//  주소 규칙 (IEC 61131-3 기준):
//    "40001" → Holding Register 1번 (FC 03, 시작주소 0)
//    "30001" → Input Register 1번    (FC 04, 시작주소 0)
//    "10001" → Discrete Input        (FC 02)
//    "00001" → Coil                  (FC 01)
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Net;

namespace IIoT.CollectorRuntime.Protocols;

/// <summary>Modbus TCP 드라이버 설정</summary>
public sealed record ModbusTcpConfig(
    string DriverId,
    string Host,
    int    Port    = 502,
    byte   UnitId  = 1,
    int    TimeoutMs = 2000,
    int    RetryCount = 2);

/// <summary>
/// Modbus TCP 프로토콜 드라이버.
/// lssLib.Net RequestResponseChannel 을 사용하여 통신합니다.
///
/// 배치 최적화:
///   연속 레지스터 그룹을 단일 FC03 요청으로 묶어 통신 횟수를 최소화합니다.
///   예) 40001, 40002, 40003 → 1회 요청 (시작:0, 개수:3)
/// </summary>
public sealed class ModbusTcpDriver : IProtocolDriver
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "ModbusTcpDriver";

    private readonly ModbusTcpConfig _cfg;
    private RequestResponseChannel?  _channel;
    private ushort _transactionId;
    private bool   _disposed;

    // §2 ─ IProtocolDriver 프로퍼티 ───────────────────────────
    public string DriverId    => _cfg.DriverId;
    public bool   IsConnected => _channel is not null;

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public ModbusTcpDriver(ModbusTcpConfig cfg) => _cfg = cfg;

    // §4 ─ 연결 ───────────────────────────────────────────────
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var tcpCfg = new TcpDeviceConfig(
                deviceId : 1,
                name     : _cfg.DriverId,
                host     : _cfg.Host,
                port     : _cfg.Port,
                timeoutMs: _cfg.TimeoutMs);

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

    // §5 ─ 읽기 ───────────────────────────────────────────────
    public async Task<TagReadResult> ReadAsync(
        TagAddressDef tag, CancellationToken ct = default)
    {
        var batch = await ReadBatchAsync([tag], ct);
        return batch.IsSuccess && batch.Values.TryGetValue(tag.TagId, out var val)
            ? TagReadResult.Ok(tag.TagId, val)
            : TagReadResult.Fail(tag.TagId, batch.ErrorMsg);
    }

    public async Task<BatchReadResult> ReadBatchAsync(
        IEnumerable<TagAddressDef> tags,
        CancellationToken ct = default)
    {
        if (_channel is null)
            return BatchReadResult.Fail("연결되지 않음");

        try
        {
            var tagList = tags.ToList();

            // ── 연속 레지스터 그룹으로 묶기 (Holding Register FC03 기준) ──
            var groups = _GroupConsecutiveRegisters(tagList);
            var values = new Dictionary<string, double>(tagList.Count);

            foreach (var (startAddr, group) in groups)
            {
                var (fc, baseAddr) = _ParseFirstAddress(group[0].Address);
                int quantity       = group.Count;

                byte[] request = _BuildReadRequest(fc, (ushort)baseAddr, (ushort)quantity);
                var result     = await _channel.RequestAsync(
                    deviceId: 1, request, ct,
                    timeoutMs: _cfg.TimeoutMs,
                    retries:   _cfg.RetryCount);

                if (!result.IsOk || result.Data is null)
                {
                    foreach (var t in group)
                        values[t.TagId] = double.NaN;
                    continue;
                }

                // 응답 파싱: MBAP(6) + FC(1) + ByteCount(1) + Data(N*2)
                var data = result.Data;
                int dataOffset = 9; // MBAP 6 + FC 1 + ByteCount 1 = 8, 다음이 data
                for (int i = 0; i < group.Count; i++)
                {
                    int byteIdx = dataOffset + i * 2;
                    if (byteIdx + 1 >= data.Length) break;

                    ushort raw = (ushort)((data[byteIdx] << 8) | data[byteIdx + 1]);
                    values[group[i].TagId] = _ApplyScaling(raw, group[i]);
                }
            }

            return BatchReadResult.Ok(values);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc,
                $"[{_cfg.DriverId}] 배치 읽기 오류: {ex.Message}");
            return BatchReadResult.Fail(ex.Message);
        }
    }

    // §6 ─ Modbus 패킷 빌드 ────────────────────────────────────

    /// <summary>
    /// Modbus TCP MBAP 헤더 + PDU 빌드
    ///   FC 03 (Holding Registers) / FC 04 (Input Registers)
    /// </summary>
    private byte[] _BuildReadRequest(byte fc, ushort startAddr, ushort quantity)
    {
        var tid = ++_transactionId;
        return
        [
            // MBAP Header (6 bytes)
            (byte)(tid >> 8), (byte)(tid & 0xFF),  // Transaction ID
            0x00, 0x00,                             // Protocol ID (Modbus = 0)
            0x00, 0x06,                             // Length = 6 (Unit+FC+Addr+Qty)
            _cfg.UnitId,                            // Unit ID
            // PDU
            fc,                                     // Function Code
            (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),  // Start Address
            (byte)(quantity >> 8),  (byte)(quantity & 0xFF),   // Quantity
        ];
    }

    // §7 ─ 주소 파싱 헬퍼 ──────────────────────────────────────

    /// <summary>
    /// IEC 61131-3 주소 → (FC, 0-based 레지스터 주소) 변환
    ///   "40001" → (FC=03, addr=0)
    ///   "40100" → (FC=03, addr=99)
    ///   "30001" → (FC=04, addr=0)
    /// </summary>
    private static (byte fc, int addr) _ParseFirstAddress(string address)
    {
        if (int.TryParse(address, out int iecAddr))
        {
            if (iecAddr >= 40001) return (0x03, iecAddr - 40001);
            if (iecAddr >= 30001) return (0x04, iecAddr - 30001);
            if (iecAddr >= 10001) return (0x02, iecAddr - 10001);
            if (iecAddr >= 1)     return (0x01, iecAddr - 1);
        }
        // 0-기반 직접 주소 형식 (예: "R100")
        if (address.StartsWith("R") && int.TryParse(address[1..], out int rAddr))
            return (0x03, rAddr);

        return (0x03, 0);
    }

    /// <summary>연속 레지스터끼리 묶어 배치 그룹 생성</summary>
    private static List<(int startAddr, List<TagAddressDef> tags)>
        _GroupConsecutiveRegisters(List<TagAddressDef> tags)
    {
        var sorted = tags
            .Select(t => (tag: t, addr: _ParseFirstAddress(t.Address).addr))
            .OrderBy(x => x.addr)
            .ToList();

        var groups = new List<(int, List<TagAddressDef>)>();
        List<TagAddressDef>? current = null;
        int lastAddr = -2;
        int groupStart = 0;

        foreach (var (tag, addr) in sorted)
        {
            if (current is null || addr > lastAddr + 1 || current.Count >= 125)
            {
                current    = [tag];
                groupStart = addr;
                groups.Add((groupStart, current));
            }
            else
            {
                current.Add(tag);
            }
            lastAddr = addr;
        }

        return groups;
    }

    /// <summary>raw 레지스터 값 → 공학값 변환 (기본: 1:1)</summary>
    private static double _ApplyScaling(ushort raw, TagAddressDef tag) => raw;

    // §8 ─ IAsyncDisposable ────────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
    }
}
