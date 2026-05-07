namespace lssLib.Retry.Demo;

// ═══════════════════════════════════════════════════════════════════
//  DemoHelper — 예제 공통 출력 유틸리티
// ═══════════════════════════════════════════════════════════════════

internal static class DemoHelper
{
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ── {title} ──────────────────────────────");
        Console.ResetColor();
    }

    public static void Show(string label, object? value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {label,-32} ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value?.ToString() ?? "<null>");
        Console.ResetColor();
    }

    public static void Ok(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {msg}");
        Console.ResetColor();
    }

    public static void Warn(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ⚠ {msg}");
        Console.ResetColor();
    }

    public static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  → {msg}");
        Console.ResetColor();
    }

    public static void Err(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {msg}");
        Console.ResetColor();
    }

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

    public static async Task TryCatchAsync(string label, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [{label}] {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
        }
    }

    // 비동기를 동기로 실행 (데모 단순화)
    public static void RunAsync(Func<Task> action)
        => action().GetAwaiter().GetResult();

    public static long MeasureMs(Action action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        action();
        return sw.ElapsedMilliseconds;
    }
}