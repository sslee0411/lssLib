using lssLib.Utils;

namespace lssLib.Utils.Demo;

// ═══════════════════════════════════════════════════════════════════
//  FileDemo — FileExtensions 예제
//  §1 경로조작     §2 EnsureDir·쓰기·읽기
//  §3 메타데이터   §4 디렉터리열거          §5 삭제·복사·이동
//  ▸ 시스템 임시 폴더에 실제 파일을 생성하고 마지막에 정리
// ═══════════════════════════════════════════════════════════════════

internal static class FileDemo
{
    static readonly string BaseDir = Path.Combine(Path.GetTempPath(), "lssLib_demo");

    public static void Run()
    {
        DemoHelper.Header("FileExtensions — 파일 · 디렉터리 처리");
        BaseDir.EnsureDirSelf();
        DemoHelper.Info($"임시 작업 디렉터리: {BaseDir}");

        PathDemo();
        WriteReadDemo();
        MetaDemo();
        EnumerateDemo();
        CopyMoveDeleteDemo();

        Directory.Delete(BaseDir, recursive: true);
        DemoHelper.Info("임시 디렉터리 정리 완료");
    }

    // ─────────────────────────────────────────────
    // §1  경로 조작
    // ─────────────────────────────────────────────
    static void PathDemo()
    {
        DemoHelper.Section("§1  경로 조작");

        string path = Path.Combine(BaseDir, "frames", "capture.bin");

        DemoHelper.Show("GetExt()", path.GetExt());
        DemoHelper.Show("GetFileNameNoExt()", path.GetFileNameNoExt());
        DemoHelper.Show("GetFileName()", path.GetFileName());
        DemoHelper.Show("GetDir()", path.GetDir());

        DemoHelper.Show("WithTimestamp()", Path.GetFileName(path.WithTimestamp()));
        DemoHelper.Show("WithTimestamp(\"yyyyMMdd\")", Path.GetFileName(path.WithTimestamp(format: "yyyyMMdd")));

        // ToUniquePath — 파일 없으면 원본 반환
        DemoHelper.Show("ToUniquePath() (파일 없음)", Path.GetFileName(path.ToUniquePath()));

        // 파일 생성 후 다시 시도
        path.EnsureDir().WriteText("dummy");
        DemoHelper.Show("ToUniquePath() (파일 있음)", Path.GetFileName(path.ToUniquePath()));
        path.TryDelete();
    }

    // ─────────────────────────────────────────────
    // §2  EnsureDir · 쓰기 · 읽기
    // ─────────────────────────────────────────────
    static void WriteReadDemo()
    {
        DemoHelper.Section("§2  EnsureDir · 쓰기 · 읽기");

        // WriteText — 상위 디렉터리 자동 생성
        string txtPath = Path.Combine(BaseDir, "logs", "2024-04", "app.log");
        txtPath.WriteText("첫 번째 로그 항목");
        DemoHelper.Ok($"WriteText → {txtPath.GetFileName()} ({txtPath.GetSizeDisplay()})");

        // AppendLine
        txtPath.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] INFO  센서 연결");
        txtPath.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] INFO  수신 시작");
        txtPath.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] DEBUG Frame#001");
        DemoHelper.Ok($"AppendLine 3줄 추가 → {txtPath.GetSizeDisplay()}");

        // ReadText / ReadLines
        string content = txtPath.ReadText();
        DemoHelper.Show("ReadText() 줄 수", content.ToNonEmptyLines().Count());
        var lines = txtPath.ReadLines().ToList();
        DemoHelper.Show("ReadLines() 줄 수", lines.Count);
        foreach (var l in lines)
            DemoHelper.Show("  줄", l.Truncate(60));

        // WriteBytes / ReadBytes
        byte[] binData = { 0x02, 0x01, 0x00, 0x00, 0x00, 0x41, 0x20, 0xF3, 0x42, 0x03 };
        string binPath = Path.Combine(BaseDir, "frames", "frame_001.bin");
        binPath.WriteBytes(binData);
        DemoHelper.Ok($"WriteBytes → {binPath.GetFileName()} ({binPath.GetSizeDisplay()})");

        byte[] readBack = binPath.ReadBytes();
        DemoHelper.Show("ReadBytes() 크기", readBack.Length);
        DemoHelper.Show("HEX 덤프", readBack.ToHex(spaced: true));

        // 체이닝 패턴
        string saved = Path.Combine(BaseDir, "output", "result.json")
            .WriteText("{\"status\":\"ok\",\"count\":42}");
        DemoHelper.Ok($"체이닝 WriteText → {saved.GetFileName()}");
    }

    // ─────────────────────────────────────────────
    // §3  파일 메타데이터
    // ─────────────────────────────────────────────
    static void MetaDemo()
    {
        DemoHelper.Section("§3  파일 메타데이터");

        var files = new (string name, int size)[]
        {
            ("tiny.txt",   100),
            ("small.bin",  2_048),
            ("medium.dat", 1_200_000),
        };
        foreach (var (name, size) in files)
            Path.Combine(BaseDir, "meta", name).WriteBytes(new byte[size]);

        DemoHelper.Info("── 파일 크기 표시 ──");
        foreach (var (name, _) in files)
        {
            string p = Path.Combine(BaseDir, "meta", name);
            DemoHelper.Show(name, $"{p.GetSize(),12:N0} B  →  {p.GetSizeDisplay()}");
        }

        string logPath = Path.Combine(BaseDir, "meta", "tiny.txt");
        DemoHelper.Show("GetLastModified()", logPath.GetLastModified()?.ToIsoDateTime() ?? "<null>");
        DemoHelper.Show("GetSize(없는 파일)", @"C:\nonexistent.txt".GetSize());
        DemoHelper.Show("GetSizeDisplay(없는 파일)", @"C:\nonexistent.txt".GetSizeDisplay());
    }

    // ─────────────────────────────────────────────
    // §4  디렉터리 열거
    // ─────────────────────────────────────────────
    static void EnumerateDemo()
    {
        DemoHelper.Section("§4  디렉터리 열거");

        string capDir = Path.Combine(BaseDir, "captures");
        capDir.EnsureDirSelf();
        foreach (var f in new[] { "frame_001.bin", "frame_002.bin", "frame_003.bin" })
        {
            Thread.Sleep(5);
            Path.Combine(capDir, f).WriteBytes(new byte[512]);
        }
        foreach (var f in new[] { "session_a.log", "session_b.log" })
            Path.Combine(capDir, f).WriteText("log");

        var bins = capDir.EnumerateByExt(".bin").ToList();
        DemoHelper.Show("EnumerateByExt(\".bin\")", bins.Count);
        foreach (var f in bins) DemoHelper.Show("  .bin 파일", f.GetFileName());

        DemoHelper.Info("── EnumerateByDate() 최신순 ──");
        foreach (var f in capDir.EnumerateByDate())
            DemoHelper.Show($"  [{f.GetLastModified()?.ToTimeString()}]", f.GetFileName());

        DemoHelper.Info("── 최신 1개 유지 (롤링) ──");
        var toDelete = capDir.EnumerateByDate("*.bin").Skip(1).ToList();
        foreach (var f in toDelete) { f.TryDelete(); DemoHelper.Show("  삭제", f.GetFileName()); }
        DemoHelper.Show("남은 .bin 파일", capDir.EnumerateByExt(".bin").Count());

        var empty = @"C:\nonexistent_xyz".EnumerateFiles().ToList();
        DemoHelper.Show("없는 디렉터리 열거 (예외 없음)", $"{empty.Count}개");
    }

    // ─────────────────────────────────────────────
    // §5  안전 삭제 · 복사 · 이동
    // ─────────────────────────────────────────────
    static void CopyMoveDeleteDemo()
    {
        DemoHelper.Section("§5  안전 삭제 · 복사 · 이동");

        // TryDelete
        string tmp = Path.Combine(BaseDir, "temp.tmp");
        tmp.WriteText("임시");
        DemoHelper.Show("TryDelete() 성공", tmp.TryDelete());
        DemoHelper.Show("TryDelete() 없는 파일", tmp.TryDelete());

        // CopyTo — 대상 폴더 자동 생성
        string src = Path.Combine(BaseDir, "output", "result.json");
        string dest = Path.Combine(BaseDir, "backup", "2024-04", "result.json");
        if (src.FileExists())
        {
            string copied = src.CopyTo(dest);
            DemoHelper.Ok($"CopyTo → {copied.GetFileName()} ({copied.GetSizeDisplay()})");
            DemoHelper.Show("원본 존재", src.FileExists());
            DemoHelper.Show("복사본 존재", copied.FileExists());
        }

        // MoveTo — 아카이브 이동
        string moveSrc = Path.Combine(BaseDir, "frames", "frame_001.bin");
        string moveDest = Path.Combine(BaseDir, "archive", "2024-04", "frame_001.bin");
        if (moveSrc.FileExists())
        {
            string moved = moveSrc.MoveTo(moveDest);
            DemoHelper.Ok($"MoveTo → {moved.GetFileName()}");
            DemoHelper.Show("원본 삭제됨", !moveSrc.FileExists());
            DemoHelper.Show("이동본 존재", moved.FileExists());
        }

        // ToUniquePath + CopyTo 조합
        if (dest.FileExists())
        {
            string uniqueDest = dest.ToUniquePath();
            src.CopyTo(uniqueDest);
            DemoHelper.Show("고유 경로 복사", Path.GetFileName(uniqueDest));
        }
    }
}