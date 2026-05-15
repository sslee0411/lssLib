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

using lssLib.Binary;
using lssLib.Extensions;

// ════════════════════════════════════════════════════════════════
//  HookView — 훅 체이닝 (WithLog/XorDecrypt/Stats/OnParseDone/Offset)
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// HookView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HookView : UserControl
    {
        public HookView() => InitializeComponent();

        private static BufSchema MakeSchema() => new BufSchema()
            .Then("STX", BufType.UInt8)
            .Then("FC", BufType.UInt8)
            .Then("Length", BufType.UInt16BE)
            .Then("Seq", BufType.UInt32BE)
            .Then("Value", BufType.FloatBE);

        private void OnWithLog(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] raw = ViewHelper.FromHex(HexIn.Text);
            var log = new StringBuilder();
            var result = raw.ToParser()
                .WithLog(msg => log.AppendLine(msg))
                .Parse(MakeSchema());
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("WithLog — 필드별 읽기 로그"));
            sb.Append(log);
            sb.AppendLine(ViewHelper.Line("파싱 결과"));
            sb.Append(result.ToString());
            return sb.ToString();
        });

        private void OnXorDecrypt(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            if (!byte.TryParse(XorKey.Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber, null, out byte key))
                key = 0xAA;
            byte[] original = ViewHelper.FromHex(HexIn.Text);
            byte[] encrypted = original.Select(b => (byte)(b ^ key)).ToArray();
            var schema = new BufSchema().Then("STX", BufType.UInt8).Then("FC", BufType.UInt8).Then("Value", BufType.FloatBE);
            var result = encrypted.ToParser().WithXorDecrypt(key).Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"WithXorDecrypt(key=0x{key:X2})"));
            sb.AppendLine($"  원본      : {original.ToHexString()}");
            sb.AppendLine($"  암호화    : {encrypted.ToHexString()}");
            sb.AppendLine($"  복호화 후 : {encrypted.ToParser().WithXorDecrypt(key).ToHex()}");
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("파싱 결과"));
            sb.Append(result.ToString());
            return sb.ToString();
        });

        private void OnWithStats(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] raw = ViewHelper.FromHex(HexIn.Text);
            var stats = new Dictionary<string, int>();
            var bp = raw.ToParser().WithStats(stats);
            bp.Parse(MakeSchema());
            if (raw.Length > 0) bp.ReadUInt8(0);
            if (raw.Length > 3) bp.ReadUInt16BE(2);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("WithStats — 읽기 타입별 횟수"));
            foreach (var (nm, cnt) in stats.OrderByDescending(p => p.Value))
                sb.AppendLine($"  {nm,-14} : {cnt}회");
            sb.AppendLine($"\n  총 {stats.Values.Sum()}회");
            return sb.ToString();
        });

        private void OnParseDone(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] raw = ViewHelper.FromHex(HexIn.Text);
            var log = new StringBuilder();
            raw.ToParser()
               .WithLog(msg => log.Append(msg))
               .OnParseDone((result, schema) =>
               {
                   log.AppendLine($"\n[OnParseDone 콜백]");
                   log.AppendLine($"  파싱 완료: {schema.Fields.Count}개 필드");
                   log.AppendLine($"  IsAllOk  : {result.IsAllOk}");
                   byte stx = result.GetOr<byte>("STX");
                   log.AppendLine($"  STX 검증 : 0x{stx:X2} {(stx == 0xAA ? "✅" : "⚠ 불일치")}");
               })
               .Parse(MakeSchema());
            return $"{ViewHelper.Line("OnParseDone — 파싱 완료 콜백")}\n{log}";
        });

        private void OnWithOffset(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] header = [0xFF, 0xFF, 0xFF, 0xFF];
            byte[] payload = ViewHelper.FromHex(HexIn.Text);
            byte[] full = [.. header, .. payload];
            var schema = new BufSchema().Then("STX", BufType.UInt8).Then("FC", BufType.UInt8).Then("Value", BufType.FloatBE);
            var result = full.ToParser().WithOffset(4).Parse(schema);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("WithOffset(4) — 헤더 건너뜀"));
            sb.AppendLine($"  전체 ({full.Length}B) : {full.ToHexString()}");
            sb.AppendLine($"  헤더  (4B)   : {header.ToHexString()}  (건너뜀)");
            sb.AppendLine($"  페이로드     : {payload.ToHexString()}");
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("파싱 결과"));
            sb.Append(result.ToString());
            return sb.ToString();
        });

        private void OnPreprocess(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] raw = ViewHelper.FromHex(HexIn.Text);
            var bp = raw.ToParser().WithPreprocess(data => data.Select(b => (byte)(b ^ 0xFF)).ToArray());
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("WithPreprocess — 커스텀 전처리 (XOR 0xFF)"));
            sb.AppendLine($"  원본   : {raw.ToHexString()}");
            sb.AppendLine($"  변환 후: {bp.ToHex()}");
            sb.AppendLine();
            sb.AppendLine("  [활용 예]");
            sb.AppendLine("  .WithPreprocess(data => AesDecrypt(data, key, iv))");
            sb.AppendLine("  .WithPreprocess(data => data.Skip(4).ToArray())  // 헤더 제거");
            return sb.ToString();
        });

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}
