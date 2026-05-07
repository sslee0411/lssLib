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
//  TypeView      — Text 확장 (파싱 / IEEE754 / JSON / XML / CSV)
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// TypeView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TypeView : UserControl
    {
        public TypeView() => InitializeComponent();

        private void OnParseInt(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            string txt = Input.Text.Trim();
            int v = txt.ToInt32();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"\"{txt}\".ToInt32()"));
            sb.AppendLine($"  Dec : {v}");
            sb.AppendLine($"  Hex : 0x{v:X8}");
            sb.AppendLine($"  Bin : {v.ToBinString(32)}");
            sb.AppendLine();
            sb.AppendLine("  [접두사 비교]");
            foreach (var (sample, label) in new[] { ("255", "10진"), ("0xFF", "HEX"), ("0b11111111", "BIN"), ("0o377", "OCT") })
                if (sample.TryParse<int>(out int r))
                    sb.AppendLine($"  \"{sample}\" ({label}) → {r}");
            return sb.ToString();
        });

        private void OnParseFloat(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
            $"{ViewHelper.Line($"\"{Input.Text.Trim()}\".ToFloat()")}\n{Input.Text.Trim().ToFloat().Analyze()}");

        private void OnParseDecimal(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            decimal v = Input.Text.Trim().ToDecimal();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"\"{Input.Text.Trim()}\".ToDecimal()"));
            sb.AppendLine(v.Decompose().ToString());
            sb.AppendLine();
            decimal a = "1.000000000000000000000000001".ToDecimal();
            decimal b = "0.999999999999999999999999999".ToDecimal();
            sb.AppendLine($"  [정밀도]  a - b = {a - b:G}");
            return sb.ToString();
        });

        private void OnHexToBytes(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            byte[] b = Input.Text.Trim().ToBytes();
            return $"{ViewHelper.Line($"\"{Input.Text.Trim()}\".ToBytes()")}\n  [{string.Join(", ", b.Select(x => $"0x{x:X2}"))}]\n  {b.ToHexString()}\n  BIN: {b.ToBinString(" ")}";
        });

        private void OnAnalyze(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            float v = Input.Text.ToFloat();
            var info = v.Analyze();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("f.Analyze() — IEEE 754 분석"));
            sb.AppendLine(info.ToString());
            sb.AppendLine();
            sb.AppendLine("  [특수값]");
            foreach (float fv in new[] { 0f, 1f, -1f, float.MaxValue, float.PositiveInfinity, float.NaN })
            {
                var i2 = fv.Analyze();
                sb.AppendLine($"  {fv.ToString("G9"),-20} {i2.Hex}  NaN={i2.IsNaN}  Inf={i2.IsInfinity}");
            }
            return sb.ToString();
        });

        private void OnTryParse(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            string txt = Input.Text.Trim();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("TryParse<T> / ParseOr<T>"));
            bool ok1 = txt.TryParse<int>(out int r1); sb.AppendLine($"  TryParse<int>    → ok={ok1}  val={r1}");
            bool ok2 = txt.TryParse<float>(out float r2); sb.AppendLine($"  TryParse<float>  → ok={ok2}  val={r2:G9}");
            bool ok3 = txt.TryParse<decimal>(out decimal r3); sb.AppendLine($"  TryParse<decimal>→ ok={ok3}  val={r3:G}");
            sb.AppendLine();
            sb.AppendLine($"  \"invalid\".ParseOr<int>(-1)     = {"invalid".ParseOr<int>(-1)}");
            sb.AppendLine($"  \"0xFF\".ParseOr<int>(0)        = {"0xFF".ParseOr<int>(0)}");
            sb.AppendLine($"  \"bad\".ParseOr<decimal>(0m)    = {"bad".ParseOr<decimal>(0m)}m");
            return sb.ToString();
        });

        private void OnJson(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var data = new[]
            {
                new SensorRecord(1,"온도센서-A",25.3f,60.1f,1234.56m),
                new SensorRecord(2,"온도센서-B",22.7f,55.4f,789.00m),
            };
            string json = data.ToJson();
            var back = json.FromJson<SensorRecord[]>();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("ToJson / FromJson — decimal 정밀 유지"));
            sb.AppendLine(json[..Math.Min(350, json.Length)]);
            sb.AppendLine($"\n  역직렬화: {back?.Length}개  Price[0]={back?[0].Price:G}m");
            return sb.ToString();
        });

        private void OnCsv(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var data = new[]{
                new SensorRecord(1,"온도",25.3f,60.1f,1234.56m),
                new SensorRecord(2,"습도",22.7f,55.4f,789.00m),
            };
            string csv = data.ToCsv();
            var back = csv.FromCsv<SensorRecord>();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("ToCsv / FromCsv — decimal 정밀 유지"));
            sb.AppendLine(csv.TrimEnd());
            sb.AppendLine($"\n  복원: {back.Count}개");
            foreach (var r in back)
                sb.AppendLine($"  [{r.Id}] {r.Name,-8}  Temp={r.Temp:F1}  Price={r.Price:G}m");
            return sb.ToString();
        });

        private void OnXml(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var obj = new SensorRecord(1, "테스트", 25.3f, 60.1f, 999.99m);
            string xml = obj.ToXml();
            var back = xml.FromXml<SensorRecord>();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("ToXml / FromXml"));
            sb.AppendLine(xml[..Math.Min(350, xml.Length)]);
            sb.AppendLine($"\n  역직렬화: Id={back?.Id}  Name={back?.Name}  Price={back?.Price:G}m");
            return sb.ToString();
        });

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}
