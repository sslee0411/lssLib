using System.IO;
using System.Text;

namespace lssLib.Utils;

// ═══════════════════════════════════════════════════════════════════
//  lssLib.Utils — FileExtensions
//  파일 · 디렉터리 처리 확장 메서드
//  ▸ No abstractions  ▸ Extension-method only
//  ▸ 쓰기 계열: 상위 디렉터리 자동 생성 + 경로 반환 (체이닝)
//  ▸ 바이너리 R/W: lssLib.Binary BufferWriter/BufferParser 직접 연계
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 파일 및 디렉터리 처리 확장 메서드.<br/>
/// 쓰기 계열 메서드는 상위 디렉터리를 자동으로 생성하고 경로를 반환하므로
/// 체이닝 파이프라인을 구성할 수 있습니다.
/// </summary>
public static class FileExtensions
{
    // ─────────────────────────────────────────────
    // §1  경로 조작
    // ─────────────────────────────────────────────

    /// <summary>파일로 존재하면 <c>true</c>를 반환합니다.</summary>
    public static bool FileExists(this string path) => File.Exists(path);

    /// <summary>디렉터리로 존재하면 <c>true</c>를 반환합니다.</summary>
    public static bool DirExists(this string path) => Directory.Exists(path);

    /// <summary>
    /// 확장자를 반환합니다 (점 포함, 예: <c>".bin"</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// @"C:\logs\frame.bin".GetExt()  // ".bin"
    /// </code>
    /// </example>
    public static string GetExt(this string path) => Path.GetExtension(path);

    /// <summary>확장자 없는 파일명을 반환합니다.</summary>
    /// <example>
    /// <code>
    /// @"C:\logs\frame.bin".GetFileNameNoExt()  // "frame"
    /// </code>
    /// </example>
    public static string GetFileNameNoExt(this string path)
        => Path.GetFileNameWithoutExtension(path);

    /// <summary>파일명을 반환합니다 (확장자 포함).</summary>
    /// <example>
    /// <code>
    /// @"C:\logs\frame.bin".GetFileName()  // "frame.bin"
    /// </code>
    /// </example>
    public static string GetFileName(this string path) => Path.GetFileName(path);

    /// <summary>상위 디렉터리 경로를 반환합니다. 루트이면 빈 문자열.</summary>
    /// <example>
    /// <code>
    /// @"C:\logs\frame.bin".GetDir()  // @"C:\logs"
    /// </code>
    /// </example>
    public static string GetDir(this string path)
        => Path.GetDirectoryName(path) ?? string.Empty;

    /// <summary>
    /// 파일명에 타임스탬프 접미사를 삽입한 새 경로를 반환합니다.<br/>
    /// lssLib 세션 덤프 및 로그 파일 네이밍 규칙과 일치합니다.
    /// </summary>
    /// <param name="path">원본 파일 경로.</param>
    /// <param name="at">기준 시각. <c>null</c>이면 <see cref="DateTime.Now"/> 사용.</param>
    /// <param name="format">타임스탬프 포맷. 기본값: <c>"yyyyMMdd_HHmmss"</c>.</param>
    /// <returns>타임스탬프가 포함된 새 파일 경로.</returns>
    /// <example>
    /// <code>
    /// // lssLib.Binary BufferWriter 직렬화 결과 저장
    /// @"frames\session.bin".WithTimestamp()
    /// // → @"frames\session_20240401_143000.bin"
    ///
    /// // 월별 아카이브 파일명
    /// @"reports\summary.csv".WithTimestamp(format: "yyyyMM")
    /// // → @"reports\summary_202404.csv"
    ///
    /// // 체이닝
    /// @"output\frame.bin"
    ///     .WriteBytes(writer.ToArray())
    ///     .WithTimestamp();  // "frame_20240401_143000.bin"
    /// </code>
    /// </example>
    public static string WithTimestamp(this string path,
        DateTime? at = null, string format = "yyyyMMdd_HHmmss")
        => Path.Combine(
            path.GetDir(),
            $"{path.GetFileNameNoExt()}_{(at ?? DateTime.Now).ToString(format)}{path.GetExt()}");

    /// <summary>
    /// 동일 경로의 파일이 이미 존재하면 숫자 접미사를 붙여 고유한 경로를 반환합니다.
    /// </summary>
    /// <returns>충돌하지 않는 고유한 파일 경로.</returns>
    /// <example>
    /// <code>
    /// // "frame.bin" 존재 → "frame_1.bin" → 존재하면 "frame_2.bin"...
    /// string dest = outputPath.ToUniquePath();
    /// File.Copy(source, dest);
    ///
    /// // WithTimestamp와 조합 — 같은 초에 여러 저장이 있어도 충돌 없음
    /// string snap = @"snapshots\frame.bin"
    ///     .WithTimestamp()
    ///     .ToUniquePath();
    /// </code>
    /// </example>
    public static string ToUniquePath(this string path)
    {
        if (!File.Exists(path)) return path;
        var dir = path.GetDir();
        var name = path.GetFileNameNoExt();
        var ext = path.GetExt();
        int i = 1;
        string candidate;
        do { candidate = Path.Combine(dir, $"{name}_{i++}{ext}"); }
        while (File.Exists(candidate));
        return candidate;
    }

    // ─────────────────────────────────────────────
    // §2  디렉터리 자동 보장
    // ─────────────────────────────────────────────

    /// <summary>
    /// 파일 경로의 상위 디렉터리가 없으면 생성한 뒤 파일 경로를 그대로 반환합니다.<br/>
    /// 체이닝 설계 — 쓰기 메서드 바로 앞에 삽입합니다.
    /// </summary>
    /// <returns><paramref name="path"/> (상위 디렉터리 존재 보장).</returns>
    /// <example>
    /// <code>
    /// // 중첩 디렉터리 자동 생성 후 쓰기
    /// @"logs\2024\04\session.log"
    ///     .EnsureDir()
    ///     .AppendLine($"[{DateTime.Now.ToIsoDateTime()}] 시작");
    ///
    /// // lssLib.Binary BufferWriter 결과 저장
    /// @"output\frames\capture.bin"
    ///     .EnsureDir()
    ///     .WriteBytes(writer.ToArray());
    /// </code>
    /// </example>
    public static string EnsureDir(this string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return path;
    }

    /// <summary>
    /// 디렉터리 경로 자체가 없으면 생성한 뒤 디렉터리 경로를 반환합니다.<br/>
    /// <see cref="EnsureDir"/>과 달리 경로 자체가 디렉터리일 때 사용합니다.
    /// </summary>
    /// <returns><paramref name="dir"/> (디렉터리 존재 보장).</returns>
    /// <example>
    /// <code>
    /// // 출력 구조 초기화
    /// Path.Combine(baseDir, "frames").EnsureDirSelf();
    /// Path.Combine(baseDir, "logs").EnsureDirSelf();
    ///
    /// // Guard와 체이닝
    /// string outDir = Guard.NotWhiteSpace(config.OutputDir).EnsureDirSelf();
    ///
    /// // 열거 전에 폴더 보장
    /// @"archive\2024".EnsureDirSelf().EnumerateFiles("*.bin");
    /// </code>
    /// </example>
    public static string EnsureDirSelf(this string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ─────────────────────────────────────────────
    // §3  파일 읽기
    // ─────────────────────────────────────────────

    /// <summary>
    /// 파일 전체를 텍스트로 읽습니다. 기본 인코딩은 UTF-8입니다.
    /// </summary>
    /// <example>
    /// <code>
    /// string json   = @"config.json".ReadText();
    /// string legacy = @"legacy.txt".ReadText(Encoding.GetEncoding("EUC-KR"));
    ///
    /// // lssLib.Extensions TextExtensions 연계
    /// var schema = BufSchema.FromJson(@"schema.json".ReadText());
    /// </code>
    /// </example>
    public static string ReadText(this string path, Encoding? enc = null)
        => File.ReadAllText(path, enc ?? Encoding.UTF8);

    /// <summary>
    /// 파일 전체를 비동기로 텍스트 읽기합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// string content = await @"report.txt".ReadTextAsync(ct: ct);
    /// </code>
    /// </example>
    public static Task<string> ReadTextAsync(this string path,
        Encoding? enc = null, CancellationToken ct = default)
        => File.ReadAllTextAsync(path, enc ?? Encoding.UTF8, ct);

    /// <summary>
    /// 파일 전체를 바이트 배열로 읽습니다.<br/>
    /// 반환 배열은 lssLib.Binary <c>BufferParser</c>에 직접 전달할 수 있습니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // lssLib.Binary 연계 — 저장된 프레임 재생
    /// byte[] raw    = @"dumps\session.bin".ReadBytes();
    /// var    parser = new BufferParser(Guard.NotEmpty(raw));
    /// uint   id     = parser.Read<uint>(BufType.UInt32LE);
    /// float  temp   = parser.Read<float>(BufType.FloatLE);
    /// </code>
    /// </example>
    public static byte[] ReadBytes(this string path)
        => File.ReadAllBytes(path);

    /// <summary>파일 전체를 비동기로 바이트 배열 읽기합니다.</summary>
    public static Task<byte[]> ReadBytesAsync(this string path, CancellationToken ct = default)
        => File.ReadAllBytesAsync(path, ct);

    /// <summary>
    /// 파일을 줄 단위로 지연 읽기합니다.<br/>
    /// 대용량 파일에서 전체를 메모리에 올리지 않고 처리할 때 사용합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 대용량 로그 분석 — 메모리 절약
    /// var errors = @"app.log".ReadLines()
    ///     .Where(l => l.ContainsIgnoreCase("ERROR"))
    ///     .ToList();
    /// </code>
    /// </example>
    public static IEnumerable<string> ReadLines(this string path, Encoding? enc = null)
        => File.ReadLines(path, enc ?? Encoding.UTF8);

    // ─────────────────────────────────────────────
    // §4  파일 쓰기 (경로 반환 — 체이닝)
    //     ▸ 상위 디렉터리 자동 생성
    //     ▸ 반환값: 쓴 파일의 경로 (체이닝 가능)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 텍스트를 파일에 씁니다 (덮어씀). 상위 디렉터리를 자동으로 생성합니다.
    /// </summary>
    /// <returns><paramref name="path"/> (체이닝 가능).</returns>
    /// <example>
    /// <code>
    /// // 설정 파일 업데이트 후 즉시 백업
    /// @"config\app.json"
    ///     .WriteText(newJson)
    ///     .CopyTo(@"config\backup\app.json");
    ///
    /// // lssLib.Extensions TextExtensions 직렬화 결과 저장
    /// @"output\schema.json".WriteText(schema.ToJson());
    /// </code>
    /// </example>
    public static string WriteText(this string path, string content, Encoding? enc = null)
    {
        path.EnsureDir();
        File.WriteAllText(path, content, enc ?? Encoding.UTF8);
        return path;
    }

    /// <summary>텍스트를 파일에 비동기로 씁니다 (덮어씀).</summary>
    public static async Task<string> WriteTextAsync(this string path, string content,
        Encoding? enc = null, CancellationToken ct = default)
    {
        path.EnsureDir();
        await File.WriteAllTextAsync(path, content, enc ?? Encoding.UTF8, ct);
        return path;
    }

    /// <summary>
    /// 바이트 배열을 파일에 씁니다 (덮어씀). 상위 디렉터리를 자동으로 생성합니다.<br/>
    /// lssLib.Binary <c>BufferWriter.ToArray()</c>를 직접 전달할 수 있습니다.
    /// </summary>
    /// <returns><paramref name="path"/> (체이닝 가능).</returns>
    /// <example>
    /// <code>
    /// // lssLib.Binary 직렬화 결과 저장
    /// var writer = new BufferWriter();
    /// writer.Write(BufType.UInt32LE, sensorId);
    /// writer.Write(BufType.FloatLE,  temperature);
    ///
    /// string saved = @"frames\sensor.bin"
    ///     .WriteBytes(writer.ToArray())   // 경로 반환
    ///     .WithTimestamp();               // "sensor_20240401_143000.bin"
    ///
    /// // AppendCrc32 포함 저장 (lssLib.Extensions.CrcExtensions)
    /// @"output\verified.bin".WriteBytes(frame.AppendCrc32());
    /// </code>
    /// </example>
    public static string WriteBytes(this string path, byte[] data)
    {
        path.EnsureDir();
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>바이트 배열을 파일에 비동기로 씁니다 (덮어씀).</summary>
    public static async Task<string> WriteBytesAsync(this string path, byte[] data,
        CancellationToken ct = default)
    {
        path.EnsureDir();
        await File.WriteAllBytesAsync(path, data, ct);
        return path;
    }

    /// <summary>
    /// 텍스트 한 줄을 파일 끝에 추가합니다. 파일이 없으면 생성합니다.<br/>
    /// 간단한 파일 기반 로깅에 적합합니다.
    /// </summary>
    /// <returns><paramref name="path"/> (체이닝 가능).</returns>
    /// <example>
    /// <code>
    /// // 날짜별 로그 파일에 누적
    /// string logFile = $@"logs\{DateTime.Today.ToIsoDate()}.log";
    /// logFile.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] INFO 서버 시작");
    /// logFile.AppendLine($"[{DateTime.Now.ToIsoDateTime()}] INFO 포트 8080 열림");
    /// </code>
    /// </example>
    public static string AppendLine(this string path, string line, Encoding? enc = null)
    {
        path.EnsureDir();
        using var sw = new StreamWriter(path, append: true, enc ?? Encoding.UTF8);
        sw.WriteLine(line);
        return path;
    }

    // ─────────────────────────────────────────────
    // §5  파일 메타데이터
    // ─────────────────────────────────────────────

    /// <summary>
    /// 파일 크기를 바이트 단위로 반환합니다. 파일이 없으면 <c>-1</c>을 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// long bytes = @"capture.bin".GetSize();  // -1: 파일 없음
    /// </code>
    /// </example>
    public static long GetSize(this string path)
        => File.Exists(path) ? new FileInfo(path).Length : -1L;

    /// <summary>
    /// 파일 크기를 자동 단위(B/KB/MB/GB)로 변환하여 반환합니다.<br/>
    /// 파일이 없으면 <c>"N/A"</c>를 반환합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// @"small.txt".GetSizeDisplay()   // "512 B"
    /// @"log.zip".GetSizeDisplay()     // "1.4 MB"
    /// @"backup.tar".GetSizeDisplay()  // "2.31 GB"
    /// @"missing".GetSizeDisplay()     // "N/A"
    ///
    /// // 디스크 사용량 리포트
    /// foreach (var f in outputDir.EnumerateByDate("*.bin").Take(10))
    ///     Console.WriteLine($"{f.GetSizeDisplay(),10}  {f.GetFileName()}");
    /// </code>
    /// </example>
    public static string GetSizeDisplay(this string path)
    {
        long bytes = path.GetSize();
        if (bytes < 0) return "N/A";
        return bytes switch
        {
            < 1_024 => $"{bytes} B",
            < 1_048_576 => $"{bytes / 1_024.0:F1} KB",
            < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
            _ => $"{bytes / 1_073_741_824.0:F2} GB"
        };
    }

    /// <summary>
    /// 파일의 마지막 수정 시각을 반환합니다. 파일이 없으면 <c>null</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// // 설정 파일 변경 감지
    /// DateTime? modified = configPath.GetLastModified();
    /// if (modified &gt; lastChecked)
    ///     ReloadConfig();
    /// </code>
    /// </example>
    public static DateTime? GetLastModified(this string path)
        => File.Exists(path) ? File.GetLastWriteTime(path) : null;

    // ─────────────────────────────────────────────
    // §6  디렉터리 열거
    // ─────────────────────────────────────────────

    /// <summary>
    /// 디렉터리의 파일을 열거합니다.<br/>
    /// 디렉터리가 없어도 예외 없이 빈 <see cref="IEnumerable{T}"/>를 반환합니다.
    /// </summary>
    /// <param name="dir">열거할 디렉터리 경로.</param>
    /// <param name="pattern">파일 패턴. 기본값: <c>"*"</c> (전체).</param>
    /// <param name="recursive">하위 디렉터리 포함 여부.</param>
    /// <example>
    /// <code>
    /// @"logs".EnumerateFiles()                         // 전체
    /// @"logs".EnumerateFiles("*.bin")                  // .bin만
    /// @"logs".EnumerateFiles("*.log", recursive: true) // 하위 포함
    /// </code>
    /// </example>
    public static IEnumerable<string> EnumerateFiles(this string dir,
        string pattern = "*", bool recursive = false)
    {
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        var opt = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(dir, pattern, opt);
    }

    /// <summary>
    /// 확장자로 파일을 필터링하여 열거합니다.
    /// </summary>
    /// <param name="dir">열거할 디렉터리 경로.</param>
    /// <param name="ext">확장자 (점 포함, 예: <c>".bin"</c>).</param>
    /// <param name="recursive">하위 디렉터리 포함 여부.</param>
    /// <example>
    /// <code>
    /// @"captures".EnumerateByExt(".bin")
    /// @"exports".EnumerateByExt(".csv", recursive: true)
    /// </code>
    /// </example>
    public static IEnumerable<string> EnumerateByExt(this string dir,
        string ext, bool recursive = false)
        => dir.EnumerateFiles("*" + ext, recursive);

    /// <summary>
    /// 파일을 수정 시간 내림차순(최신 순)으로 열거합니다.<br/>
    /// 최신 파일 우선 처리, 오래된 파일 정리에 사용합니다.
    /// </summary>
    /// <param name="dir">열거할 디렉터리 경로.</param>
    /// <param name="pattern">파일 패턴. 기본값: <c>"*"</c>.</param>
    /// <param name="recursive">하위 디렉터리 포함 여부.</param>
    /// <example>
    /// <code>
    /// // 최신 10개 파일
    /// @"logs".EnumerateByDate().Take(10)
    ///
    /// // 7일 이상 지난 로그 자동 정리
    /// @"logs".EnumerateByDate()
    ///        .Where(f => f.GetLastModified() < DateTime.Now.AddDays(-7))
    ///        .ToList()
    ///        .ForEach(f => f.TryDelete());
    ///
    /// // 최신 20개만 유지 (롤링)
    /// @"snapshots".EnumerateByDate("*.bin")
    ///             .Skip(20)
    ///             .ToList()
    ///             .ForEach(f => f.TryDelete());
    /// </code>
    /// </example>
    public static IEnumerable<string> EnumerateByDate(this string dir,
        string pattern = "*", bool recursive = false)
        => dir.EnumerateFiles(pattern, recursive)
              .OrderByDescending(f => File.GetLastWriteTime(f));

    // ─────────────────────────────────────────────
    // §7  안전 삭제 · 복사 · 이동
    // ─────────────────────────────────────────────

    /// <summary>
    /// 파일을 안전하게 삭제합니다. 파일이 없으면 <c>false</c>, 성공하면 <c>true</c>를 반환합니다.<br/>
    /// 예외를 throw하지 않습니다.
    /// </summary>
    /// <example>
    /// <code>
    /// // 임시 파일 정리
    /// if (!tempPath.TryDelete())
    ///     logger.Warn($"임시 파일 삭제 실패 (무시): {tempPath}");
    ///
    /// // finally 블록에서 안전 정리
    /// finally { tempPath.TryDelete(); }
    /// </code>
    /// </example>
    public static bool TryDelete(this string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); return true; } return false; }
        catch { return false; }
    }

    /// <summary>
    /// 파일을 대상 경로로 복사합니다. 대상 디렉터리를 자동으로 생성합니다.
    /// </summary>
    /// <param name="source">원본 파일 경로.</param>
    /// <param name="dest">대상 파일 경로.</param>
    /// <param name="overwrite">덮어씀 여부. 기본값: <c>true</c>.</param>
    /// <returns>대상 파일 경로 (체이닝 가능).</returns>
    /// <exception cref="IOException"><paramref name="overwrite"/>가 <c>false</c>이고 대상 파일이 이미 존재하는 경우.</exception>
    /// <example>
    /// <code>
    /// // 설정 파일 백업 후 업데이트 (체이닝)
    /// @"config\app.json"
    ///     .WriteText(newJson)
    ///     .CopyTo(@"config\backup\app.json");
    ///
    /// // overwrite: false — 이미 있으면 IOException
    /// src.CopyTo(dest, overwrite: false);
    /// </code>
    /// </example>
    public static string CopyTo(this string source, string dest, bool overwrite = true)
    {
        dest.EnsureDir();
        File.Copy(source, dest, overwrite);
        return dest;
    }

    /// <summary>
    /// 파일을 대상 경로로 이동합니다. 대상 디렉터리를 자동으로 생성합니다.
    /// </summary>
    /// <param name="source">원본 파일 경로.</param>
    /// <param name="dest">대상 파일 경로.</param>
    /// <param name="overwrite">덮어씀 여부. 기본값: <c>true</c>.</param>
    /// <returns>대상 파일 경로 (체이닝 가능).</returns>
    /// <example>
    /// <code>
    /// // 처리 완료 파일 아카이브로 이동
    /// string archived = @"frames\frame_001.bin"
    ///     .MoveTo(@"archive\2024-04\frame_001.bin".ToUniquePath());
    ///
    /// // 날짜별 아카이브
    /// void ArchiveFile(string path)
    /// {
    ///     string date  = path.GetLastModified()?.ToIsoDate() ?? "unknown";
    ///     string dest  = Path.Combine("archive", date, path.GetFileName());
    ///     path.MoveTo(dest.ToUniquePath());
    /// }
    /// </code>
    /// </example>
    public static string MoveTo(this string source, string dest, bool overwrite = true)
    {
        dest.EnsureDir();
        File.Move(source, dest, overwrite);
        return dest;
    }
}