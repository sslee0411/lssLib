using lssLib.Retry;

namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  RetryPolicyDemo — RetryPolicy 값 객체 예제
//  §1 프리셋  §2 커스텀  §3 지수백오프  §4 재사용패턴
// ═══════════════════════════════════════════════════════════════════

internal static class RetryPolicyDemo
{
    public static void Run()
    {
        DemoHelper.Header("RetryPolicy — Retry 설정 값 객체");
        PresetsDemo();
        CustomDemo();
        BackoffDemo();
        ReuseDemo();
    }

    static void PresetsDemo()
    {
        DemoHelper.Section("§1  사전 정의 프리셋");
        var presets = new (string name, RetryPolicy p)[]
        {
            ("Default",   RetryPolicy.Default),
            ("Http",      RetryPolicy.Http),
            ("Database",  RetryPolicy.Database),
            ("Immediate", RetryPolicy.Immediate),
        };
        foreach (var (name, p) in presets)
        {
            DemoHelper.Show($"{name,-12} MaxAttempts", p.MaxAttempts);
            DemoHelper.Show($"{name,-12} Delay", p.Delay?.ToString() ?? "null → 200ms 적용");
            DemoHelper.Show($"{name,-12} Backoff", p.Backoff);
            Console.WriteLine();
        }
    }

    static void CustomDemo()
    {
        DemoHelper.Section("§2  커스텀 정책 정의");

        var iot = new RetryPolicy(
            MaxAttempts: 10,
            Delay: TimeSpan.FromMilliseconds(100),
            Backoff: false,
            OnRetry: (ex, n) => DemoHelper.Warn($"  재시도 {n}/10: {ex.Message}")
        );
        DemoHelper.Show("IoT.MaxAttempts", iot.MaxAttempts);
        DemoHelper.Show("IoT.Delay", iot.Delay);
        DemoHelper.Show("IoT.Backoff", iot.Backoff);

        // with 식 파생
        var verbose = RetryPolicy.Http with
        {
            OnRetry = (ex, n) => DemoHelper.Warn($"  Http재시도 {n}: {ex.GetType().Name}")
        };
        DemoHelper.Show("Http + OnRetry Backoff", verbose.Backoff);
        DemoHelper.Ok("with 식으로 파생 정책 생성 완료");
    }

    static void BackoffDemo()
    {
        DemoHelper.Section("§3  지수 백오프 대기 시간 계산");

        // 지수 백오프 공식: Delay × 2^attempt
        static long CalcBackoff(double baseMs, int attempt)
            => (long)(baseMs * Math.Pow(2, attempt));

        DemoHelper.Info("Http 정책 (Delay=500ms, Backoff=true):");
        double httpBase = RetryPolicy.Http.Delay?.TotalMilliseconds ?? 200;
        for (int i = 0; i < RetryPolicy.Http.MaxAttempts - 1; i++)
            DemoHelper.Show($"  실패 {i + 1}회 후 대기", $"{CalcBackoff(httpBase, i)} ms");

        DemoHelper.Info("Database 정책 (Delay=1000ms, Backoff=true):");
        double dbBase = RetryPolicy.Database.Delay?.TotalMilliseconds ?? 200;
        for (int i = 0; i < RetryPolicy.Database.MaxAttempts - 1; i++)
            DemoHelper.Show($"  실패 {i + 1}회 후 대기", $"{CalcBackoff(dbBase, i)} ms");

        DemoHelper.Info("Default 정책 (Delay=200ms, Backoff=false) — 고정 간격:");
        double defBase = RetryPolicy.Default.Delay?.TotalMilliseconds ?? 200;
        for (int i = 0; i < RetryPolicy.Default.MaxAttempts - 1; i++)
            DemoHelper.Show($"  실패 {i + 1}회 후 대기", $"{defBase:0} ms  (고정)");
    }

    static void ReuseDemo()
    {
        DemoHelper.Section("§4  정책 재사용 패턴");
        DemoHelper.Info("같은 정책을 여러 호출에 재사용:");
        DemoHelper.Info("  Func<Task> connect = () => port.OpenAsync();");
        DemoHelper.Info("  await connect.RetryAsync(IotPolicy, ct);");
        DemoHelper.Info("  Func<Task<byte[]>> read = () => port.ReadAsync();");
        DemoHelper.Info("  await read.RetryAsync(IotPolicy, ct);");
        DemoHelper.Info("");
        DemoHelper.Info("with 식으로 로깅 추가:");
        DemoHelper.Info("  var p = RetryPolicy.Http with { OnRetry = (ex,n) => Log(n) };");
    }
}