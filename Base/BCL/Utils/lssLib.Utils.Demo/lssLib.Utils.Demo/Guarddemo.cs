using lssLib.Utils;

namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  GuardDemo — Guard 인수 선행 검증 예제
//  §1 NotNull   §2 NotEmpty / NotWhiteSpace
//  §3 Range     §4 NotNegative / Positive
//  §5 That      §6 체이닝 운용 패턴
// ═══════════════════════════════════════════════════════════════════

internal static class GuardDemo
{
    public static void Run()
    {
        DemoHelper.Header("Guard — 인수 선행 검증");
        NotNullDemo();
        NotEmptyDemo();
        RangeDemo();
        SignDemo();
        ThatDemo();
        ChainDemo();
    }

    // ─────────────────────────────────────────────
    // §1  NotNull
    // ─────────────────────────────────────────────
    static void NotNullDemo()
    {
        DemoHelper.Section("§1  NotNull");

        // 참조 타입 통과
        string? name = "lssLib";
        string result = Guard.NotNull(name);
        DemoHelper.Ok($"NotNull(\"lssLib\") → \"{result}\"");

        // Nullable<int> 언박싱 통과
        int? maybeId = 42;
        int id = Guard.NotNull(maybeId);
        DemoHelper.Ok($"NotNull(42) → {id}  (int? → int 언박싱)");

        // 실패 — 참조 null
        DemoHelper.TryCatch("NotNull(null)", () =>
        {
            string? missing = null;
            Guard.NotNull(missing);           // "missing"이 메시지에 자동 포함
        });

        // 실패 — Nullable 값 없음
        DemoHelper.TryCatch("NotNull(null?)", () =>
        {
            int? emptyId = null;
            Guard.NotNull(emptyId);
        });

        // 생성자 체이닝 패턴 안내
        DemoHelper.Info("생성자 패턴:");
        DemoHelper.Info("  _logger = Guard.NotNull(logger);");
        DemoHelper.Info("  _schema = Guard.NotNull(schema);");
    }

    // ─────────────────────────────────────────────
    // §2  NotEmpty / NotWhiteSpace
    // ─────────────────────────────────────────────
    static void NotEmptyDemo()
    {
        DemoHelper.Section("§2  NotEmpty · NotWhiteSpace");

        // 문자열 통과
        string code = Guard.NotEmpty("SENSOR_01");
        DemoHelper.Ok($"NotEmpty(\"SENSOR_01\") → \"{code}\"");

        // NotEmpty 는 공백 허용
        Guard.NotEmpty("  ");
        DemoHelper.Ok("NotEmpty(\"  \") → 통과  (공백은 허용)");

        // 문자열 실패 — 빈 문자열
        DemoHelper.TryCatch("NotEmpty(\"\")", () => Guard.NotEmpty(""));

        // 문자열 실패 — null
        DemoHelper.TryCatch("NotEmpty(null)", () =>
        {
            string? s = null;
            Guard.NotEmpty(s);
        });

        // NotWhiteSpace — 공백 거부
        DemoHelper.TryCatch("NotWhiteSpace(\"  \")", () => Guard.NotWhiteSpace("  "));
        DemoHelper.Info("NotWhiteSpace: null · \"\" · 공백 모두 거부");

        // 배열 통과
        byte[] frame = Guard.NotEmpty(new byte[] { 0x02, 0xAA, 0x03 });
        DemoHelper.Ok($"NotEmpty(byte[3]) → 길이={frame.Length}  (BufferParser 전달 가능)");

        // 배열 실패
        DemoHelper.TryCatch("NotEmpty(byte[0])", () =>
            Guard.NotEmpty(Array.Empty<byte>()));
    }

    // ─────────────────────────────────────────────
    // §3  Range
    // ─────────────────────────────────────────────
    static void RangeDemo()
    {
        DemoHelper.Section("§3  Range<T>");

        // int 범위
        int offset = Guard.Range(128, 0, 255);
        DemoHelper.Ok($"Range(128, 0, 255) → {offset}");

        // 경계값 통과
        DemoHelper.Ok($"Range(0,   0, 255) → {Guard.Range(0, 0, 255)}  (최솟값)");
        DemoHelper.Ok($"Range(255, 0, 255) → {Guard.Range(255, 0, 255)}  (최댓값)");

        // 실패 — 초과
        DemoHelper.TryCatch("Range(256, 0, 255)", () => Guard.Range(256, 0, 255));

        // float 범위 (SmoothStep alpha)
        float alpha = Guard.Range(0.15f, 0.0f, 1.0f);
        DemoHelper.Ok($"Range(0.15f, 0f, 1f) → {alpha}  (SmoothStep alpha)");
        DemoHelper.TryCatch("Range(1.5f, 0f, 1f)", () => Guard.Range(1.5f, 0.0f, 1.0f));

        // DateTime 범위
        var today = DateTime.Today;
        var valid = Guard.Range(today, today.AddYears(-1), today);
        DemoHelper.Ok($"Range(DateTime.Today, -1yr, today) → {valid:yyyy-MM-dd}");
    }

    // ─────────────────────────────────────────────
    // §4  NotNegative / Positive
    // ─────────────────────────────────────────────
    static void SignDemo()
    {
        DemoHelper.Section("§4  NotNegative · Positive");

        // NotNegative: 0 허용, 음수 거부
        DemoHelper.Ok($"NotNegative(0)  → {Guard.NotNegative(0)}  (0 허용)");
        DemoHelper.Ok($"NotNegative(42) → {Guard.NotNegative(42)}");
        DemoHelper.TryCatch("NotNegative(-1)", () => Guard.NotNegative(-1));

        // Positive: 0도 거부, 양수만 허용
        DemoHelper.Ok($"Positive(1024)  → {Guard.Positive(1024)}");
        DemoHelper.TryCatch("Positive(0)", () => Guard.Positive(0));
        DemoHelper.TryCatch("Positive(-1)", () => Guard.Positive(-1));

        // 운용 패턴
        DemoHelper.Info("── 설정값 검증 패턴 ──");
        int bufSize = Guard.Positive(4096);
        int maxRetry = Guard.NotNegative(3);
        double timeout = Guard.Positive(5.0);
        DemoHelper.Show("bufSize  = Positive(4096)", bufSize);
        DemoHelper.Show("maxRetry = NotNegative(3)", maxRetry);
        DemoHelper.Show("timeout  = Positive(5.0)", timeout);
    }

    // ─────────────────────────────────────────────
    // §5  That
    // ─────────────────────────────────────────────
    static void ThatDemo()
    {
        DemoHelper.Section("§5  That (조건 검증)");

        // 통과
        int length = 8;
        Guard.That(length >= 4);
        DemoHelper.Ok("That(length >= 4) → 통과");

        // 실패 — 조건식이 메시지에 자동 포함
        DemoHelper.TryCatch("That(false)", () =>
            Guard.That(length > 100));

        // 실패 — 명시적 메시지
        DemoHelper.TryCatch("That + message", () =>
        {
            int frameLen = 6;
            int schemaLen = 12;
            Guard.That(frameLen == schemaLen,
                $"프레임 크기 불일치: 수신={frameLen}, 예상={schemaLen}");
        });

        // STX/ETX 헤더 검증 패턴
        byte[] testFrame = { 0x02, 0xAA, 0xBB, 0x03 };
        Guard.That(testFrame[0] == 0x02, "STX 없음");
        Guard.That(testFrame[^1] == 0x03, "ETX 없음");
        DemoHelper.Ok("STX/ETX 헤더 검증 → 통과");

        // CRC 검증 패턴
        uint rxCrc = 0xAABBCCDD;
        uint calcCrc = 0xAABBCCDD;
        Guard.That(rxCrc == calcCrc,
            $"CRC 불일치: 수신={rxCrc:X8}, 계산={calcCrc:X8}");
        DemoHelper.Ok("CRC 검증 → 통과");
    }

    // ─────────────────────────────────────────────
    // §6  체이닝 운용 패턴
    // ─────────────────────────────────────────────
    static void ChainDemo()
    {
        DemoHelper.Section("§6  체이닝 운용 패턴");

        // 반환값으로 즉시 사용
        string host = Guard.NotWhiteSpace("  192.168.0.1  ".Trim());
        DemoHelper.Ok($"NotWhiteSpace(host.Trim()) → \"{host}\"");

        // 복합 Guard 체인
        byte[] raw = { 0x02, 0x01, 0x00, 0x00, 0x00, 0x03 };
        byte[] valid = Guard.NotEmpty(raw);
        Guard.That(valid[0] == 0x02, "STX 없음");
        Guard.That(valid.Length >= 4, "최소 프레임 길이 미달");
        DemoHelper.Ok($"복합 체인 → 길이={valid.Length}, STX=0x{valid[0]:X2}");

        // FileExtensions EnsureDirSelf 체이닝
        string tmpDir = Path.Combine(Path.GetTempPath(), "guard_chain_test");
        string outDir = Guard.NotWhiteSpace(tmpDir).EnsureDirSelf();
        DemoHelper.Ok($"NotWhiteSpace(dir).EnsureDirSelf() → \"{Path.GetFileName(outDir)}\" 생성");
        Directory.Delete(tmpDir);
    }
}