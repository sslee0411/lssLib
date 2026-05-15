using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LSS.Core.Text;
using lssLib.Binary;
using lssLib.Extensions;


// ════════════════════════════════════════════════════════════════
//  CompositeView — 종합 시나리오 16개
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// CompositeView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CompositeView : UserControl
    {
        public CompositeView() => InitializeComponent();

        private static readonly (string Title, string Desc)[] _info =
        [
            ("① ADC 센서 프레임 파싱 + 스케일링",    "BufferParser로 프레임 파싱 → ScaleExtensions로 물리량 변환 → CRC-8 검증"),
            ("② 금융 데이터 프레임 (decimal)",       "decimal 16바이트 직렬화. float 오차 없이 정확한 가격/수량/합계 계산"),
            ("③ XOR 암호화 프레임 투명 파싱",         "WithXorDecrypt로 암호화된 수신 데이터를 원본 불변 상태로 복호화 후 파싱"),
            ("④ 다중 프레임 슬라이딩 윈도우",          "WithOffset으로 수신 버퍼 내 다중 프레임 순차 파싱"),
            ("⑤ CRC 검증 후 조건부 파싱",             "VerifyCrc32 통과 시에만 파싱. 통신 오류 검출 표준 패턴"),
            ("⑥ JSON 설정 → 스키마 동적 생성",        "JSON 파일로 BufSchema 런타임 생성. 장비 설정 파일 기반 파싱"),
            ("⑦ 파싱 → Scale → CSV 파이프라인",      "수신 프레임 파싱 → 물리량 변환 → CSV 직렬화 전체 데이터 흐름"),
            ("⑧ struct ↔ byte[] 왕복 직렬화",       "StructLayout 구조체를 ToBytes/To<T>로 직렬화·역직렬화 + Dump"),
            ("⑨ 요청 프레임 생성 + 응답 파싱",        "BufferWriter로 명령 프레임 생성 → 응답 데이터를 BufferParser로 파싱"),
            ("⑩ Writer → Parser 왕복 검증",          "BufferWriter.ToParser()로 쓴 내용을 즉시 검증. decimal 손실 없음 확인"),
            ("⑪ RingBuffer — 수신 스트림 처리",       "연속 수신 버퍼에서 STX 기반 프레임 자동 추출. 쓰레기 데이터 자동 제거"),
            ("⑫ RingBuffer — 가변 길이 프레임",       "Length 필드 기반 가변 프레임 추출. CRC32 검증 포함"),
            ("⑬ BufferDiff — 프레임 변경 감지",       "Compare로 두 프레임의 차이 바이트 위치와 값을 정확하게 파악"),
            ("⑭ MaskedEquals — CRC/타임스탬프 무시", "매번 다른 CRC/타임스탬프를 제외하고 페이로드만 비교"),
            ("⑮ CompareFields — 필드 단위 비교",     "스키마 기반으로 어떤 필드가 변경됐는지 이름으로 확인"),
            ("⑯ 펌웨어 패치 생성 + 적용",             "두 펌웨어 이미지 비교 → 변경 패치 생성 → ApplyPatches로 재현"),
        ];

        // 버튼 → 설명 + 시나리오 실행
        private void OnAdcSensor(object s, RoutedEventArgs e) { UpdateDesc(0); ViewHelper.Run(Out, ScenAdcSensor); }
        private void OnFinancial(object s, RoutedEventArgs e) { UpdateDesc(1); ViewHelper.Run(Out, ScenFinancial); }
        private void OnEncrypted(object s, RoutedEventArgs e) { UpdateDesc(2); ViewHelper.Run(Out, ScenEncrypted); }
        private void OnSliding(object s, RoutedEventArgs e) { UpdateDesc(3); ViewHelper.Run(Out, ScenSliding); }
        private void OnCrcVerify(object s, RoutedEventArgs e) { UpdateDesc(4); ViewHelper.Run(Out, ScenCrcVerify); }
        private void OnDynSchema(object s, RoutedEventArgs e) { UpdateDesc(5); ViewHelper.Run(Out, ScenDynSchema); }
        private void OnPipeline(object s, RoutedEventArgs e) { UpdateDesc(6); ViewHelper.Run(Out, ScenPipeline); }
        private void OnStructRound(object s, RoutedEventArgs e) { UpdateDesc(7); ViewHelper.Run(Out, ScenStructRound); }
        private void OnWriterRequest(object s, RoutedEventArgs e) { UpdateDesc(8); ViewHelper.Run(Out, ScenWriterRequest); }
        private void OnWriterRoundtrip(object s, RoutedEventArgs e) { UpdateDesc(9); ViewHelper.Run(Out, ScenWriterRoundtrip); }
        private void OnRingBuffer(object s, RoutedEventArgs e) { UpdateDesc(10); ViewHelper.Run(Out, ScenRingBuffer); }
        private void OnRingVariable(object s, RoutedEventArgs e) { UpdateDesc(11); ViewHelper.Run(Out, ScenRingVariable); }
        private void OnDiffBasic(object s, RoutedEventArgs e) { UpdateDesc(12); ViewHelper.Run(Out, ScenDiffBasic); }
        private void OnDiffMasked(object s, RoutedEventArgs e) { UpdateDesc(13); ViewHelper.Run(Out, ScenDiffMasked); }
        private void OnDiffFields(object s, RoutedEventArgs e) { UpdateDesc(14); ViewHelper.Run(Out, ScenDiffFields); }
        private void OnDiffPatch(object s, RoutedEventArgs e) { UpdateDesc(15); ViewHelper.Run(Out, ScenDiffPatch); }
        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);

        private void UpdateDesc(int idx)
        { ScenTitle.Text = _info[idx].Title; ScenDesc.Text = _info[idx].Desc; }

        // ════════════════════════════════════════════════
        //  시나리오 ① ~ ⑧  (기존)
        // ════════════════════════════════════════════════

        static string ScenAdcSensor()
        {
            ushort tempAdc = 2500, humiAdc = 3200, voltAdc = 2048;
            byte[] frame = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt8(0x01)
                .WriteUInt16BE(tempAdc).WriteUInt16BE(humiAdc).WriteUInt16BE(voltAdc)
                .WriteUInt8(0x01).WritePad(1)
                .ToBytes();
            frame[^1] = frame.Crc8(0, frame.Length - 1);
            var schema = new BufSchema()
                .Then("STX", BufType.UInt8).Then("DevID", BufType.UInt8)
                .Then("TempADC", BufType.UInt16BE).Then("HumiADC", BufType.UInt16BE)
                .Then("VoltADC", BufType.UInt16BE).Then("Status", BufType.UInt8).Then("CRC8", BufType.UInt8);
            var result = frame.ToParser().Parse(schema);
            double tempC = result.GetInt("TempADC").MapTo(0, 4095, -40.0, 125.0);
            double humiP = result.GetInt("HumiADC").MapTo(0, 4095, 0.0, 100.0);
            double voltV = result.GetInt("VoltADC").MapTo(0, 4095, 0.0, 3.3);
            byte cs = result.Get<byte>("CRC8"); byte calc = frame.Crc8(0, frame.Length - 1);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("프레임 파싱"));
            sb.AppendLine($"  프레임: {frame.ToHexString()}  ({frame.Length}B)");
            sb.Append(result.ToString()); sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("Scale 변환 결과"));
            sb.AppendLine($"  온도: ADC {tempAdc} → {tempC:F2}°C");
            sb.AppendLine($"  습도: ADC {humiAdc} → {humiP:F2}%RH");
            sb.AppendLine($"  전압: ADC {voltAdc} → {voltV:F4}V");
            sb.AppendLine(ViewHelper.Line("CRC-8 검증"));
            sb.AppendLine($"  저장=0x{cs:X2}  계산=0x{calc:X2}  {(cs == calc ? "✅" : "❌")}");
            return sb.ToString();
        }

        static string ScenFinancial()
        {
            decimal unit = 1234.567890123456789m, qty = 100.000m, disc = 0.05m;
            decimal total = unit * qty * (1 - disc);
            byte[] raw = BufferWriter.Create()
                .WriteDecimalLE(unit).WriteDecimalLE(qty).WriteDecimalLE(total).ToBytes();
            var schema = new BufSchema()
                .Add("UnitPrice", BufType.DecimalLE, 0)
                .Add("Quantity", BufType.DecimalLE, 16)
                .Add("Total", BufType.DecimalLE, 32);
            var result = raw.ToParser().Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("decimal 금융 프레임"));
            sb.AppendLine($"  단가 : {unit:G}m  수량: {qty}m  할인: {disc}");
            sb.AppendLine($"  합계 : {total:G}m");
            sb.AppendLine($"  직렬화({raw.Length}B): {raw.ToHexString()}");
            sb.AppendLine(); sb.Append(result.ToString()); sb.AppendLine();
            sb.AppendLine($"  [정밀도] float = {(float)unit * (float)qty * (1f - 0.05f):G9}");
            sb.AppendLine($"  [정밀도] decimal = {total:G}");
            return sb.ToString();
        }

        static string ScenEncrypted()
        {
            const byte KEY = 0xA5;
            byte[] plain = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt8(0x03).WriteUInt16BE(0x0010).WriteFloatBE(3.14f)
                .ToBytes();
            byte[] enc = plain.Select(b => (byte)(b ^ KEY)).ToArray();
            var schema = new BufSchema()
                .Then("STX", BufType.UInt8).Then("FC", BufType.UInt8)
                .Then("Len", BufType.UInt16BE).Then("Value", BufType.FloatBE);
            var res1 = enc.ToParser().WithXorDecrypt(KEY).Parse(schema);
            var res2 = plain.ToParser().Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"XOR 암호화 프레임 (KEY=0x{KEY:X2})"));
            sb.AppendLine($"  원본 : {plain.ToHexString()}");
            sb.AppendLine($"  암호화: {enc.ToHexString()}");
            sb.AppendLine(); sb.AppendLine(ViewHelper.Line("복호화 후 파싱")); sb.Append(res1.ToString());
            sb.AppendLine(); sb.AppendLine($"  Value 일치: {(res1.GetFloat("Value") == res2.GetFloat("Value") ? "✅" : "❌")}");
            return sb.ToString();
        }

        static string ScenSliding()
        {
            byte[] header = [0xFF, 0xFF, 0xFF, 0xFF];
            byte[] fA = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(0x01).WriteFloatBE(10.0f).WriteUInt32BE(1).ToBytes();
            byte[] fB = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(0x02).WriteFloatBE(20.0f).WriteUInt32BE(2).ToBytes();
            byte[] rx = [.. header, .. fA, .. fB];
            var schema = new BufSchema().Then("STX", BufType.UInt8).Then("ID", BufType.UInt8).Then("Value", BufType.FloatBE).Then("Count", BufType.UInt32BE);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("슬라이딩 윈도우 (WithOffset)"));
            sb.AppendLine($"  전체({rx.Length}B): {rx.ToHexString()}");
            foreach (var (buf, label) in new[] { (rx, "rx"), })
            {
                var resA = rx.ToParser().WithOffset(4).Parse(schema);
                var resB = rx.ToParser().WithOffset(12).Parse(schema);
                sb.AppendLine(); sb.AppendLine(ViewHelper.Line("프레임A (offset=4)")); sb.Append(resA.ToString());
                sb.AppendLine(); sb.AppendLine(ViewHelper.Line("프레임B (offset=12)")); sb.Append(resB.ToString());
                break;
            }
            return sb.ToString();
        }

        static string ScenCrcVerify()
        {
            byte[] data = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(0x03).WriteFloatBE(3.14f).ToBytes();
            byte[] w = data.AppendCrc32();
            byte[] tampered = (byte[])w.Clone(); tampered[2] ^= 0xFF;
            var schema = new BufSchema().Then("STX", BufType.UInt8).Then("FC", BufType.UInt8).Then("Value", BufType.FloatBE);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("CRC-32 검증 후 조건부 파싱"));
            foreach (var (f, label) in new[] { (w, "유효"), (tampered, "훼손") })
            {
                bool ok = f.VerifyCrc32();
                sb.AppendLine($"  [{label}] CRC:{(ok ? "✅" : "❌")}  {f.ToHexString()}");
                if (ok) { var r = f[..^4].ToParser().Parse(schema); sb.AppendLine(r.ToString()); }
                else sb.AppendLine("  → 파싱 중단");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string ScenDynSchema()
        {
            string json = """[{"Name":"STX","Type":"UInt8","Offset":0},{"Name":"FC","Type":"UInt8","Offset":1},{"Name":"Value","Type":"FloatBE","Offset":2},{"Name":"Price","Type":"DecimalLE","Offset":6}]""";
            var schema = BufSchema.FromJson(json);
            byte[] buf = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(0x03).WriteFloatBE(3.14f).WriteDecimalLE(123.456m).ToBytes();
            var result = buf.ToParser().Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("JSON 설정 → BufSchema 동적 생성"));
            sb.AppendLine($"  JSON: {json}"); sb.AppendLine();
            sb.AppendLine($"  스키마: {schema}");
            sb.AppendLine($"  버퍼  : {buf.ToHexString()}  ({buf.Length}B)");
            sb.AppendLine(); sb.Append(result.ToString());
            sb.AppendLine();
            sb.AppendLine($"  GetDecimal(\"Price\") = {result.GetDecimal("Price"):G}m");
            return sb.ToString();
        }

        static string ScenPipeline()
        {
            var frames = new[] { new[] { 0xAA, (byte)0x01, 0x09, 0xC4, (byte)0x0C, 0x80, (byte)0x08, 0x00 }.Select(x => (byte)x).ToArray(), new[] { 0xAA, (byte)0x02, (byte)0x0E, 0x74, (byte)0x18, 0x80, (byte)0x0A, 0x00 }.Select(x => (byte)x).ToArray() };
            var schema = new BufSchema().Then("STX", BufType.UInt8).Then("ID", BufType.UInt8).Then("TempADC", BufType.UInt16BE).Then("HumiADC", BufType.UInt16BE).Then("VoltADC", BufType.UInt16BE);
            var records = new List<ProcessedSensor>();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("① 파싱"));
            foreach (var f in frames)
            {
                var r = f.ToParser().Parse(schema);
                ushort t = r.Get<ushort>("TempADC"), h = r.Get<ushort>("HumiADC"), v = r.Get<ushort>("VoltADC");
                records.Add(new ProcessedSensor(r.Get<byte>("ID"), ((double)t).MapTo(0, 4095, -40.0, 125.0), 
                                                                   ((double)h).MapTo(0, 4095, 0.0, 100.0), 
                                                                   ((double)v).MapTo(0, 4095, 0.0, 3.3)));
                sb.AppendLine($"  ID={r.Get<byte>("ID")}  {f.ToHexString()}");
            }
            sb.AppendLine(); sb.AppendLine(ViewHelper.Line("② Scale"));
            foreach (var r in records) sb.AppendLine($"  ID={r.Id}  {r.Temp:F2}°C  {r.Humi:F1}%  {r.Volt:F4}V");
            sb.AppendLine(); sb.AppendLine(ViewHelper.Line("③ CSV"));
            sb.Append(records.ToCsv().TrimEnd());
            return sb.ToString();
        }

        static string ScenStructRound()
        {
            var pkt = new SensorPacket { Header = 0xAA, DeviceId = 1, Sequence = 1001, TempAdc = 2500, HumiAdc = 3200, Flags = 0b11, Checksum = 0 };
            byte[] raw = pkt.ToBytes(); pkt.Checksum = raw.Take(raw.Length - 1).ToArray().Sum8(); raw = pkt.ToBytes();
            SensorPacket r = raw.To<SensorPacket>();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("struct → ToBytes() → To<T>() 왕복"));
            sb.AppendLine(pkt.Dump()); sb.AppendLine();
            sb.AppendLine($"  ByteEquals: {(pkt.ByteEquals(r) ? "✅" : "❌")}");
            sb.AppendLine($"  Checksum  : 0x{r.Checksum:X2}  Sum8==0: {(raw.Sum8() == 0 ? "✅" : "❌")}");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════
        //  시나리오 ⑨ ~ ⑩  (BufferWriter)
        // ════════════════════════════════════════════════

        static string ScenWriterRequest()
        {
            // 요청 프레임 생성 (ReadHoldingRegisters FC03)
            byte slaveId = 0x01; ushort startAddr = 0x0000, regCount = 0x0004;
            var bw = BufferWriter.Create()
                .WriteUInt8(slaveId).WriteUInt8(0x03)
                .WriteUInt16BE(startAddr).WriteUInt16BE(regCount).WritePad(2);
            ushort crc = bw.ToBytes()[..^2].Crc16Modbus();
            bw.PatchUInt16BE(4, (ushort)((crc & 0xFF) << 8 | (crc >> 8))); // LE→BE swap
            byte[] req = bw.ToBytes();
            // 응답 시뮬레이션: [01][03][08][00 64][01 2C][00 00][03 E9][xx xx]
            float t1 = 100f, t2 = 300f, t3 = 0f, t4 = 1001f;
            byte[] payload = BufferWriter.Create()
                .WriteUInt8(0x01).WriteUInt8(0x03).WriteUInt8(8)
                .WriteUInt16BE((ushort)t1).WriteUInt16BE((ushort)t2)
                .WriteUInt16BE((ushort)t3).WriteUInt16BE((ushort)t4).WritePad(2)
                .ToBytes();
            ushort resCrc = payload[..^2].Crc16Modbus();
            payload[^2] = (byte)(resCrc & 0xFF); payload[^1] = (byte)(resCrc >> 8);
            // 응답 파싱
            var schema = new BufSchema()
                .Add("SlaveId", BufType.UInt8, 0).Add("FC", BufType.UInt8, 1).Add("ByteCount", BufType.UInt8, 2)
                .Add("Reg0", BufType.UInt16BE, 3).Add("Reg1", BufType.UInt16BE, 5)
                .Add("Reg2", BufType.UInt16BE, 7).Add("Reg3", BufType.UInt16BE, 9);
            var result = payload[..^2].ToParser().Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("BufferWriter — 요청 프레임 생성"));
            sb.AppendLine($"  요청({req.Length}B): {req.ToHexString()}");
            sb.AppendLine(); sb.AppendLine(ViewHelper.Line("응답 파싱"));
            sb.AppendLine($"  응답({payload.Length}B): {payload.ToHexString()}");
            sb.Append(result.ToString());
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("레지스터 → 물리값 (× 0.1)"));
            for (int i = 0; i < 4; i++) sb.AppendLine($"  Reg[{i}] = {result.GetInt($"Reg{i}")} → {result.GetInt($"Reg{i}") * 0.1:F1}");
            return sb.ToString();
        }

        static string ScenWriterRoundtrip()
        {
            var values = new[] { 0m, 1m, -1m, decimal.MaxValue, 0.000000000000000000000000001m, 123456789.123456789m };
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("BufferWriter → ToParser 왕복 검증 (decimal)"));
            sb.AppendLine($"  {"값",-36}  {"복원",-36}  {"일치"}");
            foreach (var v in values)
            {
                var bw = BufferWriter.Create().WriteDecimalLE(v);
                decimal back = bw.ToParser().ReadDecimalLE(0);
                sb.AppendLine($"  {v.ToString("G"),-36}  {back.ToString("G"),-36}  {(v == back ? "✅" : "❌")}");
            }
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("전체 타입 왕복"));
            var frame = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteInt16BE(-500).WriteUInt32LE(999999u)
                .WriteFloatBE(3.14f).WriteDoubleLE(Math.PI)
                .WriteDecimalLE(1234567890.123456789m)
                .WriteStringAscii("TestFrame", fixedLen: 12)
                .ToBytes();
            sb.AppendLine($"  프레임({frame.Length}B): {frame.ToHexString()}");
            var bp = frame.ToParser();
            sb.AppendLine($"  UInt8(0)       = 0x{bp.ReadUInt8(0):X2}");
            sb.AppendLine($"  Int16BE(1)     = {bp.ReadInt16BE(1)}");
            sb.AppendLine($"  UInt32LE(3)    = {bp.ReadUInt32LE(3)}");
            sb.AppendLine($"  FloatBE(7)     = {bp.ReadFloatBE(7):G9}");
            sb.AppendLine($"  DoubleLE(11)   = {bp.ReadDoubleLE(11):G15}");
            sb.AppendLine($"  DecimalLE(19)  = {bp.ReadDecimalLE(19):G}m");
            sb.AppendLine($"  StringAscii(35)= \"{bp.ReadStringAscii(35, 12)}\"");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════
        //  시나리오 ⑪ ~ ⑫  (RingBuffer)
        // ════════════════════════════════════════════════

        static string ScenRingBuffer()
        {
            var ring = new RingBuffer(1024);
            var schema = new BufSchema()
                .Then("STX", BufType.UInt8).Then("ID", BufType.UInt8)
                .Then("Value", BufType.FloatBE).Then("Seq", BufType.UInt16BE);

            // 수신 시뮬레이션: 쓰레기 + 프레임 3개를 여러 번 나눠서 Write
            byte[] garbage = [0xCC, 0xFF, 0x00];
            var chunk1 = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(1).WriteFloatBE(10.5f).WriteUInt16BE(1).ToBytes();
            var chunk2 = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(2).WriteFloatBE(20.3f).WriteUInt16BE(2).ToBytes();
            var chunk3 = BufferWriter.Create().WriteUInt8(0xAA).WriteUInt8(3).WriteFloatBE(30.1f).WriteUInt16BE(3).ToBytes();

            // 실제 TCP 수신처럼 조각조각 Write
            ring.Write(garbage);
            ring.Write(chunk1[..4]);         // 첫 4바이트만 먼저
            ring.Write(chunk1[4..]);          // 나머지
            ring.Write(chunk2);
            ring.Write([0xBB, 0xBB]);         // 쓰레기
            ring.Write(chunk3);

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("RingBuffer — 수신 스트림 프레임 자동 추출"));
            sb.AppendLine($"  버퍼 상태: {ring}");
            sb.AppendLine($"  총 수신: garbage(3B) + 프레임3개(각 8B) + 쓰레기(2B)");
            sb.AppendLine();

            int extracted = 0;
            while (ring.TryReadFrame(stx: 0xAA, length: 8, out byte[] frame))
            {
                var result = frame.ToParser().Parse(schema);
                extracted++;
                sb.AppendLine($"  [프레임 {extracted}] {frame.ToHexString()}");
                sb.AppendLine($"    ID={result.GetInt("ID")}  Value={result.GetFloat("Value"):F2}  Seq={result.GetInt("Seq")}");
            }
            sb.AppendLine();
            sb.AppendLine($"  추출 완료: {extracted}개  남은 데이터: {ring.Count}B");
            sb.AppendLine($"  → STX 이전 쓰레기 데이터 자동 제거됨");
            return sb.ToString();
        }

        static string ScenRingVariable()
        {
            var ring = new RingBuffer(4096, threadSafe: true);

            // 가변 프레임 빌더: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
            byte[] MakeVarFrame(byte fc, decimal price, float value)
            {
                var data = BufferWriter.Create()
                    .WriteDecimalLE(price).WriteFloatBE(value).ToBytes();   // 20바이트
                return BufferWriter.Create()
                    .WriteUInt8(0xAA).WriteUInt8(fc)
                    .WriteUInt16BE((ushort)data.Length)
                    .WriteRaw(data)
                    .ToBytes().AppendCrc32();  // +4바이트 CRC32
            }

            byte[] f1 = MakeVarFrame(0x01, 1234.56m, 3.14f);
            byte[] f2 = MakeVarFrame(0x02, 789.00m, 9.81f);

            // 분할 수신 시뮬레이션
            ring.Write([0xFF, 0xCC]);           // 쓰레기
            ring.Write(f1[..10]);               // 첫 10바이트
            ring.Write(f1[10..]);               // 나머지
            ring.Write(f2);

            var schema = new BufSchema()
                .Then("STX", BufType.UInt8)
                .Then("FC", BufType.UInt8)
                .Then("Length", BufType.UInt16BE)
                .Then("Price", BufType.DecimalLE)
                .Then("Value", BufType.FloatBE);

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("RingBuffer — 가변 길이 프레임 + CRC32 검증"));
            sb.AppendLine($"  프레임1: {f1.Length}B ({f1.ToHexString()[..40]}...)");
            sb.AppendLine($"  프레임2: {f2.Length}B");
            sb.AppendLine($"  버퍼 상태: {ring}");
            sb.AppendLine();

            int count = 0;
            while (ring.TryReadVariableFrame(
                stx: 0xAA, lengthOffset: 2, lengthSize: 2, bigEndian: true, overhead: 8,
                out byte[] frame))
            {
                count++;
                bool crcOk = frame.VerifyCrc32();
                if (!crcOk) { sb.AppendLine($"  [프레임{count}] CRC ❌"); continue; }

                byte[] payload = frame[..^4];  // CRC 제외
                var result = payload.ToParser().Parse(schema);
                sb.AppendLine($"  [프레임 {count}] {frame.Length}B  CRC:✅");
                sb.AppendLine($"    FC=0x{result.GetOr<byte>("FC"):X2}  Price={result.GetDecimal("Price"):G}m  Value={result.GetFloat("Value"):F4}");
            }
            sb.AppendLine($"\n  추출: {count}개  남음: {ring.Count}B");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════
        //  시나리오 ⑬ ~ ⑯  (BufferDiff)
        // ════════════════════════════════════════════════

        static BufSchema MakeDiffSchema() => new BufSchema()
            .Then("STX", BufType.UInt8)
            .Then("Config", BufType.UInt16BE)
            .Then("Value", BufType.FloatBE)
            .Then("Timestamp", BufType.UInt32LE)
            .Then("CRC", BufType.UInt16BE);

        static string ScenDiffBasic()
        {
            byte[] a = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0100).WriteFloatBE(3.14f)
                .WriteUInt32LE(1000000u).WriteUInt16BE(0xABCD).ToBytes();
            byte[] b = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0200).WriteFloatBE(9.81f)  // Config+Value 변경
                .WriteUInt32LE(2000000u).WriteUInt16BE(0x1234).ToBytes();    // Timestamp+CRC 변경

            var diff = BufferDiff.Compare(a, b);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("BufferDiff.Compare — 바이트 단위 비교"));
            sb.AppendLine($"  A: {a.ToHexString()}");
            sb.AppendLine($"  B: {b.ToHexString()}");
            sb.AppendLine();
            sb.AppendLine(diff.ToPatchString());
            sb.AppendLine();
            sb.AppendLine($"  HasChanges : {diff.HasChanges}");
            sb.AppendLine($"  IsIdentical: {diff.IsIdentical}");
            sb.AppendLine($"  Similarity : {diff.Similarity:P1}");
            sb.AppendLine($"  변경 offset: [{string.Join(", ", diff.ChangedOffsets.Select(o => $"0x{o:X2}"))}]");
            return sb.ToString();
        }

        static string ScenDiffMasked()
        {
            var schema = MakeDiffSchema();
            byte[] a = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0100).WriteFloatBE(3.14f)
                .WriteUInt32LE(1000000u).WriteUInt16BE(0xABCD).ToBytes();
            byte[] b = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0100).WriteFloatBE(3.14f)
                .WriteUInt32LE(2000000u).WriteUInt16BE(0x1234).ToBytes(); // Timestamp+CRC만 다름

            // Timestamp(3~6) + CRC(7~8) 를 무시
            bool rawEq = BufferDiff.IsEqual(a, b);
            bool maskedEq = a.MaskedEquals(b, ignoreOffsets: new[] { 3, 4, 5, 6, 7, 8 });
            bool schemaEq = a.MaskedEquals(b, schema, ignoreFields: new[] { "Timestamp", "CRC" });

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("MaskedEquals — CRC/타임스탬프 무시"));
            sb.AppendLine($"  A: {a.ToHexString()}");
            sb.AppendLine($"  B: {b.ToHexString()}  (Timestamp/CRC만 다름)");
            sb.AppendLine();
            sb.AppendLine($"  IsEqual(완전 비교)    : {rawEq}   ← Timestamp/CRC 때문에 다름");
            sb.AppendLine($"  MaskedEquals(offset)  : {maskedEq}  ← 페이로드 동일");
            sb.AppendLine($"  MaskedEquals(schema)  : {schemaEq}  ← 스키마 필드 이름으로 제외");
            sb.AppendLine();
            sb.AppendLine("  → 활용: 매번 바뀌는 타임스탬프/CRC 제외, 설정값만 비교");
            return sb.ToString();
        }

        static string ScenDiffFields()
        {
            var schema = MakeDiffSchema();
            byte[] a = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0100).WriteFloatBE(3.14f)
                .WriteUInt32LE(1000000u).WriteUInt16BE(0xABCD).ToBytes();
            byte[] b = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt16BE(0x0200).WriteFloatBE(9.81f)
                .WriteUInt32LE(2000000u).WriteUInt16BE(0x1234).ToBytes();

            var diff = BufferDiff.CompareFields(a, b, schema,
                ignoreFields: new[] { "Timestamp", "CRC" });

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("CompareFields — 스키마 기반 필드 단위 비교"));
            sb.AppendLine($"  A: {a.ToHexString()}");
            sb.AppendLine($"  B: {b.ToHexString()}");
            sb.AppendLine($"  무시 필드: Timestamp, CRC");
            sb.AppendLine();
            sb.AppendLine(diff.Summary);
            sb.AppendLine();
            sb.AppendLine($"  HasFieldChanged(\"Config\") = {diff.HasFieldChanged("Config")}");
            sb.AppendLine($"  HasFieldChanged(\"Value\")  = {diff.HasFieldChanged("Value")}");
            sb.AppendLine($"  HasFieldChanged(\"STX\")   = {diff.HasFieldChanged("STX")}");
            sb.AppendLine();
            sb.AppendLine($"  변경된 필드: [{string.Join(", ", diff.ChangedFieldNames)}]");
            return sb.ToString();
        }

        static string ScenDiffPatch()
        {
            byte[] fw1 = Enumerable.Range(0, 32).Select(i => (byte)(i * 3)).ToArray();
            byte[] fw2 = (byte[])fw1.Clone();
            fw2[4] = 0xFF; fw2[8] = 0xAA; fw2[16] = 0x11;  // 3곳 변경

            var diff = BufferDiff.Compare(fw1, fw2);
            byte[] restored = BufferDiff.ApplyPatches(fw1, diff);
            bool ok = BufferDiff.IsEqual(restored, fw2);

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("펌웨어 패치 생성 + 적용"));
            sb.AppendLine($"  FW1 ({fw1.Length}B): {fw1.ToHexString()}");
            sb.AppendLine($"  FW2 ({fw2.Length}B): {fw2.ToHexString()}");
            sb.AppendLine();
            sb.AppendLine(diff.ToPatchString());
            sb.AppendLine();
            sb.AppendLine($"  패치 적용 결과 ({restored.Length}B): {restored.ToHexString()}");
            sb.AppendLine($"  FW2 재현 성공: {(ok ? "✅" : "❌")}");
            sb.AppendLine();
            sb.AppendLine($"  유사도: {BufferDiff.Similarity(fw1, fw2):P1}");
            sb.AppendLine($"  공통 바이트: {BufferDiff.CommonBytes(fw1, fw2).Length}B / {fw1.Length}B");
            return sb.ToString();
        }
    }
}
