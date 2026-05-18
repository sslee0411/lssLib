// ══════════════════════════════════════════════════════════════════════
//  lssLib.Sequence.Demo · Program.cs
//  범용 시퀀스 엔진 데모 — 도메인 독립 예제
// ══════════════════════════════════════════════════════════════════════

using lssLib.Sequence;
using lssLib.Sequence.Demo;

Console.WriteLine("lssLib.Sequence — 범용 시퀀스 엔진 데모");
Console.WriteLine("──────────────────────────────────────");
Console.WriteLine("  1  커스텀 스텝 (HttpCallStep) 단일 순차");
Console.WriteLine("  2  커스텀 스텝 Sequential + Parallel 혼합");
Console.WriteLine("  3  컨텍스트 변수 스텝 간 공유");
Console.WriteLine("  4  ContinueOnError 오류 무시 모드");
Console.WriteLine("  5  배치 실행 (RunAllAsync)");
Console.Write("입력 (기본=1): ");

string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)); // 30초 후 자동 취소 ,
                                                                       // 무한 대기 시 `new CancellationTokenSource()` 로 생성

try
{
    await (input switch
    {
        "2" => DemoEx02_MixedGroups.RunAsync(cts.Token),
        "3" => DemoEx03_ContextVariable.RunAsync(cts.Token),
        "4" => DemoEx04_ContinueOnError.RunAsync(cts.Token),
        "5" => DemoEx05_BatchRun.RunAsync(cts.Token),
        _ => DemoEx01_CustomStep.RunAsync(cts.Token)
    });
}
catch (Exception ex) { Console.WriteLine($"오류: {ex.Message}"); }

Console.WriteLine("\n데모 완료");