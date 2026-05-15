// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils.Demo — Program
//  Guard · StringExtensions · DateTimeExtensions · FileExtensions
//  전 기능 대화형 예제 콘솔 앱
// ═══════════════════════════════════════════════════════════════════

using lssLib.Utils.Demo;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "lssLib.Utils Demo";

while (true)
{
    Console.Clear();
    Banner();

    Console.WriteLine("  [1] Guard              — 인수 선행 검증");
    Console.WriteLine("  [2] StringExtensions   — 문자열 조작 · 변환 · 파싱 · 인코딩");
    Console.WriteLine("  [3] DateTimeExtensions — 날짜/시간 포맷 · 변환 · 범위");
    Console.WriteLine("  [4] FileExtensions     — 파일 · 디렉터리 처리");
    Console.WriteLine("  [5] Pipeline           — 4개 모듈 협력 통합 시나리오");
    Console.WriteLine("  [0] 종료");
    Console.Write("\n  선택 > ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1": GuardDemo.Run(); break;
        case "2": StringDemo.Run(); break;
        case "3": DateTimeDemo.Run(); break;
        case "4": FileDemo.Run(); break;
        case "5": PipelineDemo.Run(); break;
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
  ║        lssLib.Utils  Demo  v2.0              ║
  ║  Guard · String · DateTime · File         ║
  ╚══════════════════════════════════════════════╝
""");
    Console.ResetColor();
}