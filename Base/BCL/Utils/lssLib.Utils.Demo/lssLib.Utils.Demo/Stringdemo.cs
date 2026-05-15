using lssLib.Utils;

namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  StringDemo — StringExtensions 예제
//  §1 HasValue/OrDefault  §2 케이스·포맷변환  §3 포맷예제
//  §4 비교                §5 파싱             §6 인코딩  §7 정규식
// ═══════════════════════════════════════════════════════════════════
internal static class StringDemo
{
    public static void Run()
    {
        DemoHelper.Header("StringExtensions — 문자열 조작 · 변환 · 파싱 · 인코딩");
        HasValueDemo();
        CaseConvertDemo();
        FormatDemo();
        CompareDemo();
        ParseDemo();
        EncodingDemo();
        RegexDemo();
    }

    // ─────────────────────────────────────────────
    // §1  HasValue / OrDefault
    // ─────────────────────────────────────────────
    static void HasValueDemo()
    {
        DemoHelper.Section("§1  HasValue · OrDefault");

        DemoHelper.Show("\"hello\".HasValue()", "hello".HasValue());
        DemoHelper.Show("\"  \".HasValue()", "  ".HasValue());
        DemoHelper.Show("((string?)null).HasValue()", ((string?)null).HasValue());

        DemoHelper.Show("null.OrDefault(\"기본값\")", ((string?)null).OrDefault("기본값"));
        DemoHelper.Show("\"  \".OrDefault(\"기본값\")", "  ".OrDefault("기본값"));
        DemoHelper.Show("\"hello\".OrDefault(\"기본값\")", "hello".OrDefault("기본값"));

        DemoHelper.Info("── 설정 파일 파싱 시뮬레이션 ──");
        var ini = new Dictionary<string, string?>
        {
            ["host"] = "192.168.1.10",
            ["port"] = null,
            ["log_dir"] = "  "
        };
        DemoHelper.Show("host", ini["host"].OrDefault("localhost"));
        DemoHelper.Show("port", ini["port"].OrDefault("8080"));
        DemoHelper.Show("log_dir", ini["log_dir"].OrDefault(@"logs"));
    }

    // ─────────────────────────────────────────────
    // §2  케이스 변환 (GeneratedRegex — 런타임 할당 없음)
    // ─────────────────────────────────────────────
    static void CaseConvertDemo()
    {
        DemoHelper.Section("§2  케이스 변환 (GeneratedRegex 사용)");

        // ToSnakeCase (SnakeCasePattern)
        DemoHelper.Show("\"SensorReading\".ToSnakeCase()", "SensorReading".ToSnakeCase());
        DemoHelper.Show("\"parseHTTPResponse\".ToSnakeCase()", "parseHTTPResponse".ToSnakeCase());
        DemoHelper.Show("\"frameId\".ToSnakeCase()", "frameId".ToSnakeCase());

        // ToCamelCase (CamelCasePattern — 낙타 패턴)
        DemoHelper.Show("\"sensor_reading\".ToCamelCase()", "sensor_reading".ToCamelCase());
        DemoHelper.Show("\"parse-http-body\".ToCamelCase()", "parse-http-body".ToCamelCase());
        DemoHelper.Show("\"FRAME_ID\".ToCamelCase()", "FRAME_ID".ToCamelCase());

        // Capitalize
        DemoHelper.Show("\"hello world\".Capitalize()", "hello world".Capitalize());
        DemoHelper.Show("\"STX frame\".Capitalize()", "STX frame".Capitalize());

        // Truncate
        DemoHelper.Show("\"SensorTemperatureReading\".Truncate(10)", "SensorTemperatureReading".Truncate(10));
        DemoHelper.Show("\"SensorTemperatureReading\".Truncate(10, \"...\")", "SensorTemperatureReading".Truncate(10, "..."));
        DemoHelper.Show("\"Short\".Truncate(10)", "Short".Truncate(10));

        // Repeat / PadLeftTo
        DemoHelper.Show("\"─\".Repeat(40)", "─".Repeat(40));
        DemoHelper.Show("\"42\".PadLeftTo(8, '0')", "42".PadLeftTo(8, '0'));
        DemoHelper.Show("\"1024\".PadLeftTo(8, '0')", "1024".PadLeftTo(8, '0'));
    }

    // ─────────────────────────────────────────────
    // §3  포맷 실용 예제
    // ─────────────────────────────────────────────
    static void FormatDemo()
    {
        DemoHelper.Section("§3  포맷 실용 예제");

        DemoHelper.Info("── 고정폭 프레임 ID 출력 ──");
        for (uint id = 1; id <= 5; id++)
            DemoHelper.Show($"Frame #{id}",
                $"[{id.ToString().PadLeftTo(6, '0')}]  Temp={22.5 + id:F2}°C");

        DemoHelper.Info("── BufSchema 필드명 → JSON 키 변환 ──");
        var fields = new[] { "frame_id", "sensor_temp", "humidity_raw", "crc_checksum" };
        foreach (var f in fields)
            DemoHelper.Show(f, f.ToCamelCase());
    }

    // ─────────────────────────────────────────────
    // §4  검색 · 비교 (대소문자 무시)
    // ─────────────────────────────────────────────
    static void CompareDemo()
    {
        DemoHelper.Section("§4  검색·비교 (대소문자 무시)");

        DemoHelper.Show("ContainsIgnoreCase(\"keep-alive\")", "Connection: Keep-Alive".ContainsIgnoreCase("keep-alive"));
        DemoHelper.Show("StartsWithIgnoreCase(\"http\")", "HTTP/1.1 200 OK".StartsWithIgnoreCase("http"));
        DemoHelper.Show("EndsWithIgnoreCase(\".bin\")", "capture.BIN".EndsWithIgnoreCase(".bin"));
        DemoHelper.Show("EqualsIgnoreCase(\"ok\")", "OK".EqualsIgnoreCase("ok"));

        DemoHelper.Show("\"SUCCESS\".IsAnyOf(ok/success/done)",
            "SUCCESS".IsAnyOf("ok", "success", "done"));
        DemoHelper.Show("\".BIN\".IsAnyOf(.bin/.dat/.dump)",
            ".BIN".IsAnyOf(".bin", ".dat", ".dump"));

        DemoHelper.Info("── 파일 확장자 분기 ──");
        string[] files = { "capture.bin", "report.csv", "schema.json", "frame.DAT" };
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            var kind = ext.IsAnyOf(".bin", ".dat") ? "바이너리"
                     : ext.EqualsIgnoreCase(".csv") ? "CSV"
                     : ext.EqualsIgnoreCase(".json") ? "JSON" : "기타";
            DemoHelper.Show(f, kind);
        }
    }

    // ─────────────────────────────────────────────
    // §5  안전 파싱
    // ─────────────────────────────────────────────
    static void ParseDemo()
    {
        DemoHelper.Section("§5  안전 파싱");

        DemoHelper.Show("\"123\".ToIntOrNull()", "123".ToIntOrNull());
        DemoHelper.Show("\"abc\".ToIntOrNull()", "abc".ToIntOrNull());
        DemoHelper.Show("\"-1\".ToLongOrNull()", "-1".ToLongOrNull());
        DemoHelper.Show("\"3.14\".ToDoubleOrNull()", "3.14".ToDoubleOrNull());
        DemoHelper.Show("\"9.999\".ToDecimalOrNull()", "9.999".ToDecimalOrNull());
        DemoHelper.Show("\"1,234\".ToDoubleOrNull()", "1,234".ToDoubleOrNull());   // null

        DemoHelper.Info("── ToBoolOrNull — 다양한 표현 ──");
        foreach (var s in new[] { "true", "1", "yes", "on", "false", "0", "no", "off", "abc" })
            DemoHelper.Show($"\"{s}\".ToBoolOrNull()", s.ToBoolOrNull()?.ToString() ?? "<null>");

        DemoHelper.Info("── INI 설정 파싱 시나리오 ──");
        var ini = new Dictionary<string, string>
        {
            ["smooth_alpha"] = "0.15",
            ["hyst_low"] = "0.05",
            ["max_frames"] = "1000",
            ["debug_mode"] = "yes",
            ["baud_rate"] = "115200",
            ["bad_value"] = "???",
        };
        DemoHelper.Show("smooth_alpha (float)", (float)(ini["smooth_alpha"].ToDoubleOrNull() ?? 0.2));
        DemoHelper.Show("max_frames   (int)", ini["max_frames"].ToIntOrNull() ?? 500);
        DemoHelper.Show("debug_mode   (bool)", ini["debug_mode"].ToBoolOrNull() ?? false);
        DemoHelper.Show("baud_rate    (int)", ini["baud_rate"].ToIntOrNull() ?? 9600);
        DemoHelper.Show("bad_value    → fallback", ini["bad_value"].ToIntOrNull() ?? -1);
    }

    // ─────────────────────────────────────────────
    // §6  인코딩 · 바이트 변환
    // ─────────────────────────────────────────────
    static void EncodingDemo()
    {
        DemoHelper.Section("§6  인코딩 · 바이트 변환");

        // UTF-8
        byte[] utf8 = "FRAME_HEADER".ToUtf8Bytes();
        DemoHelper.Show("\"FRAME_HEADER\".ToUtf8Bytes() 길이", utf8.Length);
        DemoHelper.Show("bytes.ToUtf8String()", utf8.ToUtf8String());

        // Base64
        string b64 = "lssLib.Utils v2".ToBase64();
        DemoHelper.Show("\"lssLib.Utils v2\".ToBase64()", b64);
        DemoHelper.Show("FromBase64() 복원", b64.FromBase64());
        DemoHelper.Show("\"invalid!!\".FromBase64()", "invalid!!".FromBase64() ?? "<null>");

        // HEX
        byte[] frame = { 0x02, 0xDE, 0xAD, 0xBE, 0xEF, 0x03 };
        DemoHelper.Show("bytes.ToHex()", frame.ToHex());
        DemoHelper.Show("bytes.ToHex(spaced:true)", frame.ToHex(spaced: true));
        DemoHelper.Show("\"DE AD BE EF\".FromHex()", "DE AD BE EF".FromHex().ToHex(spaced: true));
        DemoHelper.TryCatch("\"GG\".FromHex()", () => "GG".FromHex());

        DemoHelper.Info("── 시리얼 HEX 수신 시뮬레이션 ──");
        string hexLine = "02 01 00 00 00 CD CC 4C 42 03";
        byte[] parsed = hexLine.FromHex();
        DemoHelper.Show("수신 HEX", hexLine);
        DemoHelper.Show("FromHex() 바이트 수", parsed.Length);
        DemoHelper.Show("다시 ToHex(spaced)", parsed.ToHex(spaced: true));
    }

    // ─────────────────────────────────────────────
    // §7  정규식 유틸 (GeneratedRegex)
    // ─────────────────────────────────────────────
    static void RegexDemo()
    {
        DemoHelper.Section("§7  정규식 유틸 (GeneratedRegex)");

        DemoHelper.Show("\"12345\".IsDigitsOnly()", "12345".IsDigitsOnly());
        DemoHelper.Show("\"12a45\".IsDigitsOnly()", "12a45".IsDigitsOnly());
        DemoHelper.Show("\"user@example.com\".IsEmail()", "user@example.com".IsEmail());
        DemoHelper.Show("\"not-an-email\".IsEmail()", "not-an-email".IsEmail());
        DemoHelper.Show("IsMatch(날짜패턴)", "2024-04-01".IsMatch(@"^\d{4}-\d{2}-\d{2}$"));
        DemoHelper.Show("IsMatch(FRAME_001)", "FRAME_001".IsMatch(@"^FRAME_\d+$"));

        DemoHelper.Show("version 캡처", "version: 2.0.0".MatchGroup(@"version: (\S+)"));
        DemoHelper.Show("error code", "Error Code: E1042".MatchGroup(@"E(\d+)"));
        DemoHelper.Show("없는 패턴", "no match".MatchGroup(@"(\d+)") ?? "<null>");

        DemoHelper.Info("── 장치 응답 파싱 시뮬레이션 ──");
        string response = "STATUS=READY;TEMP=42.50;CH=3;CRC=AABB1234";
        DemoHelper.Show("원본", response);
        DemoHelper.Show("STATUS", response.MatchGroup(@"STATUS=(\w+)"));
        DemoHelper.Show("TEMP", response.MatchGroup(@"TEMP=([\d.]+)"));
        DemoHelper.Show("CH", response.MatchGroup(@"CH=(\d+)"));
        DemoHelper.Show("CRC", response.MatchGroup(@"CRC=([0-9A-Fa-f]+)"));

        DemoHelper.Info("── ToLines / ToNonEmptyLines ──");
        string multi = "line1\r\nline2\n\nline4\r\nline5";
        DemoHelper.Show("ToLines() 총 줄 수", multi.ToLines().Count());
        DemoHelper.Show("ToNonEmptyLines() 줄 수", multi.ToNonEmptyLines().Count());
    }
}