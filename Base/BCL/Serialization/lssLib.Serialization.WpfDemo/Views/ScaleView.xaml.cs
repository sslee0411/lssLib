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
//  ScaleView — 선형 변환 + SmoothStep + Hysteresis
// ════════════════════════════════════════════════════════════════
namespace lssLib.Serialization.WpfDemo.Views
{
    /// <summary>
    /// ScaleView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScaleView : UserControl
    {
        public ScaleView() => InitializeComponent();


        private (double x, double imin, double imax, double omin, double omax) P()

            => (double.Parse(XBox.Text), double.Parse(InMin.Text),

                double.Parse(InMax.Text), double.Parse(OutMin.Text), double.Parse(OutMax.Text));

        // ── 선형 변환 ────────────────────────────────────────────
        private void OnMap(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (x, imin, imax, omin, omax) = P();
            bool clamp = ClampCheck.IsChecked == true;
            double r = x.MapTo(imin, imax, omin, omax, clamp);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("x.MapTo — 선형 변환"));
            sb.AppendLine($"  {x}.MapTo({imin},{imax} → {omin},{omax}, clamp:{clamp}) = {r:G12}");
            sb.AppendLine();
            sb.AppendLine("  [ADC 12bit → 전압 예시]");
            foreach (int adc in new[] { 0, 512, 1024, 2048, 3000, 4095 })
                sb.AppendLine($"  ADC {adc,4} → {adc.MapTo(0, 4095, 0.0, 3.3):F4} V");
            return sb.ToString();
        });

        private void OnMapDetail(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (x, imin, imax, omin, omax) = P();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("MapDetail — 모드별 비교"));
            foreach (var mode in new[] { ScaleMode.Extend, ScaleMode.Clamp, ScaleMode.Wrap })
                sb.AppendLine($"  [{mode}] {x.MapDetail(imin, imax, omin, omax, mode)}");
            return sb.ToString();
        });

        private void OnNorm(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (x, imin, imax, omin, omax) = P();
            double n = x.Normalize(imin, imax);
            double dn = n.Denormalize(omin, omax);
            decimal nm = ((decimal)x).Normalize((decimal)imin, (decimal)imax);
            return $"{ViewHelper.Line("Normalize / Denormalize")}\n  {x}.Normalize({imin},{imax}) = {n:G12}\n  {n:G6}.Denormalize({omin},{omax}) = {dn:G12}\n  decimal: {(decimal)x}m.Normalize = {nm:G}m";
        });

        private void OnLerp(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (t, from, to, _, _) = P();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"Lerp / InverseLerp  (from={from}, to={to})"));
            foreach (double tv in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
                sb.AppendLine($"  t={tv:F2} → Lerp = {tv.Lerp(from, to):G10}");
            double v = t.Lerp(from, to);
            sb.AppendLine($"\n  Lerp(t={t}) = {v:G10}");
            sb.AppendLine($"  InverseLerp({v:G6}) = {v.InverseLerp(from, to):G10}");
            return sb.ToString();
        });

        private void OnClamp(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (x, imin, imax, _, _) = P();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("Clamp"));
            foreach (double v in new[] { imin - 1, imin, x, imax, imax + 1 })
                sb.AppendLine($"  {v,8:G6}.Clamp({imin},{imax}) = {v.Clamp(imin, imax):G10}");
            sb.AppendLine($"\n  1500m.Clamp(0m,1000m) = {1500m.Clamp(0m, 1000m)}m");
            return sb.ToString();
        });

        private void OnDeadZone(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("DeadZone — 조이스틱 드리프트 보정 (deadZone=0.1)"));
            foreach (double v in new[] { -1.0, -0.5, -0.09, 0.0, 0.09, 0.5, 1.0 })
                sb.AppendLine($"  x={v,6:F2} → {v.DeadZone(0.1),8:F4}{(Math.Abs(v) < 0.1 ? "  ← dead zone" : "")}");
            return sb.ToString();
        });

        private void OnPiecewise(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            double[] bps = { 0, 100, 500, 1000, 4095 };
            double[] vals = { -10, 0, 25, 85, 125 };
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("Piecewise — 비선형 NTC 온도 센서 보정"));
            sb.AppendLine("  breakpoints  = [0, 100, 500, 1000, 4095]");
            sb.AppendLine("  outputValues = [-10, 0, 25, 85, 125] °C");
            sb.AppendLine();
            foreach (int adc in new[] { 0, 50, 250, 750, 2048, 4000, 4095 })
                sb.AppendLine($"  ADC {adc,4} → {((double)adc).Piecewise(bps, vals),7:F2} °C");
            return sb.ToString();
        });

        private void OnDecimalScale(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("decimal MapTo / Piecewise — 금융 계산"));
            sb.AppendLine("  [환율 인덱스] 1200~1400원 → 0~100");
            foreach (decimal won in new[] { 1200m, 1250m, 1300m, 1350m, 1400m })
                sb.AppendLine($"  {won}원 → {won.MapTo(1200m, 1400m, 0m, 100m):F1}");
            sb.AppendLine();
            decimal[] limits = { 0m, 1000m, 5000m, 10000m };
            decimal[] fees = { 0m, 10m, 50m, 80m };
            sb.AppendLine("  [구간별 수수료]");
            foreach (decimal amt in new[] { 0m, 500m, 3000m, 7000m })
                sb.AppendLine($"  거래 {amt,6}원 → 수수료 {amt.Piecewise(limits, fees):F2}원");
            sb.AppendLine();
            sb.AppendLine($"  0.5m.Lerp(0m,1000m)        = {0.5m.Lerp(0m, 1000m)}m");
            sb.AppendLine($"  500m.InverseLerp(0m,1000m) = {500m.InverseLerp(0m, 1000m)}");
            return sb.ToString();
        });

        // ── SmoothStep ────────────────────────────────────────────
        private void OnSmoothStep(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (t, from, to, _, _) = P();
            double smooth = t.SmoothStep(from, to);
            double linear = t.Lerp(from, to);
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"SmoothStep — 3차식 3t²-2t³  (t={t:G4}, from={from}, to={to})"));
            sb.AppendLine($"  SmoothStep = {smooth:G12}");
            sb.AppendLine($"  Lerp(선형)  = {linear:G12}");
            sb.AppendLine($"  차이        = {smooth - linear:G8}");
            sb.AppendLine();
            sb.AppendLine("  [t=0~1 전체 비교]");
            sb.AppendLine($"  {"t",6}  {"Lerp",12}  {"SmoothStep",14}  {"SmootherStep",14}");
            for (double tv = 0; tv <= 1.001; tv += 0.1)
            {
                double l = tv.Lerp(from, to);
                double ss = tv.SmoothStep(from, to);
                double ms = tv.SmootherStep(from, to);
                sb.AppendLine($"  {tv,6:F2}  {l,12:G8}  {ss,14:G8}  {ms,14:G8}");
            }
            return sb.ToString();
        });

        private void OnSmootherStep(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var (t, from, to, _, _) = P();
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"SmootherStep — 5차식 6t⁵-15t⁴+10t³  (t={t:G4})"));
            sb.AppendLine($"  SmoothStep   = {t.SmoothStep(from, to):G12}  (3t²-2t³)");
            sb.AppendLine($"  SmootherStep = {t.SmootherStep(from, to):G12}  (6t⁵-15t⁴+10t³)");
            sb.AppendLine();
            sb.AppendLine("  [t=0.2 에서 차이 비교]");
            sb.AppendLine($"  0.2.Lerp(0,100)         = {0.2.Lerp(0, 100):F4}");
            sb.AppendLine($"  0.2.SmoothStep(0,100)   = {0.2.SmoothStep(0, 100):F4}  ← 더 느리게 시작");
            sb.AppendLine($"  0.2.SmootherStep(0,100) = {0.2.SmootherStep(0, 100):F4}  ← 훨씬 더 느리게");
            return sb.ToString();
        });

        private void OnSmoothCompare(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("Lerp vs SmoothStep vs SmootherStep 전체 비교 (0→100)"));
            sb.AppendLine($"  {"t",6}  {"Lerp",8}  {"Smooth",10}  {"Smoother",10}  {"Δ(S-L)",8}  {"Δ(SS-L)",8}");
            for (double t = 0; t <= 1.001; t += 0.1)
            {
                double l = t.Lerp(0, 100);
                double ss = t.SmoothStep(0, 100);
                double ms = t.SmootherStep(0, 100);
                sb.AppendLine($"  {t,6:F2}  {l,8:F3}  {ss,10:F3}  {ms,10:F3}  {ss - l,8:F3}  {ms - l,8:F3}");
            }
            return sb.ToString();
        });

        private void OnFadeSim(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("LED 페이드 시뮬레이션 (밝기 0→255, 20단계)"));
            sb.AppendLine($"  {"t",5}  {"선형",8}  {"SmoothStep",12}  {"SmootherStep",12}  {"시각화(Smooth)"}");
            for (double t = 0; t <= 1.001; t += 0.05)
            {
                int lin = (int)t.Lerp(0, 255);
                int sm = (int)t.SmoothStep(0, 255);
                int sms = (int)t.SmootherStep(0, 255);
                string bar = new string('█', sm * 20 / 255);
                sb.AppendLine($"  {t,5:F2}  {lin,8}  {sm,12}  {sms,12}  {bar}");
            }
            return sb.ToString();
        });

        // ── Hysteresis ────────────────────────────────────────────
        private void OnHysteresis(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            double low = double.Parse(HystLow.Text);
            double high = double.Parse(HystHigh.Text);
            double[] temps = { 55, 60, 63, 66, 64, 62, 59, 58, 67, 61 };
            bool alarm = false;
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"Hysteresis (ref) — 온도 경보 (low={low}°C, high={high}°C)"));
            sb.AppendLine($"  규칙: T > {high}°C → ON  |  T < {low}°C → OFF  |  그 사이 → 유지");
            sb.AppendLine();
            sb.AppendLine($"  {"T(°C)",8}  {"alarm",8}  {"변화",10}");
            bool prev = alarm;
            foreach (double t in temps)
            {
                prev = alarm;
                alarm = t.Hysteresis(ref alarm, low, high);
                string change = alarm != prev ? (alarm ? "✅ ON" : "🔴 OFF") : "  유지";
                string zone = t > high ? "(high 초과)" : t < low ? "(low  미만)" : $"(히스테리시스 {low}~{high})";
                sb.AppendLine($"  {t,8:F1}  {alarm,8}  {change,-8}  {zone}");
            }
            return sb.ToString();
        });

        private void OnHysteresisFloat(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            float low = float.Parse(HystLow.Text);
            float high = float.Parse(HystHigh.Text);
            float[] humidities = { 15f, 22f, 28f, 31f, 29f, 25f, 19f, 18f };
            bool fanOn = false;
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line($"Hysteresis (float) — 팬 제어 (low={low}%, high={high}%)"));
            sb.AppendLine($"  규칙: 습도 > {high}% → 팬 ON  |  < {low}% → 팬 OFF");
            sb.AppendLine();
            foreach (float h in humidities)
            {
                bool prev = fanOn;
                fanOn = h.Hysteresis(ref fanOn, low, high);
                string ch = fanOn != prev ? (fanOn ? "⬆ ON" : "⬇ OFF") : "유지";
                sb.AppendLine($"  습도={h,5:F1}%  팬={fanOn,-5}  {ch}");
            }
            return sb.ToString();
        });

        private void OnHysteresisPure(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            double low = double.Parse(HystLow.Text);
            double high = double.Parse(HystHigh.Text);
            double[] readings = { 55, 63, 66, 62, 59 };

            // 순수 함수 버전 — LINQ Aggregate 로 사용 가능
            bool finalState = readings.Aggregate(false,
                (state, v) => v.HysteresisPure(state, low, high));

            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("HysteresisPure — 순수 함수 (LINQ 호환)"));
            sb.AppendLine($"  입력: [{string.Join(", ", readings)}]");
            sb.AppendLine($"  low={low}  high={high}");
            sb.AppendLine();
            sb.AppendLine("  LINQ Aggregate 방식:");
            sb.AppendLine($"  readings.Aggregate(false, (s,v) => v.HysteresisPure(s, {low}, {high}))");
            sb.AppendLine($"  최종 상태: {finalState}");
            sb.AppendLine();
            // 각 단계 표시
            sb.AppendLine("  [단계별 확인]");
            bool st = false;
            foreach (double v in readings)
            {
                st = v.HysteresisPure(st, low, high);
                sb.AppendLine($"    v={v}°C → {st}");
            }
            return sb.ToString();
        });

        private void OnExceptions(object s, RoutedEventArgs e) => ViewHelper.Run(Out, () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(ViewHelper.Line("예외 테스트"));
            RunEx(sb, "inMax==inMin → DivideByZeroException", () => 5.0.MapTo(3, 3, 0, 100));
            RunEx(sb, "NaN 입력    → ArgumentException", () => double.NaN.MapTo(0, 100, 0, 1));
            RunEx(sb, "Infinity    → ArgumentException", () => double.PositiveInfinity.MapTo(0, 100, 0, 1));
            RunEx(sb, "Hyst low>=high → ArgumentException", () => { bool st = false; 50.0.Hysteresis(ref st, 70.0, 60.0); });
            sb.AppendLine();
            sb.AppendLine($"  역범위 정상 처리: 25.0.MapTo(100→0, 0→100) = {25.0.MapTo(100, 0, 0, 100):F2}");
            return sb.ToString();
        });

        private static void RunEx(StringBuilder sb, string label, Action fn)
        {
            try { fn(); sb.AppendLine($"  {label}: 예외 없음"); }
            catch (Exception ex) { sb.AppendLine($"  {label}:\n    [{ex.GetType().Name}] {ex.Message[..Math.Min(60, ex.Message.Length)]}"); }
        }

        private void OnClear(object s, RoutedEventArgs e) => ViewHelper.Clear(Out);
    }
}
