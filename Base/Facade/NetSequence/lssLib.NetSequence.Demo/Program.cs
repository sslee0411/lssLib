// ══════════════════════════════════════════════════════════════════════
//  lssLib.NetSequence.Demo · Program.cs
//  역할: Net 시퀀스 제어 예제 메뉴 (S1~S7)
// ══════════════════════════════════════════════════════════════════════

using lssLib.NetSequence;
using lssLib.NetSequence.Demo;
using lssLib.Sequence;

Console.WriteLine("lssLib.NetSequence — 시퀀스 제어 데모");
Console.WriteLine("────────────────────────────────────────────────");
Console.WriteLine(" ┌─ 단일 장비 ───────────────────────────────┐");
Console.WriteLine("  S1  단일 장비 순차 Write");
Console.WriteLine("  S2  단일 장비 Request + 응답 검증 + 재시도");
Console.WriteLine(" ├─ 다중 장비 ───────────────────────────────┤");
Console.WriteLine("  S3  다중 장비 순차 연계 (Sequential)");
Console.WriteLine("  S4  다중 장비 병렬 연계 (Parallel)");
Console.WriteLine("  S5  혼합 그룹 (공정 시나리오)");
Console.WriteLine(" ├─ 배치 / 고급 ─────────────────────────────┤");
Console.WriteLine("  S6  배치 실행 (RunAllAsync)");
Console.WriteLine("  S7  Virtual ★ 하드웨어 불필요 — 전체 검증");
Console.WriteLine(" └───────────────────────────────────────────┘");
Console.Write("입력 (기본=S7): ");

string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var controller = new NetSequenceController();
var context = new NetSequenceContext(
    logAction: msg => Console.WriteLine($"  [LOG] {msg}"),
    logErrorAction: msg => Console.WriteLine($"  [ERR] {msg}"));

// 공통 이벤트 등록
controller.SequenceStarted += s => Console.WriteLine($"\n  ▶ [{s.Name}] 시작");
controller.SequenceCompleted += r => Console.WriteLine($"  ■ {r}");
controller.StepCompleted += r =>
{
    string icon = r.IsSuccess ? "✔" : "✘";
    string retry = r.RetryCount > 0 ? $" 재시도={r.RetryCount}" : "";
    string err = !r.IsSuccess ? $" → {r.ErrorMessage}" : "";
    Console.WriteLine($"  [{r.Step.StepIndex:D2}:{r.Step.StepName}] " +
        $"{icon} {r.Elapsed.TotalMilliseconds:F0}ms{retry}{err}");
};

try
{
    await (input switch
    {
        "S1" => SeqEx01_SingleWrite.RunAsync(controller, context, cts.Token),
        "S2" => SeqEx02_Request.RunAsync(controller, context, cts.Token),
        "S3" => SeqEx03_Sequential.RunAsync(controller, context, cts.Token),
        "S4" => SeqEx04_Parallel.RunAsync(controller, context, cts.Token),
        "S5" => SeqEx05_Mixed.RunAsync(controller, context, cts.Token),
        "S6" => SeqEx06_Batch.RunAsync(controller, context, cts.Token),
        _ => SeqEx07_Virtual.RunAsync(cts.Token)
    });
}
catch (OperationCanceledException) { Console.WriteLine("타임아웃"); }
catch (Exception ex) { Console.WriteLine($"오류: {ex.Message}"); }

Console.WriteLine("\n데모 완료");