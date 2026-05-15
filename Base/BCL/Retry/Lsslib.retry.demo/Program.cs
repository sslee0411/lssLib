// ═══════════════════════════════════════════════════════════════════
//  lssLib.Retry.Demo — Program
//  UtilResult · RetryPolicy · RetryExtensions
//  CircuitBreaker · RateLimiter · 통합 파이프라인 예제
// ═══════════════════════════════════════════════════════════════════

using lssLib.Retry.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "lssLib.Retry Demo";

while (true)
{
    Console.Clear();
    Banner();

    Console.WriteLine("  [1] UtilResult         — 안전 실행 반환 타입");
    Console.WriteLine("  [2] RetryPolicy        — Retry 설정 값 객체");
    Console.WriteLine("  [3] RetryExtensions    — Retry · Timeout · TryExecute");
    Console.WriteLine("  [4] CircuitBreaker     — 회로 차단기");
    Console.WriteLine("  [5] RateLimiter        — 속도 제한 (슬라이딩 윈도우)");
    Console.WriteLine("  [6] Pipeline           — CB + RL + Retry 3중 보호 시나리오");
    Console.WriteLine("  [0] 종료");
    Console.Write("\n  선택 > ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1": UtilResultDemo.Run(); break;
        case "2": RetryPolicyDemo.Run(); break;
        case "3": RetryDemo.Run(); break;
        case "4": CircuitBreakerDemo.Run(); break;
        case "5": RateLimiterDemo.Run(); break;
        case "6": PipelineDemo.Run(); break;
        case "0": return;
        default: continue;
    }

    Console.WriteLine("\n  [Enter] 메뉴로 돌아가기");
    Console.ReadLine();
}

static void Banner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""

  ╔══════════════════════════════════════════════╗
  ║        lssLib.Retry  Demo  v1.0              ║
  ║  Retry · CircuitBreaker · RateLimiter      ║
  ╚══════════════════════════════════════════════╝
""");
    Console.ResetColor();
}