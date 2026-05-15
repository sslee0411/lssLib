namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  DemoHelper — 예제 공통 출력 유틸리티
// ═══════════════════════════════════════════════════════════════════

internal static class DemoHelper
{
    // ── 섹션 구분선
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ── {title} ──────────────────────────────");
        Console.ResetColor();
    }

    // ── 라벨 + 값 출력
    public static void Show(string label, object? value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {label,-32} ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value?.ToString() ?? "<null>");
        Console.ResetColor();
    }

    // ── 성공 (녹색 ✓)
    public static void Ok(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {msg}");
        Console.ResetColor();
    }

    // ── 경고 (노란색 ⚠)
    public static void Warn(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ⚠ {msg}");
        Console.ResetColor();
    }

    // ── 정보 (청록색 →)
    public static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  → {msg}");
        Console.ResetColor();
    }

    // ── 화면 상단 헤더
    public static void Header(string title)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"""

  ══════════════════════════════════════════════
    {title}
  ══════════════════════════════════════════════
""");
        Console.ResetColor();
    }

    // ── 예외를 잡아 출력 (실패 케이스 시연용)
    public static void TryCatch(string label, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [{label}] {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
        }
    }
}