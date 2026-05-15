// ====================================================================
//  lssLib.Binary — RingBuffer.cs
//  고급 통신 처리용 순환 버퍼
//
//  [설계 원칙]
//  - TCP/Serial 수신 루프와 파싱 루프를 분리하는 통신 버퍼
//  - STX 기반 고정/가변 길이 프레임 자동 추출
//  - STX 이전 쓰레기 데이터 자동 제거
//  - Thread-safe 옵션 (lock 기반, 생산자/소비자 패턴)
//  - Peek: 소비 없이 미리 보기 (Length 필드 확인 등)
//  - TryWrite: 오버플로우 예외 없이 bool 반환
//
//  [StreamParser 와의 차이]
//  StreamParser: 이미 받은 완성된 byte[] 에서 프레임 탐색 (일회성)
//  RingBuffer  : 지속적인 수신 → 파싱 사이클 관리 (순환, 재사용)
// ====================================================================

using System.Buffers.Binary;

namespace lssLib.Binary
{
    /// <summary>
    /// TCP/Serial 수신 스트림용 순환 버퍼.
    ///
    /// <para><b>■ StreamParser 와의 차이점</b></para>
    /// <para><see cref="StreamParser"/>: 이미 수신된 완성된 <c>byte[]</c> 에서 일회성 탐색.</para>
    /// <para><see cref="RingBuffer"/>: 지속적인 수신-파싱 사이클을 위한 상태 보존 버퍼.</para>
    /// <para>TCP 처럼 데이터가 조각조각 도착할 때, 수신 루프와 파싱 루프를 분리할 때 사용합니다.</para>
    ///
    /// <para><b>■ 핵심 기능</b></para>
    /// <list type="bullet">
    /// <item>STX 이전 쓰레기 데이터 자동 제거</item>
    /// <item>고정 길이 프레임 (<see cref="TryReadFrame"/>)</item>
    /// <item>가변 길이 프레임 (<see cref="TryReadVariableFrame"/>)</item>
    /// <item>소비 없는 미리보기 (<see cref="Peek"/>/<see cref="PeekByte"/>)</item>
    /// <item>Thread-safe 옵션 (lock 기반)</item>
    /// </list>
    ///
    /// <example><code>
    /// // ─── 기본 사용 패턴: 수신 루프 + 파싱 루프 ───────────────────
    ///
    /// var ring = new RingBuffer(capacity:4096);
    ///
    /// var schema = new BufSchema()
    ///     .Then("STX",     BufType.UInt8)
    ///     .Then("DevID",   BufType.UInt8)
    ///     .Then("TempADC", BufType.UInt16BE)
    ///     .Then("Value",   BufType.FloatBE)
    ///     .Then("CRC",     BufType.UInt8);
    ///
    /// // 수신 루프: 데이터 도착할 때마다 Write
    /// byte[] rxBuf = new byte[256];
    /// while (IsConnected)
    /// {
    ///     int n = serialPort.Read(rxBuf, 0, rxBuf.Length);
    ///     ring.Write(rxBuf, 0, n);
    ///     ProcessFrames(ring, schema);   // 즉시 파싱 시도
    /// }
    ///
    /// // 파싱 루프: 프레임 완성될 때마다 처리
    /// void ProcessFrames(RingBuffer ring, BufSchema schema)
    /// {
    ///     // 고정 8바이트 프레임, STX=0xAA
    ///     while (ring.TryReadFrame(stx:0xAA, length:8, out byte[] frame))
    ///     {
    ///         // CRC 검증
    ///         if (frame.Crc8(0, 7) != frame[7]) continue;
    ///
    ///         var result = frame.ToParser().Parse(schema);
    ///         double temp = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
    ///         Console.WriteLine($"온도: {temp:F2}°C  값: {result.GetFloat("Value"):F2}");
    ///     }
    /// }
    ///
    /// // ─── 가변 길이 프레임 패턴 ────────────────────────────────────
    ///
    /// // 프레임: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
    /// // overhead = 1(STX)+1(FC)+2(Length)+4(CRC32) = 8
    ///
    /// while (ring.TryReadVariableFrame(
    ///     stx:          0xAA,
    ///     lengthOffset: 2,      // STX 기준 Length 필드 위치
    ///     lengthSize:   2,      // Length 필드 2바이트
    ///     bigEndian:    true,   // BE
    ///     overhead:     8,      // 헤더(4B) + CRC32(4B)
    ///     out byte[] frame))
    /// {
    ///     if (!frame.VerifyCrc32()) continue;  // CRC 검증
    ///
    ///     byte[] payload = frame[..^4];         // CRC 제외
    ///     var result = payload.ToParser().Parse(payloadSchema);
    ///     ProcessResult(result);
    /// }
    ///
    /// // ─── Thread-safe 생산자/소비자 패턴 ─────────────────────────
    ///
    /// var ring = new RingBuffer(8192, threadSafe:true);
    ///
    /// // 생산자 스레드 (수신)
    /// Task.Run(async () =>
    /// {
    ///     byte[] buf = new byte[1024];
    ///     while (true)
    ///     {
    ///         int n = await stream.ReadAsync(buf);
    ///         ring.Write(buf, 0, n);
    ///     }
    /// });
    ///
    /// // 소비자 스레드 (파싱)
    /// Task.Run(() =>
    /// {
    ///     while (true)
    ///     {
    ///         while (ring.TryReadFrame(0xAA, 32, out var f))
    ///             Dispatcher.Invoke(() => UpdateUI(f));
    ///         Thread.Sleep(1);
    ///     }
    /// });
    /// </code></example>
    /// </summary>
    public sealed class RingBuffer
    {
        private readonly byte[] _buf;
        private readonly int _capacity;
        private readonly bool _threadSafe;
        private readonly object? _lock;

        private int _writePos;
        private int _readPos;
        private int _count;

        /// <summary>버퍼 전체 용량 (바이트).</summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 현재 저장된 바이트 수.
        /// <example><code>
        /// Console.WriteLine($"수신: {ring.Count}B / {ring.Capacity}B");
        ///
        /// // 최소 프레임 크기 확인 후 파싱 시도
        /// if (ring.Count >= MinFrameSize)
        ///     TryParseFrames(ring);
        /// </code></example>
        /// </summary>
        public int Count
        {
            get
            {
                if (_threadSafe) lock (_lock!) return _count;
                return _count;
            }
        }

        /// <summary>빈 공간 (Capacity - Count).</summary>
        public int Available => _capacity - Count;

        /// <summary>저장된 데이터가 있으면 true.</summary>
        public bool HasData => Count > 0;

        /// <summary>
        /// RingBuffer 를 생성합니다.
        /// <example><code>
        /// // 기본 생성 (4KB, 단일 스레드)
        /// var ring = new RingBuffer(4096);
        ///
        /// // Thread-safe 모드 (생산자/소비자 분리)
        /// // lock 기반이므로 성능보다 안전성 우선 시 사용
        /// var ring = new RingBuffer(8192, threadSafe:true);
        ///
        /// // 빠른 프레임 처리가 필요하면 capacity 여유있게 설정
        /// // 최대 예상 프레임 크기 × 10 정도 권장
        /// var ring = new RingBuffer(capacity: maxFrameSize * 10);
        /// </code></example>
        /// </summary>
        public RingBuffer(int capacity, bool threadSafe = false)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _buf = new byte[capacity];
            _threadSafe = threadSafe;
            _lock = threadSafe ? new object() : null;
        }

        // ── Write ─────────────────────────────────────────────────────

        /// <summary>
        /// byte[] 를 버퍼에 씁니다.
        /// <example><code>
        /// // 전체 배열
        /// ring.Write(receivedBytes);
        ///
        /// // offset/count 지정 (소켓 수신 패턴)
        /// byte[] buf = new byte[4096];
        /// int n = socket.Receive(buf);
        /// ring.Write(buf, offset:0, count:n);
        ///
        /// // Serial 수신
        /// int n2 = serialPort.Read(buf, 0, buf.Length);
        /// ring.Write(buf, 0, n2);
        /// </code></example>
        /// <exception cref="InvalidOperationException">버퍼 오버플로우 (Available &lt; count).</exception>
        /// </summary>
        public void Write(byte[] data, int offset = 0, int? count = null)
        {
            int n = count ?? data.Length - offset;
            if (n <= 0) return;
            if (_threadSafe) { lock (_lock!) WriteCore(data, offset, n); }
            else WriteCore(data, offset, n);
        }

        /// <summary>
        /// 단일 바이트를 씁니다.
        /// <example><code>
        /// ring.WriteByte(0xAA);  // STX 수동 삽입 (테스트용)
        /// </code></example>
        /// </summary>
        public void WriteByte(byte value)
        {
            if (_threadSafe) { lock (_lock!) WriteByteCore(value); }
            else WriteByteCore(value);
        }

        /// <summary>
        /// 쓰기를 시도합니다. 공간 부족 시 예외 없이 false 반환.
        /// <example><code>
        /// // 오버플로우 감지 후 처리
        /// if (!ring.TryWrite(newData))
        /// {
        ///     Console.WriteLine($"버퍼 부족: 필요={newData.Length}B 여유={ring.Available}B");
        ///     ring.Clear();        // 강제 초기화 (데이터 손실 허용)
        ///     ring.Write(newData); // 재시도
        /// }
        /// </code></example>
        /// </summary>
        public bool TryWrite(byte[] data, int offset = 0, int? count = null)
        {
            int n = count ?? data.Length - offset;
            if (n > Available) return false;
            Write(data, offset, n);
            return true;
        }

        // ── Read ──────────────────────────────────────────────────────

        /// <summary>
        /// 지정한 바이트 수를 읽고 소비합니다.
        /// <example><code>
        /// // 프레임 헤더 4바이트 읽기
        /// byte[] header = ring.Read(4);
        /// int length = header.ReadUInt16BE(2);   // Length 필드
        ///
        /// // 나머지 읽기
        /// byte[] body = ring.Read(length);
        /// </code></example>
        /// </summary>
        public byte[] Read(int count)
        {
            if (_threadSafe) { lock (_lock!) return ReadCore(count); }
            return ReadCore(count);
        }

        /// <summary>
        /// 버퍼를 소비하지 않고 미리 봅니다 (Peek).
        /// <example><code>
        /// // STX 확인 후 처리 방향 결정
        /// if (ring.Count >= 1)
        /// {
        ///     byte stx = ring.PeekByte(0);
        ///     if (stx == 0xAA)      ProcessTypeA(ring);
        ///     else if (stx == 0xBB) ProcessTypeB(ring);
        ///     else                  ring.SkipTo(0xAA);  // 쓰레기 제거
        /// }
        ///
        /// // Length 필드를 미리 읽어 총 프레임 크기 계산
        /// if (ring.Count >= 4)
        /// {
        ///     byte[] header  = ring.Peek(4);
        ///     ushort dataLen = header.ReadUInt16BE(2);
        ///     int total      = 4 + dataLen + 4;  // header + data + crc32
        ///
        ///     if (ring.Count >= total)
        ///     {
        ///         byte[] frame = ring.Read(total);
        ///         ProcessFrame(frame);
        ///     }
        /// }
        /// </code></example>
        /// </summary>
        public byte[] Peek(int count)
        {
            if (_threadSafe) { lock (_lock!) return PeekCore(count); }
            return PeekCore(count);
        }

        /// <summary>
        /// 특정 offset 의 바이트를 소비 없이 읽습니다.
        /// <example><code>
        /// byte stx = ring.PeekByte(0);   // 첫 바이트 확인
        /// byte fc  = ring.PeekByte(1);   // 두번째 바이트 확인
        ///
        /// // STX 확인
        /// if (ring.Count > 0 &amp;&amp; ring.PeekByte(0) != 0xAA)
        ///     ring.SkipTo(0xAA);
        /// </code></example>
        /// </summary>
        public byte PeekByte(int offset = 0)
        {
            if (_threadSafe) { lock (_lock!) return PeekByteCore(offset); }
            return PeekByteCore(offset);
        }

        // ── 프레임 추출 ───────────────────────────────────────────────

        /// <summary>
        /// 고정 길이 프레임을 탐색하여 추출합니다.
        /// <para>STX 를 찾고, STX 이전 쓰레기를 자동으로 제거하며, length 바이트가 있으면 frame 에 반환.</para>
        /// <para>STX 가 없으면 전체 버퍼를 버립니다 (쓰레기로 간주).</para>
        /// <example><code>
        /// // ─── 기본 사용 ─────────────────────────────────────────────
        ///
        /// // 고정 8바이트 프레임: [STX:1B][ID:1B][TempADC:2B][Value:4B]
        /// var schema = new BufSchema()
        ///     .Then("STX",     BufType.UInt8)
        ///     .Then("ID",      BufType.UInt8)
        ///     .Then("TempADC", BufType.UInt16BE)
        ///     .Then("Value",   BufType.FloatBE);
        ///
        /// // 데이터 수신
        /// ring.Write(tcpData);
        ///
        /// // 프레임 추출 루프
        /// while (ring.TryReadFrame(stx:0xAA, length:8, out byte[] frame))
        /// {
        ///     var result = frame.ToParser().Parse(schema);
        ///
        ///     int   id   = result.GetInt("ID");
        ///     double temp = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
        ///     float val  = result.GetFloat("Value");
        ///
        ///     Console.WriteLine($"ID={id}  Temp={temp:F2}°C  Val={val:F4}");
        /// }
        ///
        /// // ─── 쓰레기 데이터가 섞인 경우 ────────────────────────────
        ///
        /// // 수신 버퍼: FF FF AA 01 09C4 41200000 CC CC AA 02 0E74 42000000
        /// //            ^^^^^ (쓰레기)                 ^^^^^ (쓰레기)
        /// // TryReadFrame 은 STX(0xAA) 이전 쓰레기를 자동으로 건너뜁니다.
        ///
        /// ring.Write(new byte[]{0xFF,0xFF, 0xAA,0x01,0x09,0xC4,0x41,0x20,0x00,0x00,
        ///                       0xCC,0xCC, 0xAA,0x02,0x0E,0x74,0x42,0x00,0x00,0x00});
        ///
        /// int count = 0;
        /// while (ring.TryReadFrame(0xAA, 8, out var f))
        ///     Console.WriteLine($"프레임{++count}: {f.ToHexString()}");
        /// // 프레임1: AA 01 09 C4 41 20 00 00
        /// // 프레임2: AA 02 0E 74 42 00 00 00
        /// </code></example>
        /// </summary>
        public bool TryReadFrame(byte stx, int length, out byte[] frame)
        {
            if (_threadSafe) { lock (_lock!) return TryReadFrameCore(stx, length, out frame); }
            return TryReadFrameCore(stx, length, out frame);
        }

        /// <summary>
        /// 가변 길이 프레임을 탐색하여 추출합니다.
        /// 프레임 내 Length 필드를 읽어 총 프레임 크기를 동적으로 결정합니다.
        /// <example><code>
        /// // ─── 프레임 구조별 파라미터 설정 예시 ─────────────────────
        ///
        /// // 구조 1: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
        /// //         overhead = STX(1)+FC(1)+Length(2)+CRC32(4) = 8
        /// ring.TryReadVariableFrame(
        ///     stx:0xAA, lengthOffset:2, lengthSize:2,
        ///     bigEndian:true, overhead:8, out frame);
        ///
        /// // 구조 2: [STX:1B][Length:1B][Data:NB][XOR:1B]
        /// //         overhead = STX(1)+Length(1)+XOR(1) = 3
        /// ring.TryReadVariableFrame(
        ///     stx:0xAA, lengthOffset:1, lengthSize:1,
        ///     bigEndian:false, overhead:3, out frame);
        ///
        /// // 구조 3: [STX:1B][DevID:1B][FC:1B][Length:2B LE][Data:NB][CRC16:2B LE]
        /// //         overhead = STX(1)+DevID(1)+FC(1)+Length(2)+CRC16(2) = 7
        /// ring.TryReadVariableFrame(
        ///     stx:0xAA, lengthOffset:3, lengthSize:2,
        ///     bigEndian:false, overhead:7, out frame);
        ///
        /// // ─── 가변 프레임 파싱 예시 ───────────────────────────────────
        ///
        /// var headerSchema = new BufSchema()
        ///     .Then("STX",    BufType.UInt8)
        ///     .Then("FC",     BufType.UInt8)
        ///     .Then("Length", BufType.UInt16BE);
        ///
        /// while (ring.TryReadVariableFrame(0xAA, 2, 2, true, 8, out byte[] frame))
        /// {
        ///     // CRC32 검증
        ///     if (!frame.VerifyCrc32())
        ///     {
        ///         Console.WriteLine($"CRC 오류: {frame.ToHexString()}");
        ///         continue;
        ///     }
        ///
        ///     // CRC 제외 페이로드 파싱
        ///     byte[] payload = frame[..^4];
        ///     var result = payload.ToParser().Parse(fullSchema);
        ///
        ///     byte   fc    = result.Get&lt;byte&gt;("FC");
        ///     decimal price = result.GetDecimal("Price");
        ///     Console.WriteLine($"FC=0x{fc:X2}  Price={price:G}m");
        /// }
        /// </code></example>
        /// </summary>
        public bool TryReadVariableFrame(byte stx, int lengthOffset, int lengthSize,
            bool bigEndian, int overhead, out byte[] frame)
        {
            if (_threadSafe)
            { lock (_lock!) return TryReadVariableFrameCore(stx, lengthOffset, lengthSize, bigEndian, overhead, out frame); }
            return TryReadVariableFrameCore(stx, lengthOffset, lengthSize, bigEndian, overhead, out frame);
        }

        /// <summary>
        /// STX 이전 데이터를 버립니다. 수신 동기화에 사용합니다.
        /// <example><code>
        /// // 연결 초기화 시 이전 잔여 데이터 제거
        /// ring.SkipTo(stx:0xAA);
        ///
        /// // 연속 오류 시 재동기화
        /// int skipped = ring.SkipTo(0xAA);
        /// if (skipped > 0)
        ///     Console.WriteLine($"동기화: {skipped}바이트 버림");
        /// </code></example>
        /// </summary>
        public int SkipTo(byte stx)
        {
            if (_threadSafe) { lock (_lock!) return SkipToCore(stx); }
            return SkipToCore(stx);
        }

        /// <summary>
        /// 버퍼를 완전히 초기화합니다.
        /// <example><code>
        /// // 연결 재시작 시 초기화
        /// ring.Clear();
        /// Console.WriteLine("버퍼 초기화 완료");
        ///
        /// // 심각한 오류 후 강제 초기화
        /// if (consecutiveErrors > MaxErrors)
        /// {
        ///     ring.Clear();
        ///     consecutiveErrors = 0;
        /// }
        /// </code></example>
        /// </summary>
        public void Clear()
        {
            if (_threadSafe) { lock (_lock!) { _readPos = _writePos = _count = 0; } }
            else { _readPos = _writePos = _count = 0; }
        }

        /// <summary>
        /// 현재 버퍼 내용을 HEX 문자열로 반환합니다 (소비 없음).
        /// <example><code>
        /// Console.WriteLine($"버퍼: {ring.ToHex()}");
        /// </code></example>
        /// </summary>
        public string ToHex(string sep = " ")
        {
            var data = PeekCore(_count);
            return string.Join(sep, data.Select(b => b.ToString("X2")));
        }

        /// <summary>"RingBuffer[Count/Capacity bytes]" 형식 문자열.</summary>
        public override string ToString() => $"RingBuffer[{Count}/{_capacity} bytes]";

        // ── 내부 구현 ─────────────────────────────────────────────────

        /// <summary>
        /// 버퍼에 데이터를 씁니다. 공간 부족 시 예외 발생
        /// </summary>
        private void WriteCore(byte[] data, int offset, int n)
        {
            if (n > _capacity - _count)
                throw new InvalidOperationException(
                    $"RingBuffer 오버플로우: 필요={n}B 여유={_capacity - _count}B (Capacity={_capacity})");
            for (int i = 0; i < n; i++)
            {
                _buf[_writePos] = data[offset + i];
                _writePos = (_writePos + 1) % _capacity;
            }
            _count += n;
        }
        /// <summary>
        /// 버퍼에 단일 바이트를 씁니다. 공간 부족 시 예외발생
        /// </summary>
        private void WriteByteCore(byte v)
        {
            if (_count >= _capacity)
                throw new InvalidOperationException("RingBuffer 오버플로우 (1바이트)");
            _buf[_writePos] = v;
            _writePos = (_writePos + 1) % _capacity;
            _count++;
        }

        /// <summary>
        /// 버퍼에서 지정한 바이트 수를 읽고 소비함
        /// 데이터 부족 시 예외 발생
        /// </summary>
        private byte[] ReadCore(int n)
        {
            if (n > _count)
                throw new InvalidOperationException(
                    $"읽기 데이터 부족: 요청={n}B 현재={_count}B");
            var result = new byte[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = _buf[_readPos];
                _readPos = (_readPos + 1) % _capacity;
            }
            _count -= n;
            return result;
        }

        /// <summary>
        /// 버퍼에서 지정한 바이트 수를 소비 없이 미리 봄 (Peek).
        /// </summary>
        private byte[] PeekCore(int n)
        {
            int actual = Math.Min(n, _count);
            var result = new byte[actual];
            int pos = _readPos;
            for (int i = 0; i < actual; i++)
            {
                result[i] = _buf[pos];
                pos = (pos + 1) % _capacity;
            }
            return result;
        }

        /// <summary>
        /// 특정 offset 의 바이트를 소비 없이 읽습니다. (PeekByte)
        /// </summary>
        private byte PeekByteCore(int offset)
        {
            if (offset >= _count)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"PeekByte: offset={offset} >= count={_count}");
            return _buf[(_readPos + offset) % _capacity];
        }

        /// <summary>
        /// 버퍼에서 지정한 바이트 수를 소비하여 버림 (Skip)
        /// </summary>
        /// <param name="n"></param>
        private void DiscardCore(int n)
        {
            n = Math.Min(n, _count);
            _readPos = (_readPos + n) % _capacity;
            _count -= n;
        }

        /// <summary>
        /// STX 기반 고정 길이 프레임을 탐색하여 추출합니다.
        /// </summary>
        private bool TryReadFrameCore(byte stx, int length, out byte[] frame)
        {
            frame = [];
            // STX 탐색
            int stxPos = -1;
            for (int i = 0; i < _count; i++)
                if (PeekByteCore(i) == stx) { stxPos = i; break; }

            if (stxPos < 0) { DiscardCore(_count); return false; } // STX 없음 → 전체 버림
            if (stxPos > 0) DiscardCore(stxPos);                  // STX 이전 쓰레기 제거
            if (_count < length) return false;                       // 데이터 부족

            frame = ReadCore(length);
            return true;
        }

        /// <summary>
        /// STX 기반 가변 길이 프레임을 탐색하여 추출
        /// </summary>
        private bool TryReadVariableFrameCore(byte stx, int lenOff, int lenSize,
            bool bigEndian, int overhead, out byte[] frame)
        {
            frame = [];
            // STX 탐색 + 쓰레기 제거
            int stxPos = -1;
            for (int i = 0; i < _count; i++)
                if (PeekByteCore(i) == stx) { stxPos = i; break; }
            if (stxPos < 0) { DiscardCore(_count); return false; }
            if (stxPos > 0) DiscardCore(stxPos);

            // Length 읽기 가능 여부
            int minForLen = lenOff + lenSize;
            if (_count < minForLen) return false;

            int dataLen;
            if (lenSize == 1)
                dataLen = PeekByteCore(lenOff);
            else if (lenSize == 2)
            {
                byte hi = PeekByteCore(lenOff);
                byte lo = PeekByteCore(lenOff + 1);
                dataLen = bigEndian ? (hi << 8) | lo : (lo << 8) | hi;
            }
            else return false;

            int totalLen = overhead + dataLen;
            if (totalLen <= 0 || totalLen > _capacity) return false;
            if (_count < totalLen) return false;

            frame = ReadCore(totalLen);
            return true;
        }

        /// <summary>
        /// STX 이전 데이터를 버립니다. 수신 동기화에 사용함
        /// </summary>
        private int SkipToCore(byte stx)
        {
            int skipped = 0;
            while (_count > 0 && PeekByteCore(0) != stx)
            { DiscardCore(1); skipped++; }
            return skipped;
        }
    }
}