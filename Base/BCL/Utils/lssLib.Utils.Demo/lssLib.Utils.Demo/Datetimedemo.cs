using lssLib.Utils;

namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  DateTimeDemo — DateTimeExtensions 예제
//  §1 표준포맷  §2 Unix Epoch  §3 날짜경계
//  §4 상대시간  §5 TimeSpan    §6 안전파싱
// ═══════════════════════════════════════════════════════════════════

internal static class DateTimeDemo
{
    public static void Run()
    {
        DemoHelper.Header("DateTimeExtensions — 날짜/시간 포맷 · 변환 · 범위");
        FormatDemo();
        EpochDemo();
        BoundaryDemo();
        RelativeDemo();
        TimeSpanDemo();
        ParseDemo();
    }

    // ─────────────────────────────────────────────
    // §1  표준 포맷 문자열
    // ─────────────────────────────────────────────
    static void FormatDemo()
    {
        DemoHelper.Section("§1  표준 포맷 문자열 (InvariantCulture 고정)");

        var now = DateTime.Now;
        DemoHelper.Show("ToIsoDate()", now.ToIsoDate());
        DemoHelper.Show("ToTimeString()", now.ToTimeString());
        DemoHelper.Show("ToIsoDateTime()", now.ToIsoDateTime());
        DemoHelper.Show("ToIso8601Utc()", now.ToIso8601Utc());
        DemoHelper.Show("ToFileStamp()", now.ToFileStamp());
        DemoHelper.Show("ToMsStamp()", now.ToMsStamp());

        DemoHelper.Info("── 파일명 자동 생성 ──");
        DemoHelper.Show("세션 덤프", $"session_{now.ToFileStamp()}.bin");
        DemoHelper.Show("로그 파일", $"app_{now.ToIsoDate()}.log");
        DemoHelper.Show("정밀 스냅샷", $"snap_{now.ToMsStamp()}");

        DemoHelper.Info("── REST API 타임스탬프 ──");
        DemoHelper.Show("timestamp (Unix ms)", DateTime.UtcNow.ToUnixMilliseconds());
        DemoHelper.Show("created_at (ISO)", DateTime.UtcNow.ToIso8601Utc());
    }

    // ─────────────────────────────────────────────
    // §2  Unix Epoch 양방향 변환
    // ─────────────────────────────────────────────
    static void EpochDemo()
    {
        DemoHelper.Section("§2  Unix Epoch 양방향 변환");

        long nowSec = DateTime.UtcNow.ToUnixSeconds();
        long nowMs = DateTime.UtcNow.ToUnixMilliseconds();
        DemoHelper.Show("ToUnixSeconds()", nowSec);
        DemoHelper.Show("ToUnixMilliseconds()", nowMs);

        DateTime fromSec = nowSec.FromUnixSeconds();
        DateTime fromMs = nowMs.FromUnixMilliseconds();
        DemoHelper.Show("FromUnixSeconds() 복원", fromSec.ToIsoDateTime());
        DemoHelper.Show("FromUnixMilliseconds() 복원", fromMs.ToIsoDateTime());
        DemoHelper.Show("왕복 일치", fromSec.ToUnixSeconds() == nowSec ? "✓ 일치" : "✗ 불일치");

        DemoHelper.Info("── 프레임 헤더 Unix 타임스탬프 파싱 시뮬레이션 ──");
        long sensorTs = DateTime.UtcNow.AddSeconds(-3).ToUnixMilliseconds();
        DateTime sensorTime = sensorTs.FromUnixMilliseconds();
        DemoHelper.Show("센서 Unix ms", sensorTs);
        DemoHelper.Show("변환된 시각", sensorTime.ToIsoDateTime());
        DemoHelper.Show("상대 표시", sensorTime.ToRelativeKo());
    }

    // ─────────────────────────────────────────────
    // §3  날짜 경계 · 범위 판단
    // ─────────────────────────────────────────────
    static void BoundaryDemo()
    {
        DemoHelper.Section("§3  날짜 경계 · 범위 판단");

        var now = DateTime.Now;
        DemoHelper.Show("StartOfDay()", now.StartOfDay().ToIsoDateTime());
        DemoHelper.Show("EndOfDay()", now.EndOfDay().ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        DemoHelper.Show("StartOfWeek()", now.StartOfWeek().ToIsoDate());
        DemoHelper.Show("StartOfMonth()", now.StartOfMonth().ToIsoDate());
        DemoHelper.Show("EndOfMonth()", now.EndOfMonth().ToIsoDate());

        var from = now.AddHours(-1);
        var to = now.AddHours(1);
        DemoHelper.Show("IsBetween(now-1h, now+1h)", now.IsBetween(from, to));
        DemoHelper.Show("IsBetween(경계 밖)", now.AddHours(2).IsBetween(from, to));
        DemoHelper.Show($"IsWeekday() [{now:ddd}]", now.IsWeekday());
        DemoHelper.Show($"IsWeekend() [{now:ddd}]", now.IsWeekend());

        DemoHelper.Info("── 다음 영업일 계산 ──");
        var next = now.Date;
        while (next.IsWeekend()) next = next.AddDays(1);
        DemoHelper.Show("다음 영업일", next.ToIsoDate());
    }

    // ─────────────────────────────────────────────
    // §4  상대 시간 (한국어)
    // ─────────────────────────────────────────────
    static void RelativeDemo()
    {
        DemoHelper.Section("§4  상대 시간 (한국어)");

        var samples = new (string label, DateTime dt)[]
        {
            ("30초 전",  DateTime.Now.AddSeconds(-30)),
            ("5분 전",   DateTime.Now.AddMinutes(-5)),
            ("2시간 전", DateTime.Now.AddHours(-2)),
            ("3일 전",   DateTime.Now.AddDays(-3)),
            ("2주 전",   DateTime.Now.AddDays(-14)),
            ("2개월 전", DateTime.Now.AddMonths(-2)),
        };
        foreach (var (label, dt) in samples)
            DemoHelper.Show(label, dt.ToRelativeKo());

        DemoHelper.Info("── 이벤트 로그 시뮬레이션 ──");
        var events = new[]
        {
            (DateTime.Now.AddSeconds(-45), "CRC 불일치 감지"),
            (DateTime.Now.AddMinutes(-12), "센서 재연결 완료"),
            (DateTime.Now.AddHours(-3),    "버퍼 오버플로 경고"),
            (DateTime.Now.AddDays(-2),     "스키마 업데이트"),
        };
        foreach (var (time, msg) in events)
            DemoHelper.Show($"[{time.ToRelativeKo()}]", msg);
    }

    // ─────────────────────────────────────────────
    // §5  TimeSpan 유틸
    // ─────────────────────────────────────────────
    static void TimeSpanDemo()
    {
        DemoHelper.Section("§5  TimeSpan 유틸");

        var samples = new TimeSpan[]
        {
            TimeSpan.FromSeconds(0.123),
            TimeSpan.FromSeconds(90.5),
            TimeSpan.FromMinutes(5.25),
            TimeSpan.FromHours(1.5),
        };
        foreach (var ts in samples)
        {
            DemoHelper.Show($"{ts.TotalSeconds,8:F2}초  ToDisplay()", ts.ToDisplay());
            DemoHelper.Show($"{ts.TotalSeconds,8:F2}초  ToMs()", ts.ToMs());
            Console.WriteLine();
        }

        DemoHelper.Info("── 처리 시간 측정 시뮬레이션 ──");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(47);
        sw.Stop();
        DemoHelper.Show("경과 (ToDisplay)", sw.Elapsed.ToDisplay());
        DemoHelper.Show("경과 (ToMs)", $"{sw.Elapsed.ToMs()} ms");

        TimeSpan remaining = DateTime.Now.AddSeconds(2) - DateTime.Now;
        DemoHelper.Show("잔여 시간", remaining.ToDisplay());
        DemoHelper.Show("타임아웃 임박", remaining.ToMs() < 3000 ? "임박" : "여유");
    }

    // ─────────────────────────────────────────────
    // §6  안전 파싱
    // ─────────────────────────────────────────────
    static void ParseDemo()
    {
        DemoHelper.Section("§6  안전 파싱");

        DemoHelper.Show("\"2024-04-01 14:30:00\"", "2024-04-01 14:30:00".TryParseDateTime()?.ToIsoDateTime() ?? "<null>");
        DemoHelper.Show("\"2024-04-01\" (커스텀)", "2024-04-01".TryParseDateTime("yyyy-MM-dd")?.ToIsoDate() ?? "<null>");
        DemoHelper.Show("\"invalid\"", "invalid".TryParseDateTime()?.ToIsoDate() ?? "<null>");

        DemoHelper.Info("── TryParseAny — 다양한 입력 포맷 지원 ──");
        string[] inputs = { "2024-04-01", "20240401", "04/01/2024", "2024/04/01", "invalid" };
        string[] formats = { "yyyy-MM-dd", "yyyyMMdd", "MM/dd/yyyy", "yyyy/MM/dd" };
        foreach (var input in inputs)
        {
            var parsed = input.TryParseAny(formats);
            DemoHelper.Show($"입력: \"{input}\"", parsed?.ToIsoDate() ?? "<파싱 실패>");
        }

        DemoHelper.Info("── UI 날짜 필터 파싱 시뮬레이션 ──");
        string userFrom = "20240301";
        string userTo = "2024-04-30";
        DateTime from = userFrom.TryParseAny("yyyy-MM-dd", "yyyyMMdd") ?? DateTime.Today.StartOfMonth();
        DateTime to = userTo.TryParseAny("yyyy-MM-dd", "yyyyMMdd") ?? DateTime.Today.EndOfMonth();
        DemoHelper.Show("from", from.ToIsoDate());
        DemoHelper.Show("to", to.ToIsoDate());
        DemoHelper.Show("기간(일)", (int)(to - from).TotalDays);
    }
}