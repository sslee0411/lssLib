using System.Text;
using System.Windows;
using System.Windows.Controls;

using lssLib.Binary;
using lssLib.Extensions;
// ====================================================================
//  BufferView    — BufferParser + BufferWriter + StreamParser
// ====================================================================

namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// BufferView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BufferView : UserControl
    {
        public BufferView() => InitializeComponent();


        // ── BufferParser ──────────────────────────────────────────
        private void OnSchemaAdd(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] buf = ViewHelper.FromHex(HexIn.Text);
            var schema = ViewHelper.ParseSchemaText(SchemaIn.Text);
            var result = buf.ToParser().Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("스키마 파싱 (Add — offset 직접 지정)"));
            sb.AppendLine($"  버퍼   : {buf.ToHexString()}  ({buf.Length}B)");
            sb.AppendLine($"  스키마 : {schema}");
            sb.AppendLine(ViewHelper.Line());
            sb.Append(result.ToString());
            return sb.ToString();
        });

        private void OnSchemaThen(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] buf = ViewHelper.FromHex(HexIn.Text);
            var schema = new BufSchema()
                .Then("STX", BufType.UInt8)
                .Then("FC", BufType.UInt8)
                .Then("Length", BufType.UInt16BE)
                .Then("Seq", BufType.UInt32BE)
                .Then("Value", BufType.FloatBE)
                .Then("Name", BufType.StringAscii, size: 6);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("Then() — offset 자동 계산"));
            sb.AppendLine($"  {schema}");
            foreach (var f in schema.Fields)
                sb.AppendLine($"  {f.Name,-12} offset={f.Offset,3}  size={BufSchema.FieldBytes(f)}B");
            sb.AppendLine();
            if (buf.Length >= 18)
            {
                var result = buf.ToParser().Parse(schema);
                sb.AppendLine(ViewHelper.Line("파싱 결과"));
                sb.Append(result.ToString());
            }
            return sb.ToString();
        });

        private void OnDirect(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] buf = ViewHelper.FromHex(HexIn.Text);
            var bp = buf.ToParser();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("직접 읽기 (ReadXxx)"));
            sb.AppendLine($"  HEX  : {bp.ToHex()}  ({buf.Length}B)");
            sb.AppendLine();
            if (buf.Length > 0) sb.AppendLine($"  ReadUInt8  (0)  = 0x{bp.ReadUInt8(0):X2} ({bp.ReadUInt8(0)})");
            if (buf.Length > 1) sb.AppendLine($"  ReadUInt8  (1)  = 0x{bp.ReadUInt8(1):X2} ({bp.ReadUInt8(1)})");
            if (buf.Length > 3) sb.AppendLine($"  ReadUInt16BE(2) = {bp.ReadUInt16BE(2)}");
            if (buf.Length > 7) sb.AppendLine($"  ReadUInt32BE(4) = {bp.ReadUInt32BE(4)}");
            if (buf.Length > 11) sb.AppendLine($"  ReadFloatBE(8)  = {bp.ReadFloatBE(8):G9}");
            if (buf.Length > 12)
            {
                int sLen = Math.Min(6, buf.Length - 12);
                sb.AppendLine($"  ReadStringAscii(12,{sLen}) = \"{bp.ReadStringAscii(12, sLen)}\"");
            }
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("HexDump"));
            sb.AppendLine(bp.ToBytes().ToHexDump());
            return sb.ToString();
        });

        private void OnArrayRead(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] fBuf = [0x3F,0x80,0x00,0x00, 0x40,0x00,0x00,0x00,
                           0x40,0x40,0x00,0x00, 0x40,0x80,0x00,0x00];
            float[] fs = fBuf.ToParser().ReadFloatBEArray(0, 4);
            byte[] u16Buf = [0x00, 0x64, 0x01, 0x2C, 0x04, 0x00, 0x0A, 0x00];
            ushort[] u16s = u16Buf.ToParser().ReadUInt16BEArray(0, 4);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("배열 읽기"));
            sb.AppendLine($"  [floatBE × 4]  : {fBuf.ToHexString()}");
            sb.AppendLine($"  ReadFloatBEArray(0,4) = [{string.Join(", ", fs)}]");
            sb.AppendLine();
            sb.AppendLine($"  [ushortBE × 4] : {u16Buf.ToHexString()}");
            sb.AppendLine($"  ReadUInt16BEArray(0,4) = [{string.Join(", ", u16s)}]");
            return sb.ToString();
        });

        private void OnBitRead(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] buf = ViewHelper.FromHex(HexIn.Text);
            if (buf.Length == 0) return "버퍼를 입력하세요.";
            var bp = buf.ToParser();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("비트 필드 읽기"));
            sb.AppendLine($"  byte[0] = 0x{buf[0]:X2}  (0b{Convert.ToString(buf[0], 2).PadLeft(8, '0')})");
            sb.AppendLine();
            for (int i = 7; i >= 0; i--)
                sb.AppendLine($"  ReadBit(0, bit:{i}) = {(bp.ReadBit(0, i) ? 1 : 0)}  {(i == 7 ? "← MSB" : i == 0 ? "← LSB" : "")}");
            sb.AppendLine();
            sb.AppendLine($"  ReadBitField(0, mask:0x0F) = 0x{bp.ReadBitField(0, 0x0F):X2}  (하위 4비트)");
            sb.AppendLine($"  ReadBitField(0, mask:0xF0) = 0x{bp.ReadBitField(0, 0xF0):X2}  (상위 4비트)");
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("HexDump (전체 버퍼)"));
            sb.Append(bp.ToBytes().ToHexDump());
            return sb.ToString();
        });

        // ── BufferWriter ──────────────────────────────────────────
        private void OnWriterChain(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var bw = BufferWriter.Create()
                .WriteUInt8(0xAA)
                .WriteUInt8(0x03)
                .WriteUInt16BE(256)
                .WriteInt32LE(1001)
                .WriteFloatBE(3.14f)
                .WriteStringAscii("Hello", fixedLen: 8)
                .WritePad(2);
            byte[] frame = bw.ToBytes();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("BufferWriter — Write 체이닝"));
            sb.AppendLine($"  WriteUInt8(0xAA) + WriteUInt8(0x03) + WriteUInt16BE(256)");
            sb.AppendLine($"  + WriteInt32LE(1001) + WriteFloatBE(3.14f)");
            sb.AppendLine($"  + WriteStringAscii(\"Hello\", fixedLen:8) + WritePad(2)");
            sb.AppendLine();
            sb.AppendLine($"  결과 ({frame.Length}B): {frame.ToHexString()}");
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("HexDump"));
            sb.Append(frame.ToHexDump());
            return sb.ToString();
        });

        private void OnWriterDecimal(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            decimal v = 123.456m;
            decimal[] arr = { 1234.56m, 789.00m, 100.50m };

            byte[] leFrame = BufferWriter.Create()
                .WriteUInt8(0xAA)
                .WriteDecimalLE(v)
                .ToBytes();

            byte[] arrFrame = BufferWriter.Create()
                .WriteUInt8(0xBB)
                .WriteDecimalLEArray(arr)
                .ToBytes();

            decimal vBack = leFrame.ToParser().ReadDecimalLE(1);
            decimal[] arrBack = arrFrame.ToParser().ReadDecimalLEArray(1, 3);

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("WriteDecimalLE / WriteDecimalLEArray"));
            sb.AppendLine($"  WriteDecimalLE({v:G})");
            sb.AppendLine($"  → {leFrame.ToHexString()}  ({leFrame.Length}B)");
            sb.AppendLine($"  복원: {vBack:G}  {(v == vBack ? "✅" : "❌")}");
            sb.AppendLine();
            sb.AppendLine($"  WriteDecimalLEArray([{string.Join(", ", arr.Select(d => d.ToString("G")))}])");
            sb.AppendLine($"  → {arrFrame.ToHexString()}  ({arrFrame.Length}B)");
            sb.AppendLine($"  복원: [{string.Join(", ", arrBack.Select(d => d.ToString("G")))}]");
            sb.AppendLine($"  일치: {(arr.Zip(arrBack).All(p => p.First == p.Second) ? "✅" : "❌")}");
            return sb.ToString();
        });

        private void OnWriterPatch(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var bw = BufferWriter.Create()
                .WriteUInt8(0xAA)
                .WriteUInt8(0x03)
                .WriteUInt16BE(0)      // Length 자리 예약
                .WriteFloatBE(3.14f)
                .WritePad(1);          // CRC 자리 예약

            // PatchUInt16BE: 실제 길이 삽입 (헤더 4B 이후 ~ CRC 1B 이전)
            bw.PatchUInt16BE(offset: 2, (ushort)(bw.Length - 4 - 1));

            // PatchByte: CRC 계산 후 삽입
            byte[] pre = bw.ToBytes();
            byte cs = pre.Sum8(0, pre.Length - 1);
            bw.PatchByte(pre.Length - 1, cs);

            byte[] frame = bw.ToBytes();
            bool csOk = frame.Sum8() == 0x00;

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("PatchByte / PatchUInt16BE — 사후 삽입"));
            sb.AppendLine($"  [0xAA][0x03][Length:2B BE][FloatBE(3.14)][CRC:1B]");
            sb.AppendLine($"  프레임 ({frame.Length}B): {frame.ToHexString()}");
            sb.AppendLine($"  Length 필드 = {frame[2] << 8 | frame[3]}");
            sb.AppendLine($"  CRC = 0x{frame[^1]:X2}");
            sb.AppendLine($"  frame.Sum8() == 0x00 ? {(csOk ? "✅ PASS" : "❌ FAIL")}");
            return sb.ToString();
        });

        private void OnWriterVerify(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            decimal original = 999999.999999999999999999999m;
            var bw = BufferWriter.Create()
                .WriteUInt8(0xAA)
                .WriteDecimalLE(original)
                .WriteUInt32LE(0xDEADBEEF);
            var parser = bw.ToParser();
            byte stx = parser.ReadUInt8(0);
            decimal back = parser.ReadDecimalLE(1);
            uint magic = parser.ReadUInt32LE(17);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("ToParser() — Writer → Parser 왕복 검증"));
            sb.AppendLine($"  BufferWriter → ToArray() → BufferParser");
            sb.AppendLine($"  프레임 ({bw.Length}B): {bw.ToHex()}");
            sb.AppendLine();
            sb.AppendLine($"  ReadUInt8(0)     = 0x{stx:X2}  {(stx == 0xAA ? "✅" : "❌")}");
            sb.AppendLine($"  ReadDecimalLE(1) = {back:G}");
            sb.AppendLine($"  일치: {(original == back ? "✅ 손실 없음" : "❌")}");
            sb.AppendLine($"  ReadUInt32LE(17) = 0x{magic:X8}  {(magic == 0xDEADBEEF ? "✅" : "❌")}");
            return sb.ToString();
        });

        // ── StreamParser ─────────────────────────────────────────
        private void OnStreamFixed(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            // 수신 버퍼 시뮬레이션: 쓰레기 + 프레임A + 쓰레기 + 프레임B
            byte[] garbage1 = [0xFF, 0xFF, 0x00];
            byte[] frameA = [0xAA, 0x01, 0x41, 0x20, 0x00, 0x00, 0x00, 0x0A];
            byte[] garbage2 = [0xCC, 0xDD];
            byte[] frameB = [0xAA, 0x02, 0x40, 0x00, 0x00, 0x00, 0x00, 0x14];
            byte[] rxBuf = [.. garbage1, .. frameA, .. garbage2, .. frameB];

            var schema = new BufSchema()
                .Then("STX", BufType.UInt8)
                .Then("ID", BufType.UInt8)
                .Then("Value", BufType.FloatBE)
                .Then("Count", BufType.UInt32BE);

            var sp = new StreamParser(rxBuf);
            var results = new List<BufResult>();
            while (sp.FindNext(stx: 0xAA, frameLen: 8, out int offset))
            {
                results.Add(sp.Slice(offset, 8).ToParser().Parse(schema));
                sp.Advance(offset + 8);
            }
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("StreamParser — 고정 길이 프레임 (STX=0xAA, 8B)"));
            sb.AppendLine($"  수신 버퍼 ({rxBuf.Length}B): {rxBuf.ToHexString()}");
            sb.AppendLine($"  쓰레기: {garbage1.ToHexString()} + {garbage2.ToHexString()}");
            sb.AppendLine($"  추출된 프레임: {results.Count}개");
            foreach (var (r, i) in results.Select((r, i) => (r, i)))
            {
                sb.AppendLine($"\n  [프레임 {i + 1}]");
                sb.AppendLine($"    ID={r.GetInt("ID")}  Value={r.GetFloat("Value"):G6}  Count={r.GetInt("Count")}");
            }
            return sb.ToString();
        });

        private void OnStreamVariable(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            // 구조: [STX:1B][FC:1B][Length:2B BE][Data:NB][CRC32:4B]
            byte[] MakeFrame(byte fc, byte[] data)
            {
                var bw = BufferWriter.Create()
                    .WriteUInt8(0xAA)
                    .WriteUInt8(fc)
                    .WriteUInt16BE((ushort)data.Length)
                    .WriteRaw(data);
                byte[] f = bw.ToBytes().AppendCrc32();
                return f;
            }
            byte[] f1 = MakeFrame(0x01, new byte[] { 0x01, 0x02, 0x03 });
            byte[] f2 = MakeFrame(0x02, new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 });
            byte[] rxBuf = [0xFF, .. f1, 0xCC, .. f2];

            var sp = new StreamParser(rxBuf);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("StreamParser — 가변 길이 프레임 (Length 필드)"));
            sb.AppendLine($"  수신 버퍼 ({rxBuf.Length}B): {rxBuf.ToHexString()}");
            sb.AppendLine();
            int frameCount = 0;
            while (sp.Remaining > 8)
            {
                if (!sp.FindNext(stx: 0xAA, frameLen: 8, out int pos)) break;
                int dataLen = sp.ReadUInt16BE(pos + 2);
                int total = 1 + 1 + 2 + dataLen + 4;
                if (!sp.HasBytes(pos, total)) break;
                byte[] frame = sp.Slice(pos, total);
                bool crcOk = frame.VerifyCrc32();
                frameCount++;
                sb.AppendLine($"  [프레임 {frameCount}] offset={pos}  total={total}B  CRC:{(crcOk ? "✅" : "❌")}");
                sb.AppendLine($"    FC=0x{frame[1]:X2}  DataLen={dataLen}  Data={frame[4..^4].ToHexString()}");
                sp.Advance(pos + total);
            }
            sb.AppendLine($"\n  총 {frameCount}개 프레임 추출");
            return sb.ToString();
        });

        private void OnStreamMultiStx(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            // 0xAA 타입A(8B) + 0xBB 타입B(8B) 혼재
            byte[] typeA = [0xAA, 0x01, 0x41, 0x20, 0x00, 0x00, 0x00, 0x0A];
            byte[] typeB = [0xBB, 0x02, 0x3F, 0x80, 0x00, 0x00, 0x00, 0x64];
            byte[] rxBuf = [0xFF, .. typeA, 0xCC, .. typeB];

            var schemaA = new BufSchema()
                .Then("STX", BufType.UInt8).Then("ID", BufType.UInt8)
                .Then("Value", BufType.FloatBE).Then("Extra", BufType.UInt16BE).Then("Pad", BufType.UInt8);
            var schemaB = new BufSchema()
                .Then("STX", BufType.UInt8).Then("TYPE", BufType.UInt8)
                .Then("Ratio", BufType.FloatBE).Then("Count", BufType.UInt16BE).Then("Pad", BufType.UInt8);

            var sp = new StreamParser(rxBuf);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("StreamParser — 다중 STX 패턴 (0xAA/0xBB)"));
            sb.AppendLine($"  수신 버퍼 ({rxBuf.Length}B): {rxBuf.ToHexString()}");
            sb.AppendLine();
            while (sp.FindNext(new byte[] { 0xAA, 0xBB }, 8, out int pos, out byte found))
            {
                var schema = found == 0xAA ? schemaA : schemaB;
                var result = sp.Slice(pos, 8).ToParser().Parse(schema);
                sb.AppendLine($"  STX=0x{found:X2}  → {(found == 0xAA ? "schemaA" : "schemaB")}");
                sb.Append(string.Concat(result.ToString().Split('\n').Select(l => "    " + l + "\n")));
                sp.Advance(pos + 8);
            }
            return sb.ToString();
        });

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}