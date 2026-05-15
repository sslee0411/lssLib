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
//  DecimalView   — decimal 전용 (byte[] ↔ decimal / 배열 / 분해 / 스키마)
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// DecimalView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DecimalView : UserControl
    {
        public DecimalView() => InitializeComponent();

        private void OnToBytes(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            decimal v = decimal.Parse(DecInput.Text.Trim());
            byte[] le = v.ToBytes();
            byte[] be = v.ToBigEndianBytes();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("decimal → 16바이트 LE / BE"));
            sb.AppendLine($"  값    : {v:G}");
            sb.AppendLine($"  LE    : {le.ToHexString()}");
            sb.AppendLine($"  BE    : {be.ToHexString()}");
            sb.AppendLine($"  복원LE: {le.ReadDecimalLE():G}  {(le.ReadDecimalLE() == v ? "✅" : "❌")}");
            sb.AppendLine($"  복원BE: {be.ReadDecimalBE():G}  {(be.ReadDecimalBE() == v ? "✅" : "❌")}");
            return sb.ToString();
        });

        private void OnFromBytes(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] buf = ViewHelper.FromHex(HexIn.Text);
            if (buf.Length < 16) return $"16바이트 이상 필요 (현재: {buf.Length}B)";
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("byte[] → decimal 읽기"));
            sb.AppendLine($"  HEX       : {buf.ToHexString()}");
            sb.AppendLine($"  byte[].ReadDecimalLE(0) = {buf.ReadDecimalLE():G}");
            sb.AppendLine($"  byte[].ReadDecimalBE(0) = {buf.ReadDecimalBE():G}");
            sb.AppendLine($"  bp.ReadDecimalLE(0)     = {buf.ToParser().ReadDecimalLE(0):G}");
            return sb.ToString();
        });

        private void OnDecompose(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            decimal v = decimal.Parse(DecInput.Text.Trim());
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("decimal.Decompose() — 내부 구조"));
            sb.AppendLine(v.Decompose().ToString());
            sb.AppendLine();
            sb.AppendLine(ViewHelper.Line("추가 케이스"));
            foreach (decimal d in new[] { 0m, 1m, -1m, decimal.MaxValue, decimal.MinValue, 0.001m })
            {
                var info = d.Decompose();
                sb.AppendLine($"  {d.ToString().PadRight(34)} Sign={info.IsNegative}  Scale={info.Scale}");
            }
            return sb.ToString();
        });

        private void OnArrayToBytes(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var parts = ArrInput.Text.Split(',').Select(t => decimal.Parse(t.Trim())).ToArray();
            byte[] le = parts.ToLEBytes();
            byte[] be = parts.ToBEBytes();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"decimal[{parts.Length}] → byte[] LE/BE"));
            sb.AppendLine($"  입력 : [{string.Join(", ", parts.Select(d => d.ToString("G")))}]");
            sb.AppendLine($"  LE ({le.Length}B):");
            for (int i = 0; i < parts.Length; i++)
                sb.AppendLine($"    [{i}] {le.Skip(i * 16).Take(16).ToArray().ToHexString()}");
            sb.AppendLine($"  BE ({be.Length}B):");
            for (int i = 0; i < parts.Length; i++)
                sb.AppendLine($"    [{i}] {be.Skip(i * 16).Take(16).ToArray().ToHexString()}");
            return sb.ToString();
        });

        private void OnBytesToArray(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var parts = ArrInput.Text.Split(',').Select(t => decimal.Parse(t.Trim())).ToArray();
            byte[] le = parts.ToLEBytes();
            decimal[] r1 = le.ToDecimalLEArray(0, parts.Length);
            byte[] be = parts.ToBEBytes();
            decimal[] r2 = be.ToDecimalBEArray(0, parts.Length);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("byte[] → decimal[] 왕복"));
            sb.AppendLine($"  원본  : [{string.Join(", ", parts.Select(d => d.ToString("G")))}]");
            sb.AppendLine($"  LE복원: [{string.Join(", ", r1.Select(d => d.ToString("G")))}]  {(parts.Zip(r1).All(p => p.First == p.Second) ? "✅" : "❌")}");
            sb.AppendLine($"  BE복원: [{string.Join(", ", r2.Select(d => d.ToString("G")))}]  {(parts.Zip(r2).All(p => p.First == p.Second) ? "✅" : "❌")}");
            return sb.ToString();
        });

        private void OnSchemaParse(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var prices = new decimal[] { 1234.56m, 789.00m };
            byte[] raw = BufferWriter.Create()
                .WriteUInt8(0xAA).WriteUInt8(0x01)
                .WriteDecimalLEArray(prices)
                .ToBytes();
            var schema = new BufSchema()
                .Then("STX", BufType.UInt8)
                .Then("FC", BufType.UInt8)
                .Then("Price1", BufType.DecimalLE)
                .Then("Price2", BufType.DecimalLE);
            var result = raw.ToParser().Parse(schema);
            decimal p1 = result.GetDecimal("Price1");
            decimal p2 = result.GetDecimal("Price2");
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("BufSchema DecimalLE 파싱 + GetDecimal()"));
            sb.AppendLine($"  버퍼   : {raw.ToHexString()}  ({raw.Length}B)");
            sb.AppendLine($"  스키마 : {schema}");
            sb.AppendLine();
            sb.Append(result.ToString());
            sb.AppendLine();
            sb.AppendLine($"  Price1 + Price2 = {p1 + p2:G}  (decimal 정밀 계산)");
            return sb.ToString();
        });

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}
