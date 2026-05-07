// ====================================================================
//  lssLib.Binary — BufType.cs
//  버퍼 필드 타입 열거형 (Node-RED buffer-parser 호환)
//
//  [설계 원칙]
//  - Node-RED buffer-parser 노드와 완전 호환되는 타입 체계
//  - 모든 Endian 변종 (BE/LE) 명시적 구분
//  - .NET decimal (128-bit) 을 직접 지원하는 유일한 바이너리 타입 체계
//  - Size 파라미터로 배열 원소 수 지정 → 타입 확장 없이 배열 지원
//
//  [타입별 바이트 크기]
//  Int8/UInt8/Bool/Bit          1 바이트
//  Int16/UInt16 BE/LE           2 바이트 = Word
//  Int32/UInt32/Float BE/LE     4 바이트 = 2Word = 1DWord
//  Int64/UInt64/Double BE/LE    8 바이트 = 4Word = 2DWord = 1QWord
//  DecimalLE/BE                 16 바이트 (.NET decimal 고정) = 8Word = 4DWord = 2QWord
//  String/Raw/배열              Size 에 따라 가변
//  DecimalLEArray/BEArray       Size × 16 바이트
// ====================================================================
namespace lssLib.Binary
{
    /// <summary>
    /// 버퍼 필드 타입 열거형. <see cref="BufSchema"/> 에서 각 필드의 데이터 형식을 지정합니다.
    /// <para>Node-RED buffer-parser 노드와 동일한 타입 체계를 사용합니다.</para>
    ///
    /// <para><b>■ 정수 타입 (Integer)</b></para>
    /// <para>  Int8/UInt8 (1B), Int16/UInt16 BE·LE (2B), Int32/UInt32 BE·LE (4B), Int64/UInt64 BE·LE (8B)</para>
    ///
    /// <para><b>■ 실수 타입 (Floating Point, IEEE 754)</b></para>
    /// <para>  FloatBE/LE (4B, 단정밀도), DoubleBE/LE (8B, 배정밀도)</para>
    ///
    /// <para><b>■ 고정소수점 타입 (.NET decimal, 128-bit)</b></para>
    /// <para>  DecimalLE/BE (16B), DecimalLEArray/BEArray (Size × 16B)</para>
    /// <para>  정밀도 28~29자리. float/double 의 부동소수점 오차가 허용되지 않는 금융·회계·정밀 측정에 사용.</para>
    ///
    /// <para><b>■ 논리/비트 타입</b></para>
    /// <para>  Bool (1B, 0=false), Bit (1비트, Size=비트인덱스 0~7)</para>
    ///
    /// <para><b>■ 문자열 타입</b></para>
    /// <para>  StringAscii, StringUtf8, StringHex, StringBase64 (Size=바이트 수)</para>
    ///
    /// <para><b>■ 배열 타입</b></para>
    /// <para>  Int8Array ~ UInt32Array, FloatBEArray/LEArray, DoubleBEArray/LEArray (Size=원소 수)</para>
    ///
    /// <example><code>
    /// // ─── 기본 스키마 정의 예시 ───────────────────────────────────
    ///
    /// // Modbus FC03 응답 프레임 파싱
    /// // [SlaveId:1B][FC:1B][ByteCount:1B][Reg0:2B BE][Reg1:2B BE]...[CRC:2B LE]
    /// var modbusSchema = new BufSchema()
    ///     .Add("SlaveId",   BufType.UInt8,    offset:0)
    ///     .Add("FC",        BufType.UInt8,    offset:1)
    ///     .Add("ByteCount", BufType.UInt8,    offset:2)
    ///     .Add("Reg0",      BufType.UInt16BE, offset:3)
    ///     .Add("Reg1",      BufType.UInt16BE, offset:5)
    ///     .Add("Reg2",      BufType.UInt16BE, offset:7)
    ///     .Add("Reg3",      BufType.UInt16BE, offset:9);
    ///
    /// // 금융 거래 프레임 (decimal 사용)
    /// // [STX:1B][OrderId:4B LE][Price:16B LE][Qty:16B LE][Total:16B LE]
    /// var tradeSchema = new BufSchema()
    ///     .Then("STX",     BufType.UInt8)         // offset 0  (1B)
    ///     .Then("OrderId", BufType.UInt32LE)      // offset 1  (4B)
    ///     .Then("Price",   BufType.DecimalLE)     // offset 5  (16B) ← decimal!
    ///     .Then("Qty",     BufType.DecimalLE)     // offset 21 (16B)
    ///     .Then("Total",   BufType.DecimalLE);    // offset 37 (16B)
    ///
    /// // 다중 센서 배열 프레임
    /// // [STX:1B][Count:1B][Values:4개 floatBE][Status:1B]
    /// var sensorSchema = new BufSchema()
    ///     .Then("STX",    BufType.UInt8)
    ///     .Then("Count",  BufType.UInt8)
    ///     .Then("Values", BufType.FloatBEArray, size:4)  // 4개 × 4B = 16B
    ///     .Then("Status", BufType.UInt8);
    ///
    /// // 가격 목록 배열 (decimal 배열)
    /// // [Count:2B][Prices:N개 × 16B]
    /// var priceListSchema = new BufSchema()
    ///     .Then("Count",  BufType.UInt16BE)
    ///     .Then("Prices", BufType.DecimalLEArray, size:5); // 5개 × 16B = 80B
    ///
    /// // 파싱 및 값 접근
    /// var result = raw.ToParser().Parse(tradeSchema);
    /// decimal price = result.GetDecimal("Price");    // 편의 메서드
    /// decimal total = result.Get<decimal>("Total"); // 직접 접근
    /// float[] vals  = result.Get<float[]>("Values");
    /// </code></example>
    /// </summary>
    public enum BufType
    {
        // ════════════════════════════════════════════════════════════
        //  정수 타입
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 부호 있는 8비트 정수 (1바이트). 범위: -128 ~ 127.
        /// <para>센서 오프셋값, 온도 보정값, 부호 있는 작은 카운터에 사용.</para>
        /// <example><code>
        /// // 직접 읽기
        /// sbyte offset = bp.ReadInt8(0);   // 예: -12 (보정값)
        ///
        /// // 스키마 파싱
        /// var schema = new BufSchema().Add("TempOffset", BufType.Int8, offset:3);
        /// sbyte correction = result.Get<sbyte>("TempOffset");
        ///
        /// // 쓰기
        /// bw.WriteInt8(-12);
        /// </code></example>
        /// </summary>
        Int8,

        /// <summary>
        /// 부호 없는 8비트 정수 (1바이트). 범위: 0 ~ 255.
        /// <para>STX/ETX 프레임 구분자, 명령 코드, 상태 바이트, 1바이트 체크섬에 가장 많이 사용.</para>
        /// <example><code>
        /// // 직접 읽기
        /// byte stx = bp.ReadUInt8(0);    // 0xAA -> 170
        /// byte fc  = bp.ReadUInt8(1);    // 0x03 (Function Code)
        ///
        /// // 스키마 파싱 후 편의 메서드로 변환
        /// int stxInt = result.GetInt("STX");   // 170 (byte → int 자동 변환)
        ///
        /// // 쓰기
        /// bw.WriteUInt8(0xAA).WriteUInt8(0x03);
        /// </code></example>
        /// </summary>
        UInt8,

        /// <summary>
        /// 부호 있는 16비트 정수, Big-Endian (2바이트). 범위: -32768 ~ 32767.
        /// <para>네트워크 프로토콜(TCP/IP), Modbus, S7 PLC 기본 Endian.</para>
        /// <para>메모리 레이아웃: [상위바이트][하위바이트] — 예: 256 = [0x01][0x00]</para>
        /// <example><code>
        /// // Modbus 응답에서 부호 있는 레지스터 값 읽기
        /// short temp = bp.ReadInt16BE(3);   // 예: -500 → 0xFF0C (두 보수)
        ///
        /// // 스키마 파싱
        /// var schema = new BufSchema().Add("Temp", BufType.Int16BE, offset:3);
        /// short t = result.Get<short>;("Temp");
        ///
        /// // 스케일 변환: 레지스터값 × 0.1 = 실제 온도
        /// double tempC = result.GetInt("Temp") * 0.1;
        /// </code></example>
        /// </summary>
        Int16BE,

        /// <summary>
        /// 부호 있는 16비트 정수, Little-Endian (2바이트).
        /// <para>x86 시스템 기본 Endian. Windows API, CAN Bus 로컬 값.</para>
        /// <para>메모리 레이아웃: [하위바이트][상위바이트] — 예: 256 = [0x00][0x01]</para>
        /// <example><code>
        /// short v = bp.ReadInt16LE(2);   // x86 메모리에서 직접 읽기
        /// bw.WriteInt16LE(-500);
        /// </code></example>
        /// </summary>
        Int16LE,

        /// <summary>
        /// 부호 없는 16비트 정수, Big-Endian (2바이트). 범위: 0 ~ 65535.
        /// <para>Modbus 레지스터값(0~65535), 프레임 길이 필드, 포트 번호, ADC 12/16비트 값에 사용.</para>
        /// <example><code>
        /// // Modbus 응답 레지스터 읽기
        /// ushort reg0 = bp.ReadUInt16BE(3);   // 0x0064 = 100
        /// ushort reg1 = bp.ReadUInt16BE(5);   // 0x012C = 300
        ///
        /// // ADC 12비트 값 → 전압 변환
        /// ushort adc  = result.Get<ushort>("ADC");
        /// double volt = adc.MapTo(0, 4095, 0.0, 3.3);  // ≈ 1.65V (adc=2048)
        ///
        /// // 배열로 한번에 읽기
        /// ushort[] regs = bp.ReadUInt16BEArray(offset:3, count:4);
        /// </code></example>
        /// </summary>
        UInt16BE,

        /// <summary>
        /// 부호 없는 16비트 정수, Little-Endian (2바이트).
        /// <para>Windows 구조체, 바코드 스캐너 응답, RS-485 커스텀 장비에서 주로 사용.</para>
        /// <example><code>
        /// ushort len = bp.ReadUInt16LE(2);   // 프레임 길이 필드
        /// bw.WriteUInt16LE(256);             // 0x00 0x01 순서로 저장
        /// </code></example>
        /// </summary>
        UInt16LE,

        /// <summary>
        /// 부호 있는 32비트 정수, Big-Endian (4바이트).
        /// <para>Unix 타임스탬프, 시퀀스 번호, 큰 카운터값. 네트워크 프로토콜 표준.</para>
        /// <example><code>
        /// // Unix 타임스탬프 읽기
        /// int timestamp = bp.ReadInt32BE(4);
        /// var dt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        ///
        /// // 시퀀스 번호
        /// int seq = result.GetInt("Sequence");
        /// </code></example>
        /// </summary>
        Int32BE,

        /// <summary>부호 있는 32비트 정수, Little-Endian (4바이트). Windows/x86 기본 정수형.</summary>
        Int32LE,

        /// <summary>
        /// 부호 없는 32비트 정수, Big-Endian (4바이트).
        /// <para>CRC-32 체크섬, IP 주소, 큰 카운터. 범위: 0 ~ 4,294,967,295.</para>
        /// <example><code>
        /// uint crc = bp.ReadUInt32BE(frame.Length - 4);   // 프레임 끝 CRC
        /// uint ip  = bp.ReadUInt32BE(12);                  // IPv4 주소
        /// </code></example>
        /// </summary>
        UInt32BE,

        /// <summary>
        /// 부호 없는 32비트 정수, Little-Endian (4바이트).
        /// <para>Windows DWORD, CAN Bus ID, SocketCAN 수신 버퍼 ID 필드.</para>
        /// <example><code>
        /// // SocketCAN 프레임에서 CAN ID 읽기
        /// uint rawId = bp.ReadUInt32LE(0);
        /// bool isExt = (rawId & 0x80000000) != 0;
        /// uint canId = isExt ? rawId & 0x1FFFFFFF : rawId & 0x7FF;
        ///
        /// bw.WriteUInt32LE(0xDEADBEEF);
        /// </code></example>
        /// </summary>
        UInt32LE,

        /// <summary>부호 있는 64비트 정수, Big-Endian (8바이트). 밀리초 타임스탬프, 대용량 카운터.</summary>
        Int64BE,

        /// <summary>부호 있는 64비트 정수, Little-Endian (8바이트). C# <c>long</c> 기본 저장 형식.</summary>
        Int64LE,

        /// <summary>부호 없는 64비트 정수, Big-Endian (8바이트). 범위: 0 ~ 18,446,744,073,709,551,615.</summary>
        UInt64BE,

        /// <summary>부호 없는 64비트 정수, Little-Endian (8바이트).</summary>
        UInt64LE,

        // ════════════════════════════════════════════════════════════
        //  실수 타입 (IEEE 754)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// IEEE 754 단정밀도 실수, Big-Endian (4바이트).
        /// <para>정밀도: 약 7자리. Modbus RTU, Siemens S7, Allen-Bradley PLC의 기본 실수 타입.</para>
        /// <para>메모리 레이아웃: [부호(1b)][지수(8b)][가수(23b)] — Big-Endian 순서.</para>
        /// <para><b>주의</b>: 금융 계산에는 부적합. 오차 발생 가능. 금융에는 <see cref="DecimalLE"/> 사용.</para>
        /// <example><code>
        /// // Modbus FC03 응답에서 실수값 읽기
        /// float temp    = bp.ReadFloatBE(3);    // 예: 25.5°C
        /// float pressure= bp.ReadFloatBE(7);   // 예: 101.325 kPa
        ///
        /// // 4개 센서 배열 한번에 읽기
        /// float[] sensors = bp.ReadFloatBEArray(offset:3, count:4);
        /// // sensors[0]=온도, sensors[1]=습도, sensors[2]=압력, sensors[3]=유량
        ///
        /// // 스키마 파싱 후 편의 메서드
        /// float v = result.GetFloat("Temperature");
        ///
        /// // BufferWriter 로 쓰기
        /// bw.WriteFloatBE(25.5f);
        ///
        /// // IEEE 754 비트 확인 (디버깅)
        /// string hex = 25.5f.ToHex();   // "0x41CC0000"
        /// var info    = 25.5f.Analyze();
        /// </code></example>
        /// </summary>
        FloatBE,

        /// <summary>
        /// IEEE 754 단정밀도 실수, Little-Endian (4바이트).
        /// <para>CAN Bus PDO 데이터, x86 메모리 기본 float 저장 형식, 임베디드 MCU 기본.</para>
        /// <example><code>
        /// // CAN PDO 에서 float 2개 읽기
        /// float rpm  = bp.ReadFloatLE(0);   // RPM
        /// float torq = bp.ReadFloatLE(4);   // 토크 (Nm)
        ///
        /// // 배열
        /// float[] pdoData = bp.ReadFloatLEArray(0, 2);
        /// </code></example>
        /// </summary>
        FloatLE,

        /// <summary>
        /// IEEE 754 배정밀도 실수, Big-Endian (8바이트).
        /// <para>정밀도: 약 15~17자리. 정밀 측정 장비, GPS 좌표, 과학 계산에 사용.</para>
        /// <example><code>
        /// // GPS 위경도 읽기 (정밀도 필요)
        /// double lat = bp.ReadDoubleBE(0);   // 예: 37.566535 (서울 위도)
        /// double lon = bp.ReadDoubleBE(8);   // 예: 126.977969
        /// </code></example>
        /// </summary>
        DoubleBE,

        /// <summary>
        /// IEEE 754 배정밀도 실수, Little-Endian (8바이트).
        /// <para>C# <c>double</c> 기본 저장 형식. x86 FPU 레지스터 덤프.</para>
        /// </summary>
        DoubleLE,

        // ════════════════════════════════════════════════════════════
        //  고정소수점 타입 (.NET decimal, 128-bit)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// .NET <c>decimal</c>, Little-Endian (16바이트).
        /// <para><b>정밀도</b>: 28~29 유효자리. float(7자리)/double(15자리)보다 월등히 정밀.</para>
        /// <para><b>내부 구조</b>: <c>decimal.GetBits()</c> → <c>int[4] = [lo, mid, hi, flags]</c></para>
        /// <para>  - lo (int, 4B): 128비트 정수의 하위 32비트</para>
        /// <para>  - mid (int, 4B): 중간 32비트</para>
        /// <para>  - hi (int, 4B): 상위 32비트</para>
        /// <para>  - flags (int, 4B): bit31=부호(1=음수), bits16-23=소수점 자릿수(0~28)</para>
        /// <para>각 int를 Little-Endian 으로 직렬화하여 총 16바이트.</para>
        /// <para><b>사용 시기</b>: 금융 가격, 회계 금액, 환율, 세금, 부동소수점 오차가 허용되지 않는 모든 계산.</para>
        /// <example><code>
        /// // ─── 직렬화 / 역직렬화 ────────────────────────────────────
        ///
        /// // decimal → 16바이트 LE
        /// byte[] raw = 123.456m.ToBytes();
        /// // 40 E2 01 00  00 00 00 00  00 00 00 00  00 00 03 00
        /// // [lo=0x0001E240][mid=0][hi=0][flags=0x00030000, scale=3]
        ///
        /// // 16바이트 → decimal 복원
        /// decimal v = raw.ReadDecimalLE();  // 123.456m (손실 없음)
        ///
        /// // BufferParser 직접 읽기
        /// decimal price = bp.ReadDecimalLE(offset:0);   // 16바이트 소비
        ///
        /// // ─── 스키마 파싱 ──────────────────────────────────────────
        ///
        /// var schema = new BufSchema()
        ///     .Then("STX",     BufType.UInt8)         // 1B  → next:1
        ///     .Then("OrderId", BufType.UInt32LE)      // 4B  → next:5
        ///     .Then("Price",   BufType.DecimalLE)     // 16B → next:21
        ///     .Then("Qty",     BufType.DecimalLE)     // 16B → next:37
        ///     .Then("Total",   BufType.DecimalLE);    // 16B → next:53
        ///
        /// var result = raw.ToParser().Parse(schema);
        ///
        /// // 편의 메서드로 접근 (타입 자동 변환)
        /// decimal price = result.GetDecimal("Price");
        /// decimal qty   = result.GetDecimal("Qty");
        /// decimal total = price * qty;   // 정확한 정밀도!
        ///
        /// // ─── float vs decimal 정밀도 비교 ────────────────────────
        ///
        /// decimal unitPrice = 1234567890.123456789m;
        /// decimal qty2      = 100.000m;
        /// decimal exactTotal = unitPrice * qty2;   // 정확: 123456789012.3456789
        ///
        /// float fPrice = (float)unitPrice;
        /// float fTotal = fPrice * 100f;            // 부정확: 부동소수점 오차 발생!
        ///
        /// // ─── 내부 구조 분해 ──────────────────────────────────────
        ///
        /// var info = 123.456m.Decompose();
        /// Console.WriteLine(info);
        /// // decimal 123.456  Sign=양수  Scale=3
        /// //   GetBits = [0x0001E240, 0x00000000, 0x00000000, 0x00030000]
        /// //   Bytes(LE) = 40 E2 01 00 00 00 00 00 00 00 00 00 00 00 03 00
        /// </code></example>
        /// </summary>
        DecimalLE,

        /// <summary>
        /// .NET <c>decimal</c>, Big-Endian (16바이트).
        /// <para>내부 구조: <c>GetBits [lo,mid,hi,flags]</c> 각 int를 Big-Endian 으로 직렬화.</para>
        /// <para>일부 네트워크 프로토콜이 BE decimal을 요구할 때 사용.</para>
        /// <example><code>
        /// byte[] rawBE = 123.456m.ToBigEndianBytes();  // BE 직렬화
        /// decimal v    = rawBE.ReadDecimalBE();         // 복원
        /// decimal vBp  = bp.ReadDecimalBE(offset:0);   // 파서로 읽기
        /// </code></example>
        /// </summary>
        DecimalBE,

        // ════════════════════════════════════════════════════════════
        //  논리 / 비트 타입
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 1바이트를 bool 로 해석합니다. 0 = false, 그 외 = true.
        /// <para>장비 상태 플래그, 활성화 여부, 이진 출력 값에 사용.</para>
        /// <example><code>
        /// // 직접 읽기
        /// bool isActive  = bp.ReadBool(offset:3);  // 0x01 → true, 0x00 → false
        /// bool isEnabled = bp.ReadBool(offset:4);
        ///
        /// // 스키마 파싱
        /// var schema = new BufSchema()
        ///     .Add("IsRunning", BufType.Bool, offset:2)
        ///     .Add("HasError",  BufType.Bool, offset:3);
        ///
        /// bool running = result.Get<bool>("IsRunning");
        ///
        /// // 쓰기
        /// bw.WriteBool(true);   // 0x01
        /// bw.WriteBool(false);  // 0x00
        /// </code></example>
        /// </summary>
        Bool,

        /// <summary>
        /// 특정 비트 1개를 읽습니다. <c>Size</c> 파라미터 = 비트 인덱스 (0=LSB, 7=MSB).
        /// <para>장비 상태 레지스터의 개별 비트 플래그를 파싱할 때 사용.</para>
        /// <example><code>
        /// // 상태 레지스터 바이트: 0b10110101 (0xB5)
        /// //   bit7(MSB)=1: 오류 발생
        /// //   bit6=0
        /// //   bit5=1: 고온 경보
        /// //   bit4=1
        /// //   bit3=0
        /// //   bit2=1: 실행 중
        /// //   bit1=0
        /// //   bit0(LSB)=1: 전원 ON
        ///
        /// // 직접 읽기
        /// bool isError   = bp.ReadBit(offset:2, bit:7);  // true  (MSB)
        /// bool isRunning = bp.ReadBit(offset:2, bit:2);  // true
        /// bool isPowerOn = bp.ReadBit(offset:2, bit:0);  // true  (LSB)
        /// bool bit6      = bp.ReadBit(offset:2, bit:6);  // false
        ///
        /// // 스키마 파싱 (Size = 비트 인덱스)
        /// var schema = new BufSchema()
        ///     .Add("ErrorBit",   BufType.Bit, offset:2, size:7)  // bit7
        ///     .Add("RunningBit", BufType.Bit, offset:2, size:2)  // bit2
        ///     .Add("PowerBit",   BufType.Bit, offset:2, size:0); // bit0
        ///
        /// bool err = result.Get<bool>("ErrorBit");
        ///
        /// // 비트 마스크로 여러 비트 한번에 추출
        /// byte statusBits = bp.ReadBitField(offset:2, mask:0b00111000); // bit3~5
        /// </code></example>
        /// </summary>
        Bit,

        // ════════════════════════════════════════════════════════════
        //  문자열 타입
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// ASCII 문자열. <c>Size</c> = 바이트 수. 널 종료문자(\0) 자동 제거.
        /// <para>장비 모델명, 시리얼 번호, 바코드 데이터, 단순 영문 텍스트에 사용.</para>
        /// <example><code>
        /// // 직접 읽기
        /// string model  = bp.ReadStringAscii(offset:8,  length:16);  // "SHT3x-A"
        /// string serial = bp.ReadStringAscii(offset:24, length:12);  // "SN20240101"
        ///
        /// // 스키마 파싱
        /// var schema = new BufSchema()
        ///     .Add("Model",  BufType.StringAscii, offset:8,  size:16)
        ///     .Add("Serial", BufType.StringAscii, offset:24, size:12);
        ///
        /// string model2 = result.GetString("Model");
        ///
        /// // 쓰기 (fixedLen 지정 시 빈 공간은 0x00으로 채움)
        /// bw.WriteStringAscii("SHT3x-A", fixedLen:16);
        /// </code></example>
        /// </summary>
        StringAscii,

        /// <summary>
        /// UTF-8 문자열. <c>Size</c> = 바이트 수. 한글·중국어·일본어 등 멀티바이트 지원.
        /// <para>국제화 장비 데이터, IoT 디바이스 텍스트 필드에 사용.</para>
        /// <example><code>
        /// // 한글 이름 읽기
        /// string name = bp.ReadStringUtf8(offset:4, length:32);  // "온도센서A"
        ///
        /// // 스키마
        /// var schema = new BufSchema()
        ///     .Add("DevName", BufType.StringUtf8, offset:4, size:32);
        ///
        /// bw.WriteStringUtf8("온도센서A", fixedLen:32);
        /// </code></example>
        /// </summary>
        StringUtf8,

        /// <summary>
        /// 바이트를 HEX 문자열로 반환. <c>Size</c> = 바이트 수. 예: [0xAA,0xBB,0xCC] → "AABBCC"
        /// <para>MAC 주소, UUID, 바이너리 데이터의 문자열 표현에 사용.</para>
        /// <example><code>
        /// // MAC 주소 읽기
        /// string mac = bp.ReadStringHex(offset:0, length:6);   // "001122334455"
        ///
        /// // 스키마
        /// var schema = new BufSchema().Add("MAC", BufType.StringHex, offset:0, size:6);
        /// string mac2 = result.GetString("MAC");
        /// </code></example>
        /// </summary>
        StringHex,

        /// <summary>
        /// 바이트를 Base64 문자열로 반환. <c>Size</c> = 바이트 수.
        /// <para>인증 토큰, 암호화 데이터의 텍스트 전송에 사용.</para>
        /// <example><code>
        /// string b64  = bp.ReadStringBase64(offset:0, length:12);
        /// byte[] back = Convert.FromBase64String(b64);  // 원본 복원
        /// </code></example>
        /// </summary>
        StringBase64,

        // ════════════════════════════════════════════════════════════
        //  원시 타입
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 원시 byte[] 반환. <c>Size</c> = 바이트 수.
        /// <para>페이로드 데이터, 암호화된 구간, 이미지 데이터, 서브 프레임에 사용.</para>
        /// <example><code>
        /// // 페이로드 추출 후 별도 파싱
        /// byte[] payload = bp.ReadRaw(offset:4, length:32);
        /// var subResult  = payload.ToParser().Parse(subSchema);
        ///
        /// // 스키마
        /// var schema = new BufSchema()
        ///     .Add("Header",  BufType.UInt32BE,  offset:0)
        ///     .Add("Payload", BufType.Raw,       offset:4, size:32)
        ///     .Add("CRC",     BufType.UInt32BE,  offset:36);
        ///
        /// byte[] pay = result.GetRaw("Payload");
        ///
        /// // HexDump 출력
        /// Console.WriteLine(result.ToDump("Payload"));
        /// // Payload (32 bytes):
        /// //   00000000: AA 01 00 0C 41 20 00 00  48 65 6C 6C 6F 00 00 00  ....A ..Hello...
        /// </code></example>
        /// </summary>
        Raw,

        // ════════════════════════════════════════════════════════════
        //  배열 타입 (Size = 원소 수)
        // ════════════════════════════════════════════════════════════

        /// <summary>sbyte 배열. <c>Size</c> = 원소 수 (총 Size × 1바이트).</summary>
        Int8Array,

        /// <summary>byte 배열. <c>Size</c> = 원소 수 (총 Size × 1바이트).</summary>
        UInt8Array,

        /// <summary>short 배열, Big-Endian. <c>Size</c> = 원소 수 (총 Size × 2바이트).</summary>
        Int16BEArray,

        /// <summary>short 배열, Little-Endian.</summary>
        Int16LEArray,

        /// <summary>
        /// ushort 배열, Big-Endian. <c>Size</c> = 원소 수 (총 Size × 2바이트).
        /// <para>Modbus 다중 레지스터 읽기 응답에서 가장 많이 사용되는 배열 타입.</para>
        /// <example><code>
        /// // Modbus FC03: 4개 레지스터 한번에 파싱
        /// var schema = new BufSchema()
        ///     .Add("SlaveId",   BufType.UInt8,        offset:0)
        ///     .Add("FC",        BufType.UInt8,        offset:1)
        ///     .Add("ByteCount", BufType.UInt8,        offset:2)
        ///     .Add("Registers", BufType.UInt16BEArray,offset:3, size:4);  // 8바이트
        ///
        /// ushort[] regs = result.Get<ushort[]>("Registers");
        /// // regs[0]=온도ADC, regs[1]=습도ADC, regs[2]=전압ADC, regs[3]=상태
        ///
        /// double temp = regs[0].MapTo(0, 4095, -40.0, 125.0);
        /// </code></example>
        /// </summary>
        UInt16BEArray,

        /// <summary>ushort 배열, Little-Endian.</summary>
        UInt16LEArray,

        /// <summary>int 배열, Big-Endian. <c>Size</c> = 원소 수 (총 Size × 4바이트).</summary>
        Int32BEArray,

        /// <summary>int 배열, Little-Endian.</summary>
        Int32LEArray,

        /// <summary>uint 배열, Big-Endian.</summary>
        UInt32BEArray,

        /// <summary>uint 배열, Little-Endian.</summary>
        UInt32LEArray,

        /// <summary>
        /// float 배열, Big-Endian. <c>Size</c> = 원소 수 (총 Size × 4바이트).
        /// <para>Modbus REAL 다중 레지스터, S7 DB 배열, 다채널 아날로그 출력 장비에서 자주 사용.</para>
        /// <example><code>
        /// // Siemens S7 DB에서 float 4개 읽기
        /// var schema = new BufSchema()
        ///     .Add("Temperatures", BufType.FloatBEArray, offset:0, size:4);
        ///
        /// float[] temps = result.Get<float[]>("Temperatures");
        /// // temps[0]=Zone1, temps[1]=Zone2, temps[2]=Zone3, temps[3]=Zone4
        ///
        /// // 직접 읽기
        /// float[] sensors = bp.ReadFloatBEArray(offset:3, count:4);
        ///
        /// // Scale 변환 후 출력
        /// for (int i = 0; i < sensors.Length; i++)
        ///     Console.WriteLine($"Zone{i+1}: {sensors[i]:F2}°C");
        /// </code></example>
        /// </summary>
        FloatBEArray,

        /// <summary>
        /// float 배열, Little-Endian. <c>Size</c> = 원소 수.
        /// <para>CAN Bus PDO 다채널 데이터, x86 임베디드 MCU 배열.</para>
        /// <example><code>
        /// // CAN PDO 데이터 8바이트 = float 2개
        /// float[] pdo = bp.ReadFloatLEArray(offset:0, count:2);
        /// float rpm   = pdo[0];   // RPM
        /// float torq  = pdo[1];   // 토크(Nm)
        /// </code></example>
        /// </summary>
        FloatLEArray,

        /// <summary>double 배열, Big-Endian. <c>Size</c> = 원소 수 (총 Size × 8바이트).</summary>
        DoubleBEArray,

        /// <summary>double 배열, Little-Endian.</summary>
        DoubleLEArray,

        /// <summary>
        /// decimal 배열, Little-Endian. <c>Size</c> = 원소 수 (총 <c>Size × 16</c> 바이트).
        /// <para>금융 가격 목록, 회계 원장 데이터, 다중 정밀 측정값 배열에 사용.</para>
        /// <para>각 원소가 16바이트이므로 5개 배열 = 80바이트.</para>
        /// <example><code>
        /// // 거래 가격 5개를 한번에 파싱 (총 80바이트)
        /// var schema = new BufSchema()
        ///     .Then("OrderId", BufType.UInt32LE)
        ///     .Then("Prices",  BufType.DecimalLEArray, size:5);  // 80바이트
        ///
        /// var result = raw.ToParser().Parse(schema);
        ///
        /// // 편의 메서드로 접근
        /// decimal[] prices = result.Get<decimal[]>("Prices");
        /// // prices[0]=1234.56m, prices[1]=789.00m, ...
        ///
        /// decimal total = prices.Sum();  // 정확한 합계
        ///
        /// // 직접 읽기
        /// decimal[] arr = bp.ReadDecimalLEArray(offset:4, count:5);
        ///
        /// // 직렬화 (BufferWriter)
        /// decimal[] data = { 1234.56m, 789.00m, 100.50m };
        /// byte[] raw2 = BufferWriter.Create()
        ///     .WriteUInt8(0xAA)
        ///     .WriteDecimalLEArray(data)   // 48바이트 추가
        ///     .ToArray();                  // 총 49바이트
        /// </code></example>
        /// </summary>
        DecimalLEArray,

        /// <summary>
        /// decimal 배열, Big-Endian. <c>Size</c> = 원소 수 (총 <c>Size × 16</c> 바이트).
        /// <example><code>
        /// // BE decimal 배열 직렬화/역직렬화
        /// decimal[] prices = { 1234.56m, 789.00m };
        /// byte[]    raw    = prices.ToBEBytes();           // 32바이트 BE
        /// decimal[] back   = raw.ToDecimalBEArray(0, 2);   // 복원
        ///
        /// // 스키마 파싱
        /// var schema = new BufSchema()
        ///     .Add("Prices", BufType.DecimalBEArray, offset:0, size:2);
        /// </code></example>
        /// </summary>
        DecimalBEArray,
    }
}