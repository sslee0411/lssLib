// ====================================================================
//  lssLib.Extensions — ScaleExtensions
//  double / float / int / decimal 확장 메서드
//
//  [핵심 수식]
//  result = (x - inMin) × (outMax - outMin) / (inMax - inMin) + outMin
//
//  [지원 타입]
//  double  — 기본 (가장 넓은 범위)
//  float   — 임베디드 센서, 단정밀도 충분할 때
//  int     — ADC 원시값 → 정수 범위 변환
//  decimal — 금융/회계 (28~29자리 정밀도, 내부 double 변환)
//
//  [예외 처리]
//  inMax==inMin → DivideByZeroException
//  NaN/Infinity → ArgumentException
//  역범위(inMin>inMax) → 정상 처리 (음수 기울기)
//  clamp=true → 결과를 출력 범위로 제한
// ====================================================================

namespace lssLib.Extensions
{
    /// <summary>
    /// 선형 비율 변환 확장 메서드 (double / float / int / decimal).
    /// 모든 핵심 기능이 확장 메서드로 제공됩니다.
    /// </summary>
    public static class ScaleExtensions
    {
        // ────────────────────────────────────────────────────────────
        //  MapTo — 핵심 선형 변환
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// [inMin, inMax] → [outMin, outMax] 선형 변환.
        /// <para>수식: result = (x - inMin) × (outMax - outMin) / (inMax - inMin) + outMin</para>
        /// <para>clamp=true 이면 결과를 [min(outMin,outMax), max(outMin,outMax)] 로 제한합니다.</para>
        /// <example><code>
        /// // ADC 12비트(0~4095) → 전압(0.0~3.3V)
        /// double volt = 2048.0.MapTo(0, 4095, 0.0, 3.3);      // ≈ 1.65V
        ///
        /// // 온도 센서 ADC → °C
        /// double temp = 2500.0.MapTo(0, 4095, -40.0, 125.0);  // ≈ 35.6°C
        ///
        /// // clamp: 범위 초과 방지
        /// double safe = 5000.0.MapTo(0, 4095, 0.0, 3.3, clamp:true);  // → 3.3V
        ///
        /// // 역방향 매핑 (inMin > inMax → 음수 기울기)
        /// double rev  = 25.0.MapTo(100, 0, 0.0, 100.0);  // → 75.0
        ///
        /// // 예외 처리
        /// // inMax==inMin → DivideByZeroException
        /// // NaN 입력    → ArgumentException
        /// // Infinity    → ArgumentException
        /// </code></example>
        /// </summary>
        public static double MapTo(this double x,
            double inMin, double inMax, double outMin, double outMax, bool clamp = false)
        {
            Validate(x, inMin, inMax);
            double r = (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
            return clamp ? r.Clamp(Math.Min(outMin, outMax), Math.Max(outMin, outMax)) : r;
        }

        /// <summary>
        /// float MapTo. 임베디드 센서·단정밀도 변환에 사용합니다.
        /// <example><code>
        /// float volt = 512f.MapTo(0f, 1023f, 0f, 3.3f);   // ≈ 1.65f
        /// float pwm  = 50f.MapTo(0f, 100f, 0f, 255f);     // 127.5f
        /// </code></example>
        /// </summary>
        public static float MapTo(this float x,
            float inMin, float inMax, float outMin, float outMax, bool clamp = false)
            => (float)((double)x).MapTo(inMin, inMax, outMin, outMax, clamp);

        /// <summary>
        /// int MapTo → double 결과. ADC 원시값 처리에 유용합니다.
        /// <example><code>
        /// double volt = 2048.MapTo(0, 4095, 0.0, 3.3);   // ≈ 1.65V
        /// double temp = 3000.MapTo(0, 4095, -40.0, 125.0);
        /// </code></example>
        /// </summary>
        public static double MapTo(this int x,
            int inMin, int inMax, double outMin, double outMax, bool clamp = false)
            => ((double)x).MapTo(inMin, inMax, outMin, outMax, clamp);

        /// <summary>
        /// decimal MapTo — 금융/정밀 계산.
        /// 내부적으로 double 변환 후 계산, 결과를 decimal 로 반환합니다.
        /// <para>주의: decimal의 최대 정밀도(28~29자리)보다 double 변환 오차가 발생할 수 있습니다.
        /// 정밀도가 중요한 경우 직접 산술 연산을 권장합니다.</para>
        /// <example><code>
        /// // 환율 변환 (1200~1400원 범위 → 0~100 지수)
        /// decimal idx = 1300m.MapTo(1200m, 1400m, 0m, 100m);   // 50m
        ///
        /// // 주식 가격 정규화
        /// decimal norm = 85000m.MapTo(50000m, 100000m, 0m, 1m);  // 0.7m
        ///
        /// // 수수료 구간 (clamp)
        /// decimal fee = 1500m.MapTo(0m, 1000m, 0m, 10m, clamp:true);  // → 10m
        /// </code></example>
        /// </summary>
        public static decimal MapTo(this decimal x,
            decimal inMin, decimal inMax, decimal outMin, decimal outMax, bool clamp = false)
            => (decimal)((double)x).MapTo((double)inMin, (double)inMax, (double)outMin, (double)outMax, clamp);

        /// <summary>
             /// 모든 숫자 타입(ushort, int 등)을 위한 범용 MapTo 확장 메서드.
            /// 내부적으로 double로 변환하여 계산합니다.
        /// </summary>
        public static double MapTo<T>(this T x, double inMin, double inMax, double outMin, double outMax, bool clamp = false)
            where T : struct, IComparable, IFormattable, IConvertible
        {
            // 입력값을 double로 변환하여 기존 double MapTo를 호출합니다.
            double val = Convert.ToDouble(x);
            return val.MapTo(inMin, inMax, outMin, outMax, clamp);
        }

        // ────────────────────────────────────────────────────────────
        //  MapDetail — 상세 결과 (Clamped/Wrapped 여부 포함)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 변환 결과와 함께 클램프·순환 여부를 ScaleResult 로 반환합니다.
        /// <example><code>
        /// var r = 5000.0.MapDetail(0, 4095, 0.0, 3.3, ScaleMode.Clamp);
        /// Console.WriteLine(r);
        /// // Map(5000) → 3.3 [클램프]  클램프 (5000→4095)
        ///
        /// r.Output   // 3.3
        /// r.Clamped  // true
        /// r.IsNormal // false
        ///
        /// // Wrap 모드
        /// var r2 = 5000.0.MapDetail(0, 1000, 0.0, 10.0, ScaleMode.Wrap);
        /// r2.Wrapped  // true
        /// </code></example>
        /// </summary>
        public static ScaleResult MapDetail(this double x,
            double inMin, double inMax, double outMin, double outMax,
            ScaleMode mode = ScaleMode.Extend)
        {
            Validate(x, inMin, inMax);
            double lo = Math.Min(inMin, inMax), hi = Math.Max(inMin, inMax);
            bool clamped = false, wrapped = false;
            double adj = mode switch
            {
                ScaleMode.Clamp when x < lo || x > hi => (clamped = true, Math.Clamp(x, lo, hi)).Item2,
                ScaleMode.Wrap when x < lo || x > hi =>
                    (wrapped = true, ((x - lo) % (hi - lo) + (hi - lo)) % (hi - lo) + lo).Item2,
                _ => x
            };
            double result = (adj - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
            return new ScaleResult(x, result, clamped, wrapped,
                clamped ? $"클램프 ({x}→{adj})" : wrapped ? $"순환 ({x}→{adj})" : "정상 범위");
        }

        // ────────────────────────────────────────────────────────────
        //  Normalize / Denormalize
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 값을 [0.0, 1.0] 범위로 정규화합니다.
        /// <example><code>
        /// double n1 = 512.0.Normalize(0, 1023);    // ≈ 0.500489
        /// double n2 = 2048.0.Normalize(0, 4095);   // ≈ 0.5
        ///
        /// // clamp: 범위 밖 입력 방지
        /// double n3 = 5000.0.Normalize(0, 4095, clamp:true);  // 1.0
        /// </code></example>
        /// </summary>
        public static double Normalize(this double x, double min, double max, bool clamp = false)
            => x.MapTo(min, max, 0.0, 1.0, clamp);

        /// <summary>float Normalize.</summary>
        public static float Normalize(this float x, float min, float max, bool clamp = false)
            => x.MapTo(min, max, 0f, 1f, clamp);

        /// <summary>
        /// decimal 정규화 → decimal (0~1 범위).
        /// <example><code>
        /// decimal n = 500m.Normalize(0m, 1000m);  // 0.5m
        /// </code></example>
        /// </summary>
        public static decimal Normalize(this decimal x, decimal min, decimal max, bool clamp = false)
            => x.MapTo(min, max, 0m, 1m, clamp);

        /// <summary>
        /// [0.0,1.0] → [min, max] 역정규화.
        /// <example><code>
        /// double v = 0.5.Denormalize(0, 1023);   // ≈ 511.5
        /// double v = 0.75.Denormalize(0.0, 3.3); // ≈ 2.475V
        /// </code></example>
        /// </summary>
        public static double Denormalize(this double t, double min, double max)
            => t.MapTo(0.0, 1.0, min, max);

        /// <summary>float 역정규화.</summary>
        public static float Denormalize(this float t, float min, float max)
            => t.MapTo(0f, 1f, min, max);

        /// <summary>
        /// decimal 역정규화.
        /// <example><code>
        /// decimal v = 0.5m.Denormalize(0m, 1000m);  // 500m
        /// </code></example>
        /// </summary>
        public static decimal Denormalize(this decimal t, decimal min, decimal max)
            => t.MapTo(0m, 1m, min, max);

        // ────────────────────────────────────────────────────────────
        //  Lerp / InverseLerp
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 선형 보간. t=0 → from, t=1 → to.
        /// <example><code>
        /// double v  = 0.75.Lerp(from:0, to:100);   // 75.0
        /// float  vf = 0.5f.Lerp(from:0f, to:255f); // 127.5f
        ///
        /// // 애니메이션 진행도 계산
        /// double progress = 0.0;
        /// double pos = progress.Lerp(startX, endX);
        ///
        /// // t 범위 밖도 허용 (외삽)
        /// double v2 = 1.5.Lerp(0, 100);  // 150.0 (범위 밖 외삽)
        /// </code></example>
        /// </summary>
        public static double Lerp(this double t, double from, double to)
        {
            if (double.IsNaN(t) || double.IsInfinity(t))
                throw new ArgumentException($"t 유효하지 않음: {t}");
            return from + (to - from) * t;
        }

        /// <summary>float 선형 보간.</summary>
        public static float Lerp(this float t, float from, float to)
            => (float)((double)t).Lerp(from, to);

        /// <summary>
        /// decimal 선형 보간.
        /// <example><code>
        /// decimal mid = 0.5m.Lerp(from:0m, to:1000m);  // 500m
        /// </code></example>
        /// </summary>
        public static decimal Lerp(this decimal t, decimal from, decimal to)
            => from + (to - from) * t;

        /// <summary>
        /// 역 선형 보간. v가 [from, to] 구간의 어느 위치인지 0~1 로 반환.
        /// <example><code>
        /// double t1 = 75.0.InverseLerp(0, 100);    // 0.75
        /// double t2 = 1.65.InverseLerp(0.0, 3.3);  // ≈ 0.5 (1.65V 정규화)
        ///
        /// // 게이지 표시에 활용
        /// double percent = currentValue.InverseLerp(minValue, maxValue) * 100;
        /// </code></example>
        /// </summary>
        public static double InverseLerp(this double v, double from, double to)
        {
            if (Math.Abs(to - from) < double.Epsilon)
                throw new DivideByZeroException($"from({from})==to({to})");
            return (v - from) / (to - from);
        }

        /// <summary>float 역 선형 보간.</summary>
        public static float InverseLerp(this float v, float from, float to)
            => (float)((double)v).InverseLerp(from, to);

        /// <summary>
        /// decimal 역 선형 보간.
        /// <example><code>
        /// decimal t = 750m.InverseLerp(0m, 1000m);  // 0.75m
        /// </code></example>
        /// </summary>
        public static decimal InverseLerp(this decimal v, decimal from, decimal to)
        {
            if (to == from) throw new DivideByZeroException($"from({from})==to({to})");
            return (v - from) / (to - from);
        }

        // ────────────────────────────────────────────────────────────
        //  Clamp - 값을 특정 범위로 제한
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 값을 [min, max] 범위로 제한합니다.
        /// <example><code>
        /// double v1 = 1.5.Clamp(0.0, 1.0);   // 1.0 (최대값으로 제한)
        /// double v2 = -0.5.Clamp(0.0, 1.0);  // 0.0 (최소값으로 제한)
        /// double v3 = 0.5.Clamp(0.0, 1.0);   // 0.5 (범위 내)
        ///
        /// // PWM 듀티 사이클 0~100% 제한
        /// double duty = rawValue.Clamp(0.0, 100.0);
        /// </code></example>
        /// </summary>
        public static double Clamp(this double x, double min, double max)
        {
            if (min > max) throw new ArgumentException($"min({min})>max({max})");
            return Math.Max(min, Math.Min(max, x));
        }

        /// <summary>float Clamp.</summary>
        public static float Clamp(this float x, float min, float max)
            => (float)((double)x).Clamp(min, max);

        /// <summary>int Clamp.</summary>
        public static int Clamp(this int x, int min, int max)
            => Math.Max(min, Math.Min(max, x));

        /// <summary>
        /// decimal Clamp.
        /// <example><code>
        /// decimal v = 1500m.Clamp(0m, 1000m);  // 1000m
        /// decimal d = 0.005m.Clamp(0m, 1m);    // 0.005m (범위 내)
        /// </code></example>
        /// </summary>
        public static decimal Clamp(this decimal x, decimal min, decimal max)
        {
            if (min > max) throw new ArgumentException($"min({min})>max({max})");
            return x < min ? min : x > max ? max : x;
        }

        // ────────────────────────────────────────────────────────────
        //  DeadZone - 입력 무효 영역
        //  데드존 은 입력값이 특정 범위 안에 있을 때 의도적으로 반응하지 않도록(0으로 처리하도록) 설정한 무효 영역을 뜻
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Dead Zone 적용. |x| < deadZone 이면 0, 그 외는 범위 재매핑.
        /// 조이스틱 드리프트 보정, 모터 최소 제어값 설정에 사용합니다.
        /// <example><code>
        /// // 조이스틱 x 값 -1.0~1.0, 드리프트 ±0.1 제거
        /// double out1 = 0.05.DeadZone(deadZone:0.1);   // 0.0  (dead zone)
        /// double out2 = 0.5.DeadZone(deadZone:0.1);    // ≈ 0.44 (재매핑)
        /// double out3 = 1.0.DeadZone(deadZone:0.1);    // 1.0
        /// double out4 = (-0.08).DeadZone(deadZone:0.1);// 0.0  (dead zone)
        ///
        /// // 최소 제어값 10% 적용 (0~100)
        /// double pwm = joyValue.DeadZone(deadZone:0.1, maxInput:1.0).MapTo(0,1, 0,100);
        /// </code></example>
        /// </summary>
        public static double DeadZone(this double x, double deadZone, double maxInput = 1.0)
        {
            if (deadZone < 0) throw new ArgumentException($"deadZone({deadZone})<0");
            if (Math.Abs(x) < deadZone) return 0.0;
            double sign = x > 0 ? 1.0 : -1.0;
            return sign * Math.Abs(x).MapTo(deadZone, maxInput, 0.0, maxInput);
        }

        /// <summary>float DeadZone.</summary>
        public static float DeadZone(this float x, float deadZone, float maxInput = 1f)
            => (float)((double)x).DeadZone(deadZone, maxInput);

        // ────────────────────────────────────────────────────────────
        //  Piecewise — 구간별 비선형 매핑
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 구간별 선형 매핑. 비선형 센서 보정, 구간별 수수료 등에 사용합니다.
        /// breakpoints 는 오름차순이어야 하며 outputValues 와 같은 길이여야 합니다.
        /// <example><code>
        /// // 비선형 NTC 온도 센서 보정
        /// double[] bps  = { 0, 100, 500, 1000, 4095 };
        /// double[] vals = { -10, 0, 25, 85, 125 };
        /// double temp = 750.0.Piecewise(bps, vals);  // ≈ 47.5°C
        ///
        /// // 구간별 수수료 (누진세 등)
        /// double[] limits = { 0, 1000, 5000, 10000 };
        /// double[] fees   = { 0, 10,   50,   80    };
        /// double fee = 3000.0.Piecewise(limits, fees);
        ///
        /// // 범위 밖 처리: 최소/최대 고정
        /// double v1 = (-100.0).Piecewise(bps, vals);  // -10 (최소)
        /// double v2 = 5000.0  .Piecewise(bps, vals);  // 125 (최대)
        /// </code></example>
        /// </summary>
        public static double Piecewise(this double x, double[] breakpoints, double[] outputValues)
        {
            if (breakpoints.Length != outputValues.Length)
                throw new ArgumentException("breakpoints/outputValues 길이 불일치");
            if (breakpoints.Length < 2) throw new ArgumentException("최소 2개 구간 필요");
            if (x <= breakpoints[0]) return outputValues[0];
            if (x >= breakpoints[^1]) return outputValues[^1];
            for (int i = 0; i < breakpoints.Length - 1; i++)
                if (x >= breakpoints[i] && x < breakpoints[i + 1])
                    return x.MapTo(breakpoints[i], breakpoints[i + 1], outputValues[i], outputValues[i + 1]);
            return outputValues[^1];
        }

        /// <summary>
        /// decimal Piecewise — 금융 구간 계산 등에 사용합니다.
        /// <example><code>
        /// decimal[] limits = { 0m, 1000m, 5000m, 10000m };
        /// decimal[] fees   = { 0m, 10m,   50m,   80m    };
        /// decimal fee = 3000m.Piecewise(limits, fees);
        /// </code></example>
        /// </summary>
        public static decimal Piecewise(this decimal x, decimal[] breakpoints, decimal[] outputValues)
        {
            if (breakpoints.Length != outputValues.Length)
                throw new ArgumentException("breakpoints/outputValues 길이 불일치");
            if (breakpoints.Length < 2) throw new ArgumentException("최소 2개 구간 필요");
            if (x <= breakpoints[0]) return outputValues[0];
            if (x >= breakpoints[^1]) return outputValues[^1];
            for (int i = 0; i < breakpoints.Length - 1; i++)
                if (x >= breakpoints[i] && x < breakpoints[i + 1])
                    return x.MapTo(breakpoints[i], breakpoints[i + 1], outputValues[i], outputValues[i + 1]);
            return outputValues[^1];
        }

        // ────────────────────────────────────────────────────────────
        //  Wrap - 값을 특정 범위로 순환 (모듈로 연산)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 값을 [min, max] 범위 내에서 순환시킵니다.
        /// <example><code>
        /// // 360도 회전각 순환
        /// double angle = 370.0.Wrap(0, 360);   // 10.0
        /// double angle = (-10.0).Wrap(0, 360); // 350.0
        ///
        /// // 배열 인덱스 순환
        /// double idx = 7.0.Wrap(0, 5);  // 2.0
        /// </code></example>
        /// </summary>
        public static double Wrap(this double x, double min, double max)
        {
            double span = max - min;
            if (span <= 0) throw new ArgumentException($"min({min})>=max({max})");
            return ((x - min) % span + span) % span + min;
        }

        // ────────────────────────────────────────────────────────────
        //  Threshold -- 임계값 비교 (ON/OFF, 디지털 변환 등)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 임계값 이상이면 high, 미만이면 low 를 반환합니다.
        /// <example><code>
        /// // 온도 경보 (65°C 이상 = 경보)
        /// double alarm = temp.Threshold(65.0, low:0, high:1);
        ///
        /// // 아날로그 → 디지털 변환 (0.5 기준)
        /// double bit = 0.7.Threshold(0.5);  // 1.0
        /// double bit2= 0.3.Threshold(0.5);  // 0.0
        /// </code></example>
        /// </summary>
        public static double Threshold(this double x, double threshold,
            double low = 0.0, double high = 1.0)
            => x >= threshold ? high : low;

        // ── 내부 유효성 검사 ──────────────────────────────────────────
        // ────────────────────────────────────────────────────────────
        //  SmoothStep / SmootherStep — S커브 비선형 보간
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// SmoothStep 보간 (3차식: 3t²-2t³). t=[0,1] 범위에서 부드럽게 가속/감속.
        /// <para>시작·끝에서 미분값이 0 → 충격 없는 부드러운 전환.</para>
        /// <para>게임 애니메이션, UI 전환 효과, LED 페이드, 센서 필터링에 사용합니다.</para>
        /// <example><code>
        /// // t=0~1 사이에서 0→100 부드럽게 이동
        /// double p1 = 0.2.SmoothStep(from:0, to:100);   // ≈ 10.4 (가속 구간)
        /// double p2 = 0.5.SmoothStep(from:0, to:100);   // 50.0  (중간)
        /// double p3 = 0.8.SmoothStep(from:0, to:100);   // ≈ 89.6 (감속 구간)
        ///
        /// // LED 페이드 인/아웃 (선형보다 자연스러움)
        /// for (double t = 0; t <= 1.0; t += 0.05)
        ///     SetBrightness((byte)t.SmoothStep(0, 255));
        ///
        /// // ADC 값 부드럽게 표시 (노이즈 감소)
        /// double norm = currentAdc.Normalize(adcMin, adcMax, clamp:true);
        /// double disp = norm.SmoothStep(displayMin, displayMax);
        ///
        /// // SmoothStep vs Lerp 비교 (t=0.2)
        /// double lin = 0.2.Lerp(0, 100);         // 20.0  (선형)
        /// double sms = 0.2.SmoothStep(0, 100);   // ≈ 10.4 (느리게 시작)
        /// </code></example>
        /// </summary>
        public static double SmoothStep(this double t, double from, double to)
        {
            double tc = Math.Clamp(t, 0.0, 1.0);
            double s = tc * tc * (3.0 - 2.0 * tc);   // 3t²-2t³
            return from + (to - from) * s;
        }

        /// <summary>float SmoothStep (3차식).</summary>
        public static float SmoothStep(this float t, float from, float to)
            => (float)((double)t).SmoothStep(from, to);

        /// <summary>
        /// SmootherStep 보간 (5차식: 6t⁵-15t⁴+10t³). SmoothStep 보다 더 부드러운 전환.
        /// <para>2차 미분도 0이어서 가속도 변화까지 자연스럽습니다.</para>
        /// <example><code>
        /// // SmootherStep vs SmoothStep 비교 (t=0.2)
        /// double s1 = 0.2.SmoothStep(0, 100);    // ≈ 10.4
        /// double s2 = 0.2.SmootherStep(0, 100);  // ≈  5.7 (더 느리게 시작)
        ///
        /// // 전체 비교
        /// for (double t = 0; t <= 1.0; t += 0.1)
        ///     Console.WriteLine($"t={t:F1}  Lerp={t.Lerp(0,100):F1}  Smooth={t.SmoothStep(0,100):F1}  Smoother={t.SmootherStep(0,100):F1}");
        /// </code></example>
        /// </summary>
        public static double SmootherStep(this double t, double from, double to)
        {
            double tc = Math.Clamp(t, 0.0, 1.0);
            double s = tc * tc * tc * (tc * (tc * 6.0 - 15.0) + 10.0);  // 6t⁵-15t⁴+10t³
            return from + (to - from) * s;
        }

        /// <summary>float SmootherStep (5차식).</summary>
        public static float SmootherStep(this float t, float from, float to)
            => (float)((double)t).SmootherStep(from, to);

        // ────────────────────────────────────────────────────────────
        // Hysteresis — 히스테리시스 (노이즈 방지 임계값 전환)
        // [설명]
        // 입력값 x가 임계값을 넘을 때 상태를 변경(true/false)해야 한다면, 올라갈 때의 기준점과 내려올 때의 기준점을 다르게 잡는 것
        // ON 기준: x > (임계값 + 여유값)
        // OFF 기준: x<(임계값 - 여유값)
        // [사용이유]
        // 노이즈 제거: 센서값이 0.01 단위로 흔들려도 결과(ON/OFF)는 흔들리지 않음
        // 기기 보호: 모터나 릴레이 같은 장치가 너무 자주 작동해서 수명이 줄어드는 것을 막음
        // 안정적인 제어: 사용자에게 훨씬 부드럽고 일관된 피드백
        // ────────────────────────────────────────────────────────────
        /// <summary>
        /// 히스테리시스 적용 (ref 기반). 값이 상하 두 임계값을 벗어날 때만 상태가 바뀝니다.
        /// <para><b>동작 규칙</b>: x &gt; high → true, x < low → false, 그 사이 → 이전 상태 유지.</para>
        /// <para>센서 노이즈로 인한 잦은 ON/OFF 스위칭을 방지합니다.</para>
        /// <example><code>
        /// // 온도 경보 — 65°C 이상이면 켜고, 60°C 미만이면 끔 (±2.5°C 히스테리시스)
        /// bool alarm = false;
        /// foreach (double temp in sensorReadings)
        /// {
        ///     alarm = temp.Hysteresis(ref alarm, low:60.0, high:65.0);
        ///     Console.WriteLine($"T={temp:F1}°C  alarm={alarm}");
        /// }
        /// // T=58.0°C  alarm=False  ← low 미만, 꺼짐
        /// // T=63.0°C  alarm=False  ← 히스테리시스 구간, 상태 유지
        /// // T=66.0°C  alarm=True   ← high 초과, 켜짐
        /// // T=62.0°C  alarm=True   ← 히스테리시스 구간, 상태 유지
        /// // T=59.0°C  alarm=False  ← low 미만, 꺼짐
        ///
        /// // 팬 제어 (습도 기준)
        /// bool fanOn = false;
        /// fanOn = humidity.Hysteresis(ref fanOn, low:20.0, high:30.0);
        /// </code></example>
        /// </summary>
        /// <param name="x">현재 측정값.</param>
        /// <param name="prevState">이전 상태 (ref — 내부 갱신됨).</param>
        /// <param name="low">하한 임계값. x 가 이 값 미만이면 false.</param>
        /// <param name="high">상한 임계값. x 가 이 값 초과면 true.</param>
        public static bool Hysteresis(this double x, ref bool prevState, double low, double high)
        {
            if (low >= high)
                throw new ArgumentException($"low({low}) >= high({high})");
            if (x > high) prevState = true;
            if (x < low) prevState = false;
            return prevState;
        }

        /// <summary>
        /// float 히스테리시스 (ref 기반).
        /// <example><code>
        /// bool fanOn = false;
        /// fanOn = ((float)humidity).Hysteresis(ref fanOn, 20.0f, 30.0f);
        /// </code></example>
        /// </summary>
        public static bool Hysteresis(this float x, ref bool prevState, float low, float high)
        {
            double d = x;
            return d.Hysteresis(ref prevState, low, high);
        }

        /// <summary>
        /// 순수 함수 버전 히스테리시스 (ref 없음). LINQ / 람다 / Aggregate 와 호환됩니다.
        /// <example><code>
        /// // LINQ Aggregate 로 최종 상태 계산
        /// bool finalState = readings.Aggregate(false,
        ///     (state, v) => v.HysteresisPure(state, low:60.0, high:65.0));
        ///
        /// // 시뮬레이션 — 각 단계 추적
        /// bool st = false;
        /// foreach (double v in new[]{58.0, 63.0, 66.0, 62.0, 59.0})
        ///     st = v.HysteresisPure(st, 60.0, 65.0);
        /// </code></example>
        /// </summary>
        public static bool HysteresisPure(this double x, bool prevState, double low, double high)
        {
            if (low >= high) throw new ArgumentException($"low({low}) >= high({high})");
            if (x > high) return true;
            if (x < low) return false;
            return prevState;
        }

        // ── 내부 유효성 검사 ──────────────────────────────────────────
        private static void Validate(double x, double inMin, double inMax)
        {
            if (double.IsNaN(x)) throw new ArgumentException($"x=NaN");
            if (double.IsInfinity(x)) throw new ArgumentException($"x={x} (Infinity)");
            if (double.IsNaN(inMin) || double.IsNaN(inMax))
                throw new ArgumentException("inMin/inMax=NaN");
            if (Math.Abs(inMax - inMin) < double.Epsilon)
                throw new DivideByZeroException($"inMax({inMax})==inMin({inMin}): 나눗셈 불가");
        }
    }

    // ── 보조 타입 ─────────────────────────────────────────────────────

    /// <summary>ScaleMode — 범위 초과 처리 방식.</summary>
    public enum ScaleMode
    {
        /// <summary>외삽 (기본) — 범위 밖도 수식 그대로 계산.</summary>
        Extend,
        /// <summary>클램프 — 출력 범위로 제한.</summary>
        Clamp,
        /// <summary>순환 — 입력 범위 내에서 반복.</summary>
        Wrap,
    }

    /// <summary>MapDetail 결과 레코드.</summary>
    public record ScaleResult(
        double Input, double Output,
        bool Clamped, bool Wrapped, string Message = "")
    {
        /// <summary>클램프·순환 없이 정상 변환되었으면 true.</summary>
        public bool IsNormal => !Clamped && !Wrapped;

        /// <summary>"Map(Input) → Output [상태]" 형식 문자열.</summary>
        public override string ToString()
            => $"Map({Input:G6}) → {Output:G6}{(Clamped ? " [클램프]" : "")}  {Message}";
    }
}