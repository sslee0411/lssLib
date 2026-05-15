// ====================================================================
//  lssLib.Binary — StreamParser
//  TCP / Serial 수신 버퍼에서 STX 기반 프레임을 탐색합니다.
//
//  [고정 길이 프레임]
//  var sp = new StreamParser(rxBuf);
//  while (sp.FindNext(stx:0xAA, frameLen:32, out int offset))
//  {
//      var result = sp.Slice(offset, 32).ToParser().Parse(schema);
//      sp.Advance(offset + 32);
//  }
//
//  [가변 길이 프레임]
//  // 구조: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
//  while (sp.Remaining > 8)
//  {
//      if (!sp.FindNext(0xAA, 8, out int pos)) break;
//      int dataLen = sp.ReadUInt16BE(pos + 2);
//      int total   = 1 + 1 + 2 + dataLen + 4;
//      if (!sp.HasBytes(pos, total)) break;
//      byte[] frame = sp.Slice(pos, total);
//      if (frame.VerifyCrc32())
//          Process(frame[..^4].ToParser().Parse(schema));
//      sp.Advance(pos + total);
//  }
//
//  [다중 STX 패턴]
//  if (sp.FindNext(new byte[]{0xAA,0xBB}, 16, out int pos, out byte found))
//  {
//      var schema = found == 0xAA ? schemaA : schemaB;
//      var result = sp.Slice(pos, 16).ToParser().Parse(schema);
//  }
// ====================================================================

using System.Buffers.Binary;

namespace lssLib.Binary
{
    /// <summary>
    /// TCP / Serial 수신 버퍼에서 STX 기반 프레임을 탐색합니다.
    /// <para>이미 수신된 byte[] 에서 프레임을 찾는 탐색기입니다.</para>
    /// <para>지속 수신 + 프레임 추출이 필요하면 <see cref="RingBuffer"/> 를 사용하세요.</para>
    /// <example><code>
    /// byte[] rxBuf = GetFromTcpSocket();
    /// var sp = new StreamParser(rxBuf);
    ///
    /// // 고정 32바이트 프레임 (STX=0xAA)
    /// while (sp.FindNext(stx:0xAA, frameLen:32, out int offset))
    /// {
    ///     byte[] frame  = sp.Slice(offset, 32);
    ///     var    result = frame.ToParser().Parse(schema);
    ///     sp.Advance(offset + 32); 
    ///     // Advance() : '현재 읽기 위치(포인터)'를 지정한 만큼 뒤로 이동시켜, 이미 처리한 데이터를 건너뛰는 함수
    /// }
    ///
    /// // 남은 바이트 확인
    /// Console.WriteLine($"위치={sp.Position} 남음={sp.Remaining}");
    /// 
    /// </code></example>
    /// </summary>
    public sealed class StreamParser
    {
        private readonly byte[] _data;
        private int _pos;

        /// <summary>현재 스캔 위치 (절대 offset).</summary>
        public int Position => _pos;

        /// <summary>전체 버퍼 길이.</summary>
        public int Length => _data.Length;

        /// <summary>현재 위치에서 남은 바이트 수.</summary>
        public int Remaining => Math.Max(0, _data.Length - _pos);

        /// <summary>
        /// 수신 버퍼로 StreamParser 를 생성합니다.
        /// <example><code>
        /// var sp = new StreamParser(rxBuf);
        /// var sp = new StreamParser(rxBuf, startPos:10);  // 10바이트 이후부터 탐색
        /// </code></example>
        /// </summary>
        public StreamParser(byte[] data, int startPos = 0)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _pos = Math.Clamp(startPos, 0, data.Length);
        }

        // ── 프레임 탐색 ──────────────────────────────────────────────

        /// <summary>
        /// 현재 위치에서 STX 바이트를 탐색하고 프레임 시작 위치를 반환합니다.
        /// STX 이전의 쓰레기 데이터는 무시합니다.
        /// <example><code>
        /// // 고정 32바이트 프레임
        /// while (sp.FindNext(stx:0xAA, frameLen:32, out int offset))
        /// {
        ///     byte[] frame  = sp.Slice(offset, 32);
        ///     var    result = frame.ToParser().Parse(schema);
        ///     sp.Advance(offset + 32);
        /// }
        /// </code></example>
        /// </summary>
        /// <param name="stx">프레임 시작 바이트.</param>
        /// <param name="frameLen">프레임 전체 길이 (STX 포함). 이 길이만큼 남아있을 때만 성공.</param>
        /// <param name="offset">발견된 프레임의 절대 시작 위치.</param>
        public bool FindNext(byte stx, int frameLen, out int offset)
        {
            offset = -1;
            for (int i = _pos; i <= _data.Length - frameLen; i++)
            {
                if (_data[i] == stx)
                {
                    offset = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 여러 STX 패턴 중 하나를 탐색합니다. 프레임 타입을 구분할 때 사용합니다.
        /// <example><code>
        /// // 0xAA 또는 0xBB 로 시작하는 프레임 탐색
        /// while (sp.FindNext(
        ///     stxBytes: new byte[]{0xAA, 0xBB},
        ///     frameLen: 16,
        ///     out int pos, out byte foundStx))
        /// {
        ///     var schema = foundStx == 0xAA ? schemaA : schemaB;
        ///     var result = sp.Slice(pos, 16).ToParser().Parse(schema);
        ///     sp.Advance(pos + 16);
        /// }
        /// </code></example>
        /// </summary>
        /// <param name="stxBytes">탐색할 STX 바이트 목록.</param>
        /// <param name="frameLen">프레임 전체 길이.</param>
        /// <param name="offset">발견된 시작 위치.</param>
        /// <param name="foundStx">실제 발견된 STX 값.</param>
        public bool FindNext(byte[] stxBytes, int frameLen,
            out int offset, out byte foundStx)
        {
            offset = -1;
            foundStx = 0;
            for (int i = _pos; i <= _data.Length - frameLen; i++)
            {
                if (stxBytes.Contains(_data[i]))
                {
                    offset = i;
                    foundStx = _data[i];
                    return true;
                }
            }
            return false;
        }

        // ── 슬라이스 / 이동 ──────────────────────────────────────────

        /// <summary>
        /// 지정 위치에서 length 바이트를 복사하여 반환합니다.
        /// <example><code>
        /// byte[] frame = sp.Slice(offset, 32);
        /// var result   = frame.ToParser().Parse(schema);
        /// </code></example>
        /// </summary>
        public byte[] Slice(int offset, int length)
        {
            if (offset < 0 || offset + length > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"Slice({offset},{length}) 범위 초과. bufLen={_data.Length}");
            var result = new byte[length];
            Array.Copy(_data, offset, result, 0, length);
            return result;
        }

        /// <summary>
        /// 스캔 위치를 newPos 로 이동합니다. 처리된 프레임 이후로 이동할 때 사용합니다.
        /// <example><code>
        /// sp.Advance(offset + 32);  // 32바이트 프레임 처리 후 이동
        /// </code></example>
        /// </summary>
        public void Advance(int newPos) => _pos = Math.Clamp(newPos, 0, _data.Length);

        /// <summary>현재 위치에서 delta 만큼 앞으로 이동합니다.</summary>
        public void Skip(int delta) => _pos = Math.Clamp(_pos + delta, 0, _data.Length);

        // ── 검사 / 읽기 ───────────────────────────────────────────────

        /// <summary>
        /// 지정 위치에서 length 바이트가 존재하는지 확인합니다.
        /// 가변 길이 프레임에서 전체 수신 여부를 확인할 때 사용합니다.
        /// <example><code>
        /// int total = 1 + 1 + 2 + dataLen + 4;
        /// if (!sp.HasBytes(pos, total)) break;  // 아직 데이터 부족
        /// byte[] frame = sp.Slice(pos, total);
        /// </code></example>
        /// </summary>
        public bool HasBytes(int offset, int length)
            => offset >= 0 && offset + length <= _data.Length;

        /// <summary>
        /// 지정 위치에서 ushort (Big-Endian) 를 읽습니다.
        /// 가변 길이 프레임의 Length 필드를 미리 확인할 때 사용합니다.
        /// <example><code>
        /// int dataLen = sp.ReadUInt16BE(pos + 2);  // Length 필드 읽기
        /// </code></example>
        /// </summary>
        public ushort ReadUInt16BE(int offset)
        {
            if (offset + 2 > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));
        }

        /// <summary>
        /// 지정 위치의 byte 를 읽습니다. STX 확인에 사용합니다.
        /// <example><code>
        /// byte stx = sp.ReadByte(pos);
        /// </code></example>
        /// </summary>
        public byte ReadByte(int offset)
        {
            if (offset >= _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return _data[offset];
        }

        // ── 유틸 ──────────────────────────────────────────────────────

        /// <summary>전체 버퍼를 HEX 문자열로 반환합니다.</summary>
        public string ToHex(string sep = " ")
            => string.Join(sep, _data.Select(b => b.ToString("X2")));

        /// <summary>"StreamParser[pos/len bytes]" 형식 문자열.</summary>
        public override string ToString() => $"StreamParser[{_pos}/{_data.Length} bytes]";
    }
}