// ══════════════════════════════════════════════════════════
//  IIoT.Driver.ModbusTcp · ModbusTcpDriver.cs
//  역할: Modbus TCP 실제 통신 구현 (IProtocolDriver)
//        순수 TCP Socket + Modbus ADU 프레임 직접 구현
//        (외부 Modbus 라이브러리 없이 동작)
//
//  지원 기능코드:
//    FC01 — Read Coils            (Coil, 비트 읽기)
//    FC02 — Read Discrete Inputs  (DiscreteInput, 비트 읽기)
//    FC03 — Read Holding Registers (HoldingReg, Word 읽기) ← 주력
//    FC04 — Read Input Registers   (InputReg, Word 읽기)
//    FC05 — Write Single Coil
//    FC06 — Write Single Register
//    FC16 — Write Multiple Registers
//
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using System.Net.Sockets;

namespace IIoT.Driver.ModbusTcp;

/// <summary>
/// Modbus TCP 드라이버.
/// ConnectAsync → ReadTagsAsync (폴링) / WriteTagAsync → DisposeAsync 순서로 사용.
/// </summary>
public sealed class ModbusTcpDriver : IProtocolDriver
{
    // §1 ─ 상태 ────────────────────────────────────────────

    public string DriverName  => "modbus-tcp";
    public bool   IsConnected => _client?.Connected ?? false;

    public event Action<string>?         OnConnected;
    public event Action<string, string>? OnError;

    private TcpClient?    _client;
    private NetworkStream? _stream;
    private DriverConfig?  _config;
    private int            _transactionId;  // Modbus MBAP 트랜잭션 ID

    // §2 ─ 연결 설정 값 ────────────────────────────────────

    private string _host        = "192.168.0.1";
    private int    _port        = 502;
    private byte   _slaveId     = 1;
    private int    _timeoutMs   = 3000;
    private int    _retryCount  = 3;
    private int    _maxBatch    = 120;

    // §3 ─ 연결 / 해제 ─────────────────────────────────────

    public async Task<bool> ConnectAsync(
        DriverConfig config, CancellationToken ct = default)
    {
        _config = config;
        _ReadParams(config);

        for (int attempt = 1; attempt <= _retryCount; attempt++)
        {
            try
            {
                _client?.Dispose();
                _client = new TcpClient
                {
                    SendTimeout    = _timeoutMs,
                    ReceiveTimeout = _timeoutMs
                };

                await _client.ConnectAsync(_host, _port, ct);
                _stream = _client.GetStream();

                OnConnected?.Invoke(DriverName);
                return true;
            }
            catch (Exception ex) when (attempt < _retryCount)
            {
                // 마지막 시도 전에는 계속 재시도
                OnError?.Invoke(DriverName,
                    $"연결 시도 {attempt}/{_retryCount} 실패: {ex.Message}");
                await Task.Delay(500 * attempt, ct);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(DriverName, $"연결 최종 실패: {ex.Message}");
            }
        }
        return false;
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    // §4 ─ 배치 읽기 ───────────────────────────────────────

    /// <summary>
    /// 태그 목록을 연속 주소 단위로 묶어 최소 요청으로 읽습니다.
    ///
    /// ★ 배치 최적화 원리:
    ///   태그 주소를 정렬 → 연속된 주소 범위를 하나의 FC03 요청으로 묶음
    ///   예: 40001, 40002, 40005 → [40001~40005] 1번 요청으로 처리
    ///   (중간에 빠진 40003, 40004 도 읽지만 결과에서 제외)
    /// </summary>
    public async Task<DriverReadResult> ReadTagsAsync(
        IReadOnlyList<TagReadRequest> tags, CancellationToken ct = default)
    {
        if (!IsConnected)
            return DriverReadResult.Fail("미연결 상태", TagQuality.Disconnected);

        var values = new List<TagValue>();

        // 태그를 레지스터 종류(FC) 별로 그룹화
        var groups = _GroupByFunctionCode(tags);

        foreach (var (fc, group) in groups)
        {
            // 연속 주소 범위를 배치로 묶어 읽기
            var batches = _MakeBatches(group, _maxBatch);

            foreach (var batch in batches)
            {
                try
                {
                    var startAddr  = batch.Min(t => _ParseAddress(t.Address));
                    var count      = batch.Max(t => _ParseAddress(t.Address))
                                     - startAddr + _WordSize(batch[0].DataType);

                    var raw = await _ReadRegistersAsync(fc, (ushort)startAddr,
                                                        (ushort)count, ct);
                    if (raw is null)
                    {
                        foreach (var t in batch)
                            values.Add(_MakeValue(t, null, TagQuality.Bad));
                        continue;
                    }

                    // 각 태그 주소에 해당하는 원시값 추출
                    foreach (var tag in batch)
                    {
                        var offset = _ParseAddress(tag.Address) - startAddr;
                        var value  = _ExtractValue(raw, offset, tag.DataType);
                        values.Add(_MakeValue(tag, value, TagQuality.Good));
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(DriverName, $"읽기 오류: {ex.Message}");
                    foreach (var t in batch)
                        values.Add(_MakeValue(t, null, TagQuality.Bad));
                }
            }
        }

        return DriverReadResult.Ok(values);
    }

    // §5 ─ 단일 쓰기 ───────────────────────────────────────

    public async Task<DriverWriteResult> WriteTagAsync(
        TagWriteRequest tag, CancellationToken ct = default)
    {
        if (!IsConnected)
            return DriverWriteResult.Fail("미연결 상태");

        try
        {
            var addr = (ushort)_ParseAddress(tag.Address);

            // Bool / Coil 타입
            if (tag.DataType is "Bool" or "Coil")
            {
                var val = tag.Value is "1" or "true" or "True";
                await _WriteCoilAsync(addr, val, ct);
            }
            // 정수/실수 — FC06 (단일) 또는 FC16 (멀티)
            else
            {
                var regs = _EncodeValue(tag.Value, tag.DataType);
                if (regs.Length == 1)
                    await _WriteSingleRegisterAsync(addr, regs[0], ct);
                else
                    await _WriteMultipleRegistersAsync(addr, regs, ct);
            }
            return DriverWriteResult.Ok();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(DriverName, $"쓰기 오류 [{tag.Address}]: {ex.Message}");
            return DriverWriteResult.Fail(ex.Message);
        }
    }

    // §6 ─ Modbus 프레임 구현 ──────────────────────────────

    /// <summary>FC03/FC04 읽기 요청 전송 + 응답 수신</summary>
    private async Task<ushort[]?> _ReadRegistersAsync(
        byte fc, ushort startAddr, ushort count, CancellationToken ct)
    {
        if (_stream is null) return null;

        // MBAP 헤더(6) + PDU(6) = 12바이트 요청
        var txId = (ushort)Interlocked.Increment(ref _transactionId);
        var req  = new byte[12];

        // MBAP 헤더
        req[0] = (byte)(txId >> 8);   req[1] = (byte)txId;       // Transaction ID
        req[2] = 0; req[3] = 0;                                    // Protocol ID (Modbus=0)
        req[4] = 0; req[5] = 6;                                    // Length (PDU 6바이트)
        req[6] = _slaveId;                                         // Unit ID

        // PDU
        req[7]  = fc;                                              // Function Code
        req[8]  = (byte)(startAddr >> 8); req[9]  = (byte)startAddr;
        req[10] = (byte)(count >> 8);     req[11] = (byte)count;

        await _stream.WriteAsync(req, ct);

        // 응답 수신 (MBAP 6 + Unit 1 + FC 1 + ByteCount 1 + Data)
        var header = new byte[9];
        await _stream.ReadExactlyAsync(header, ct);

        var byteCount = header[8];
        var data      = new byte[byteCount];
        await _stream.ReadExactlyAsync(data, ct);

        // 오류 응답 체크 (FC + 0x80 = 에러)
        if ((header[7] & 0x80) != 0) return null;

        // ushort 배열로 변환
        var result = new ushort[byteCount / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);

        return result;
    }

    /// <summary>FC05 단일 코일 쓰기</summary>
    private async Task _WriteCoilAsync(ushort addr, bool value, CancellationToken ct)
    {
        if (_stream is null) return;
        var txId = (ushort)Interlocked.Increment(ref _transactionId);
        var req  = new byte[12];
        req[0] = (byte)(txId >> 8); req[1] = (byte)txId;
        req[2] = 0; req[3] = 0; req[4] = 0; req[5] = 6;
        req[6] = _slaveId; req[7] = 0x05;
        req[8] = (byte)(addr >> 8); req[9] = (byte)addr;
        req[10] = value ? (byte)0xFF : (byte)0x00; req[11] = 0x00;
        await _stream.WriteAsync(req, ct);
        var resp = new byte[12];
        await _stream.ReadExactlyAsync(resp, ct);
    }

    /// <summary>FC06 단일 레지스터 쓰기</summary>
    private async Task _WriteSingleRegisterAsync(
        ushort addr, ushort value, CancellationToken ct)
    {
        if (_stream is null) return;
        var txId = (ushort)Interlocked.Increment(ref _transactionId);
        var req  = new byte[12];
        req[0] = (byte)(txId >> 8); req[1] = (byte)txId;
        req[2] = 0; req[3] = 0; req[4] = 0; req[5] = 6;
        req[6] = _slaveId; req[7] = 0x06;
        req[8] = (byte)(addr >> 8); req[9] = (byte)addr;
        req[10] = (byte)(value >> 8); req[11] = (byte)value;
        await _stream.WriteAsync(req, ct);
        var resp = new byte[12];
        await _stream.ReadExactlyAsync(resp, ct);
    }

    /// <summary>FC16 다중 레지스터 쓰기</summary>
    private async Task _WriteMultipleRegistersAsync(
        ushort addr, ushort[] values, CancellationToken ct)
    {
        if (_stream is null) return;
        var txId    = (ushort)Interlocked.Increment(ref _transactionId);
        var regCount = (ushort)values.Length;
        var byteCount = (byte)(regCount * 2);
        var pduLen   = (ushort)(7 + byteCount);
        var req      = new byte[6 + pduLen];

        req[0] = (byte)(txId >> 8); req[1] = (byte)txId;
        req[2] = 0; req[3] = 0;
        req[4] = (byte)(pduLen >> 8); req[5] = (byte)pduLen;
        req[6] = _slaveId; req[7] = 0x10;
        req[8] = (byte)(addr >> 8); req[9] = (byte)addr;
        req[10] = (byte)(regCount >> 8); req[11] = (byte)regCount;
        req[12] = byteCount;
        for (int i = 0; i < values.Length; i++)
        {
            req[13 + i * 2] = (byte)(values[i] >> 8);
            req[14 + i * 2] = (byte)values[i];
        }
        await _stream.WriteAsync(req, ct);
        var resp = new byte[12];
        await _stream.ReadExactlyAsync(resp, ct);
    }

    // §7 ─ 주소 파싱 / 값 변환 헬퍼 ──────────────────────

    /// <summary>
    /// Modbus 주소 파싱.
    /// 지원 형식:
    ///   40001 → HoldingReg 0번  (40001 = 4xxxx 오프셋 제거)
    ///   30001 → InputReg 0번
    ///   00001 → Coil 0번
    ///   10001 → DiscreteInput 0번
    ///   0     → 직접 인덱스
    /// </summary>
    private static int _ParseAddress(string address)
    {
        if (!int.TryParse(address, out var raw)) return 0;
        return raw switch
        {
            >= 40001 => raw - 40001,  // Holding Register
            >= 30001 => raw - 30001,  // Input Register
            >= 10001 => raw - 10001,  // Discrete Input
            >= 1     => raw - 1,      // Coil (1-based)
            _        => raw           // 직접 인덱스
        };
    }

    private static byte _GetFunctionCode(string address, string dataType)
    {
        if (!int.TryParse(address, out var raw)) return 0x03;
        if (dataType is "Bool" or "Coil") return raw >= 10001 ? (byte)0x02 : (byte)0x01;
        return raw >= 30001 && raw <= 39999 ? (byte)0x04 : (byte)0x03;
    }

    private static int _WordSize(string dataType) => dataType switch
    {
        "Float32" or "Int32" or "UInt32" => 2,
        "Float64" or "Int64" or "UInt64" => 4,
        _ => 1
    };

    private static object? _ExtractValue(ushort[] raw, int offset, string dataType)
    {
        if (offset >= raw.Length) return null;
        return dataType switch
        {
            "Bool"    => (raw[offset] & 0x0001) != 0,
            "Int16"   => (short)raw[offset],
            "UInt16"  => raw[offset],
            "Int32"   => offset + 1 < raw.Length
                         ? (int)((raw[offset] << 16) | raw[offset + 1])
                         : (int)raw[offset],
            "UInt32"  => offset + 1 < raw.Length
                         ? (uint)((raw[offset] << 16) | raw[offset + 1])
                         : (uint)raw[offset],
            "Float32" => offset + 1 < raw.Length
                         ? BitConverter.ToSingle(BitConverter.GetBytes(
                             (raw[offset] << 16) | raw[offset + 1]))
                         : 0f,
            _         => (object)(int)raw[offset]
        };
    }

    private static ushort[] _EncodeValue(string value, string dataType)
    {
        return dataType switch
        {
            "Int16"  or "UInt16" => [(ushort)(int.Parse(value) & 0xFFFF)],
            "Int32"  or "UInt32" => _Split32(uint.Parse(value)),
            "Float32"            => _SplitFloat(float.Parse(value)),
            _                    => [(ushort)(int.Parse(value) & 0xFFFF)]
        };
    }

    private static ushort[] _Split32(uint v)
        => [(ushort)(v >> 16), (ushort)(v & 0xFFFF)];

    private static ushort[] _SplitFloat(float v)
    {
        var bytes = BitConverter.GetBytes(v);
        var u     = BitConverter.ToUInt32(bytes);
        return [(ushort)(u >> 16), (ushort)(u & 0xFFFF)];
    }

    private static TagValue _MakeValue(TagReadRequest t, object? v, TagQuality q)
        => new(t.TagId, v, q, DateTimeOffset.UtcNow);

    // §8 ─ 배치 그룹화 헬퍼 ───────────────────────────────

    private static Dictionary<byte, List<TagReadRequest>> _GroupByFunctionCode(
        IReadOnlyList<TagReadRequest> tags)
    {
        var result = new Dictionary<byte, List<TagReadRequest>>();
        foreach (var t in tags)
        {
            var fc = _GetFunctionCode(t.Address, t.DataType);
            if (!result.TryGetValue(fc, out var list))
                result[fc] = list = new();
            list.Add(t);
        }
        return result;
    }

    private static List<List<TagReadRequest>> _MakeBatches(
        List<TagReadRequest> tags, int maxSize)
    {
        var sorted  = tags.OrderBy(t => _ParseAddress(t.Address)).ToList();
        var batches = new List<List<TagReadRequest>>();
        var current = new List<TagReadRequest>();

        foreach (var tag in sorted)
        {
            if (current.Count == 0)
            {
                current.Add(tag);
                continue;
            }
            var span = _ParseAddress(tag.Address)
                     - _ParseAddress(current[0].Address) + 1;
            if (span <= maxSize)
                current.Add(tag);
            else
            {
                batches.Add(current);
                current = [tag];
            }
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }

    // §9 ─ 파라미터 읽기 ───────────────────────────────────

    private void _ReadParams(DriverConfig config)
    {
        var p     = config.Params ?? new();
        _host     = p.GetValueOrDefault("Host",        "192.168.0.1");
        _port     = int.TryParse(p.GetValueOrDefault("Port",        "502"),  out var port)  ? port  : 502;
        _slaveId  = byte.TryParse(p.GetValueOrDefault("SlaveId",    "1"),    out var sid)   ? sid   : (byte)1;
        _timeoutMs= int.TryParse(p.GetValueOrDefault("TimeoutMs",   "3000"), out var tms)   ? tms   : 3000;
        _retryCount= int.TryParse(p.GetValueOrDefault("RetryCount", "3"),    out var retry) ? retry : 3;
        _maxBatch = int.TryParse(p.GetValueOrDefault("MaxBatchSize","120"),   out var mb)    ? mb    : 120;
    }

    // §10 ─ 리소스 해제 ────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
