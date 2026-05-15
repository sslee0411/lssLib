using lssLib.Utils;

namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  PipelineDemo — Guard + StringExt + DateTimeExt + FileExt 협력 시나리오
//
//  시나리오 1: INI 설정 파일 로드 + 완전 검증
//  시나리오 2: 센서 프레임 수신 시뮬레이션 (파싱 · 로그 · 저장)
//  시나리오 3: 세션 파일 아카이브 + 롤링 정리
// ═══════════════════════════════════════════════════════════════════

internal static class PipelineDemo
{
    static readonly string WorkDir = Path.Combine(Path.GetTempPath(), "lssLib_pipeline");

    public static void Run()
    {
        DemoHelper.Header("Pipeline — 4개 모듈 협력 시나리오");
        WorkDir.EnsureDirSelf();

        Scenario1_ConfigLoad();
        Scenario2_SensorPipeline();
        Scenario3_ArchiveAndCleanup();

        Directory.Delete(WorkDir, recursive: true);
        DemoHelper.Info("작업 디렉터리 정리 완료");
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 1 — INI 설정 파일 로드 + 완전 검증
    // ═══════════════════════════════════════════════════════════════
    static void Scenario1_ConfigLoad()
    {
        DemoHelper.Section("시나리오 1 — INI 설정 파일 로드 + 검증");

        // INI 파일 생성 (FileExtensions.WriteText)
        string cfgPath = Path.Combine(WorkDir, "config", "demo.ini");
        cfgPath.WriteText("""
            # lssLib.Utils.Demo 설정
            port_name    = COM3
            baud_rate    = 115200
            output_dir   = output/frames
            log_dir      = logs
            smooth_alpha = 0.15
            hyst_low     = 0.05
            hyst_high    = 0.95
            max_frames   = 1000
            debug_mode   = yes
            auto_save    = true
            """);
        DemoHelper.Ok($"INI 파일 생성: {cfgPath.GetFileName()} ({cfgPath.GetSizeDisplay()})");

        // Guard 파일 검증
        Guard.NotWhiteSpace(cfgPath);
        Guard.That(cfgPath.FileExists(), $"설정 파일 없음: {cfgPath}");

        // StringExtensions — INI 파싱
        var ini = cfgPath.ReadText()
            .ToNonEmptyLines()
            .Where(l => !l.StartsWithIgnoreCase("#") && l.ContainsIgnoreCase("="))
            .ToDictionary(
                l => l.MatchGroup(@"^\s*(\w+)\s*=")!.Trim(),
                l => l.MatchGroup(@"=\s*(.+)$")!.Trim()
            );
        DemoHelper.Show("파싱된 키 수", ini.Count);

        // Guard 범위 검증
        string portName = Guard.NotWhiteSpace(ini.GetValueOrDefault("port_name").OrDefault());
        int baudRate = Guard.Range(ini.GetValueOrDefault("baud_rate").ToIntOrNull() ?? 9600, 1200, 921600);
        float alpha = Guard.Range((float)(ini.GetValueOrDefault("smooth_alpha").ToDoubleOrNull() ?? 0.2f), 0f, 1f);
        float hystLow = Guard.Positive((float)(ini.GetValueOrDefault("hyst_low").ToDoubleOrNull() ?? 0.05f));
        float hystHigh = Guard.Range((float)(ini.GetValueOrDefault("hyst_high").ToDoubleOrNull() ?? 0.95f), hystLow, 1f);
        int maxFrames = Guard.Positive(ini.GetValueOrDefault("max_frames").ToIntOrNull() ?? 500);
        bool debug = ini.GetValueOrDefault("debug_mode").ToBoolOrNull() ?? false;

        // 출력 디렉터리 보장
        string outDir = Guard.NotWhiteSpace(ini.GetValueOrDefault("output_dir").OrDefault("output"))
            .Replace("/", Path.DirectorySeparatorChar.ToString());
        Path.Combine(WorkDir, outDir).EnsureDirSelf();

        DemoHelper.Info("── 로드된 설정값 ──");
        DemoHelper.Show("port_name", portName);
        DemoHelper.Show("baud_rate", baudRate);
        DemoHelper.Show("smooth_alpha", alpha);
        DemoHelper.Show("hyst_low", hystLow);
        DemoHelper.Show("hyst_high", hystHigh);
        DemoHelper.Show("max_frames", maxFrames);
        DemoHelper.Show("debug_mode", debug);
        DemoHelper.Ok("모든 설정값 Guard 검증 통과");

        // 잘못된 값 시뮬레이션
        DemoHelper.Info("── 잘못된 설정값 시뮬레이션 ──");
        DemoHelper.TryCatch("alpha = 1.5 (범위 초과)", () => Guard.Range(1.5f, 0f, 1f));
        DemoHelper.TryCatch("baud_rate = 0 (Positive 실패)", () => Guard.Positive(0));
        DemoHelper.TryCatch("port_name = \"  \" (공백)", () => Guard.NotWhiteSpace("  "));
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 2 — 센서 프레임 수신 파이프라인 시뮬레이션
    // ═══════════════════════════════════════════════════════════════
    static void Scenario2_SensorPipeline()
    {
        DemoHelper.Section("시나리오 2 — 센서 프레임 수신 파이프라인");

        string frameDir = Path.Combine(WorkDir, "frames");
        string logDir = Path.Combine(WorkDir, "logs");
        frameDir.EnsureDirSelf();
        logDir.EnsureDirSelf();

        var rng = new Random(42);
        float lastSmoothed = 22.0f;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        DemoHelper.Info("── 10개 프레임 처리 ──");

        for (int i = 1; i <= 10; i++)
        {
            // ① 프레임 생성 시뮬레이션 (lssLib.Binary 역할)
            uint frameId = (uint)i;
            float rawTemp = 20f + (float)(rng.NextDouble() * 10);
            float rawHum = 40f + (float)(rng.NextDouble() * 20);
            byte[] rawFrame = BuildFrame(frameId, rawTemp, rawHum);

            // ② Guard 검증
            Guard.NotEmpty(rawFrame);
            Guard.That(rawFrame[0] == 0x02, "STX 없음");
            Guard.That(rawFrame[^1] == 0x03, "ETX 없음");

            // ③ 신호 스무딩 (ScaleExtensions 역할 시뮬레이션)
            float smoothed = SmoothStep(rawTemp, lastSmoothed, 0.2f);
            lastSmoothed = smoothed;

            // ④ 로그 기록 (DateTimeExtensions + FileExtensions)
            string logLine =
                $"[{DateTime.Now.ToIsoDateTime()}] " +
                $"ID={frameId.ToString().PadLeftTo(4, '0')} " +
                $"Raw={rawTemp:F2} Smoothed={smoothed:F2} Hum={rawHum:F2} " +
                $"HEX={rawFrame.ToHex(spaced: true)[..11]}…";

            string logPath = Path.Combine(logDir, $"{DateTime.Today.ToIsoDate()}.log");
            logPath.AppendLine(logLine);

            // ⑤ 주기적 바이너리 스냅샷 저장
            if (i % 5 == 0)
            {
                string snapName = $"snap_{DateTime.Now.ToMsStamp()}.bin";
                Path.Combine(frameDir, snapName).WriteBytes(rawFrame);
                DemoHelper.Ok($"스냅샷 저장: {snapName}");
            }

            if (i <= 3 || i == 10)
                DemoHelper.Show($"Frame #{i:D2}",
                    $"Raw={rawTemp:F2}→Smooth={smoothed:F2}  HEX={rawFrame.ToHex()[..8]}…");
        }

        sw.Stop();
        DemoHelper.Info("── 처리 결과 ──");
        DemoHelper.Show("총 처리 시간", sw.Elapsed.ToDisplay());
        DemoHelper.Show("저장된 스냅샷", frameDir.EnumerateByExt(".bin").Count());
        DemoHelper.Show("로그 파일 크기",
        Path.Combine(logDir, $"{DateTime.Today.ToIsoDate()}.log").GetSizeDisplay());
    }

    // ═══════════════════════════════════════════════════════════════
    //  시나리오 3 — 세션 아카이브 + 롤링 정리
    // ═══════════════════════════════════════════════════════════════
    static void Scenario3_ArchiveAndCleanup()
    {
        DemoHelper.Section("시나리오 3 — 세션 아카이브 + 롤링 정리");

        string frameDir = Path.Combine(WorkDir, "frames");
        string archiveDir = Path.Combine(WorkDir, "archive");

        // 추가 테스트 파일 생성
        for (int i = 3; i <= 8; i++)
        {
            Thread.Sleep(5);
            Path.Combine(frameDir, $"snap_2024{i:D2}01.bin").WriteBytes(new byte[i * 100]);
        }

        DemoHelper.Info("── 전체 스냅샷 목록 (최신순) ──");
        var allSnaps = frameDir.EnumerateByDate("*.bin").ToList();
        foreach (var f in allSnaps)
            DemoHelper.Show(f.GetFileName(), $"{f.GetSizeDisplay(),8}  [{f.GetLastModified()?.ToTimeString()}]");

        // 최신 3개 유지, 나머지 아카이브 이동
        DemoHelper.Info("── 최신 3개 유지 → 나머지 아카이브 이동 ──");
        var toArchive = frameDir.EnumerateByDate("*.bin").Skip(3).ToList();
        foreach (var f in toArchive)
        {
            string dateKey = f.GetLastModified()?.ToIsoDate() ?? DateTime.Today.ToIsoDate();
            string dest = Path.Combine(archiveDir, dateKey, f.GetFileName()).ToUniquePath();
            f.MoveTo(dest);
            DemoHelper.Ok($"이동: {f.GetFileName()} → archive/{dateKey}/");
        }

        DemoHelper.Info("── 정리 후 상태 ──");
        DemoHelper.Show("frames/ 남은 파일", frameDir.EnumerateByExt(".bin").Count());
        DemoHelper.Show("archive/ 총 파일", archiveDir.EnumerateFiles(recursive: true).Count());

        DemoHelper.Info("── 디스크 사용량 ──");
        foreach (var dir in new[] { "frames", "archive", "logs" })
        {
            string dirPath = Path.Combine(WorkDir, dir);
            if (!dirPath.DirExists()) continue;
            long total = dirPath.EnumerateFiles(recursive: true).Sum(f => f.GetSize());
            int count = dirPath.EnumerateFiles(recursive: true).Count();
            DemoHelper.Show($"{dir}/", $"{count}개, 합계 {FormatBytes(total)}");
        }
    }

    // ─────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────

    /// <summary>STX + UInt32LE(id) + FloatLE(temp) + FloatLE(hum) + ETX 테스트 프레임 생성.</summary>
    static byte[] BuildFrame(uint id, float temp, float hum)
    {
        var buf = new byte[14];
        buf[0] = 0x02;
        BitConverter.GetBytes(id).CopyTo(buf, 1);
        BitConverter.GetBytes(temp).CopyTo(buf, 5);
        BitConverter.GetBytes(hum).CopyTo(buf, 9);
        buf[13] = 0x03;
        return buf;
    }

    /// <summary>단순 1차 보간 (ScaleExtensions.SmoothStep 동작 시뮬레이션).</summary>
    static float SmoothStep(float target, float current, float alpha)
        => current + (target - current) * alpha;

    static string FormatBytes(long bytes) => bytes switch
    {
        < 1_024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1_024.0:F1} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
        _ => $"{bytes / 1_073_741_824.0:F2} GB"
    };
}