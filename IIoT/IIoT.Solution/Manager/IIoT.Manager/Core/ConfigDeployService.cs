// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/ConfigDeployService.cs
//  역할: 설정 배포 실행자 (요구사항 4-2-7)
//        소스(Studio Config) → 대상 프로그램 {exe폴더}\Config 로
//        ① 기존 파일 백업 → ② 파일 복사 → ③ .signal 발행(자동 재시작 연동)
//  MG-06: 신규
//  MG-EX-08: 롤백 추가 — GetBackupNames(백업 시점 목록) + RollbackAsync
//        (롤백 전 현재 상태를 추가 백업 → 복원 → .signal 발행)
//  MG-EX-09: 비교 추가 — CompareAsync (소스↔대상 파일별 라인 diff 요약:
//        동일/다름(+n/-n줄)/대상에 없음/소스에 없음 + 샘플 변경 라인)
//  설계 메모:
//    - 백업: 대상 Config\Backup\yyyyMMdd_HHmmss\ 에 기존 파일 보관 (롤백용)
//    - .signal: Collector ConfigReloadWatcher 가 "*.signal" FSW 감지 →
//      설정 자동 재로드 (Studio 저장 시와 동일한 규약: device.json.signal)
//    - 파일 IO 는 Task.Run — UI 스레드 비블로킹
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-08)
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using lssLib.Log;
using System.IO;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)

namespace IIoT.Manager.Core;

/// <summary>배포 1회 결과.</summary>
public readonly record struct DeployResult(bool Ok, string Message);

/// <summary>★ MG-EX-09: 파일 1개의 비교 결과.</summary>
/// <param name="FileName">파일명</param>
/// <param name="Status">동일 / 다름 / 대상에 없음(신규 배포됨) / 소스에 없음</param>
/// <param name="Added">소스에만 있는 라인 수 (배포 시 추가)</param>
/// <param name="Removed">대상에만 있는 라인 수 (배포 시 제거)</param>
/// <param name="Samples">샘플 변경 라인 (+/- 접두, 최대 10줄)</param>
public sealed record FileCompareResult(string FileName, string Status,
                                       int Added, int Removed,
                                       IReadOnlyList<string> Samples);

/// <summary>설정 배포 서비스 (DI 싱글턴).</summary>
public sealed class ConfigDeployService
{
    // §1 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>상대 경로를 Manager 실행 폴더 기준 절대 경로로 해석한다.</summary>
    public static string ResolvePath(string path) =>
        Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));

    /// <summary>
    /// 소스 폴더의 지정 파일들을 대상 프로그램의 Config 폴더로 배포한다.
    /// (백업 → 복사 → .signal 발행)
    /// </summary>
    public async Task<DeployResult> DeployAsync(ManagedProcessInfo target,
                                                string             sourceDir,
                                                IReadOnlyList<string> files)
    {
        try
        {
            return await Task.Run(() => _Deploy(target, sourceDir, files));
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Deploy", $"{target.Name} 배포 실패: {ex.Message}");
            return new DeployResult(false, ex.Message);
        }
    }

    /// <summary>
    /// ★ MG-EX-08: 대상 프로그램의 백업 시점 목록 (최신순 — Config\Backup\ 하위 폴더명).
    /// </summary>
    public IReadOnlyList<string> GetBackupNames(ManagedProcessInfo target)
    {
        try
        {
            var backupRoot = Path.Combine(_TargetConfigDir(target) ?? "", "Backup");
            if (!Directory.Exists(backupRoot)) return [];

            return Directory.EnumerateDirectories(backupRoot)
                            .Select(Path.GetFileName)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .OrderByDescending(n => n)
                            .Cast<string>()
                            .ToList();
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("Deploy", $"{target.Name} 백업 목록 조회 실패: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// ★ MG-EX-08: 지정 백업 시점으로 복원한다.
    /// (현재 상태 추가 백업 → 백업 파일 복사 → .signal 발행)
    /// </summary>
    public async Task<DeployResult> RollbackAsync(ManagedProcessInfo target, string backupName)
    {
        try
        {
            return await Task.Run(() => _Rollback(target, backupName));
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("Deploy", $"{target.Name} 롤백 실패: {ex.Message}");
            return new DeployResult(false, ex.Message);
        }
    }

    /// <summary>
    /// ★ MG-EX-09: 소스 ↔ 대상의 배포 파일별 차이를 비교한다 (배포 전 확인용).
    /// 라인 단위 멀티셋 비교 — 순서 이동은 무시, 내용 추가/제거만 판정.
    /// </summary>
    public async Task<IReadOnlyList<FileCompareResult>> CompareAsync(
        ManagedProcessInfo target, string sourceDir, IReadOnlyList<string> files)
    {
        return await Task.Run(() =>
        {
            var results = new List<FileCompareResult>();
            var src     = ResolvePath(sourceDir);
            var dstDir  = _TargetConfigDir(target);

            foreach (var f in files)
            {
                try
                {
                    var srcPath = Path.Combine(src, f);
                    var dstPath = dstDir is null ? null : Path.Combine(dstDir, f);

                    var srcExists = File.Exists(srcPath);
                    var dstExists = dstPath is not null && File.Exists(dstPath);

                    if (!srcExists)
                    {
                        results.Add(new FileCompareResult(f, "소스에 없음 (배포 불가)", 0, 0, []));
                        continue;
                    }
                    if (!dstExists)
                    {
                        results.Add(new FileCompareResult(f, "대상에 없음 (신규 배포됨)", 0, 0, []));
                        continue;
                    }

                    var srcLines = File.ReadAllLines(srcPath);
                    var dstLines = File.ReadAllLines(dstPath!);

                    // 멀티셋 차집합 (같은 라인이 여러 번 나와도 정확히 계수)
                    var counts = new Dictionary<string, int>();
                    foreach (var l in srcLines) counts[l] = counts.GetValueOrDefault(l) + 1;
                    foreach (var l in dstLines) counts[l] = counts.GetValueOrDefault(l) - 1;

                    var added   = new List<string>();   // 소스에만 (배포 시 추가)
                    var removed = new List<string>();   // 대상에만 (배포 시 제거)
                    foreach (var (line, c) in counts)
                    {
                        if (c > 0) for (int i = 0; i < c;  i++) added.Add(line);
                        if (c < 0) for (int i = 0; i < -c; i++) removed.Add(line);
                    }

                    if (added.Count == 0 && removed.Count == 0)
                    {
                        results.Add(new FileCompareResult(f, "동일", 0, 0, []));
                        continue;
                    }

                    // 샘플: + 5줄 / - 5줄 (긴 라인은 80자 컷)
                    var samples = new List<string>();
                    foreach (var l in added.Take(5))
                        samples.Add("+ " + (l.Length > 80 ? l[..80] + "…" : l).Trim());
                    foreach (var l in removed.Take(5))
                        samples.Add("- " + (l.Length > 80 ? l[..80] + "…" : l).Trim());

                    results.Add(new FileCompareResult(
                        f, $"다름 (+{added.Count}줄 / -{removed.Count}줄)",
                        added.Count, removed.Count, samples));
                }
                catch (Exception ex)
                {
                    results.Add(new FileCompareResult(f, $"비교 실패: {ex.Message}", 0, 0, []));
                }
            }

            return (IReadOnlyList<FileCompareResult>)results;
        });
    }

    // §2 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>★ MG-EX-08: 대상 프로그램의 Config 폴더 절대 경로 (해석 실패 시 null).</summary>
    private static string? _TargetConfigDir(ManagedProcessInfo target)
    {
        var exeDir = Path.GetDirectoryName(ResolvePath(target.ExePath));
        return string.IsNullOrEmpty(exeDir) ? null : Path.Combine(exeDir, "Config");
    }

    /// <summary>★ MG-EX-08: 롤백 본체 — 현재 상태 백업 후 백업 시점 파일 복원.</summary>
    private static DeployResult _Rollback(ManagedProcessInfo target, string backupName)
    {
        var dstDir = _TargetConfigDir(target);
        if (dstDir is null)
            return new DeployResult(false, $"대상 경로 해석 실패: {target.ExePath}");

        var backupDir = Path.Combine(dstDir, "Backup", backupName);
        if (!Directory.Exists(backupDir))
            return new DeployResult(false, $"백업 폴더 없음: {backupName}");

        var backupFiles = Directory.GetFiles(backupDir);
        if (backupFiles.Length == 0)
            return new DeployResult(false, $"백업 폴더가 비어 있음: {backupName}");

        // ① 현재 상태를 추가 백업 (롤백의 롤백 가능하도록 — 목록에 함께 표시됨)
        var preDir  = Path.Combine(dstDir, "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        var preSaved = 0;
        foreach (var bf in backupFiles)
        {
            var cur = Path.Combine(dstDir, Path.GetFileName(bf));
            if (!File.Exists(cur)) continue;
            Directory.CreateDirectory(preDir);
            File.Copy(cur, Path.Combine(preDir, Path.GetFileName(bf)), overwrite: true);
            preSaved++;
        }

        // ② 백업 파일 복원
        foreach (var bf in backupFiles)
            File.Copy(bf, Path.Combine(dstDir, Path.GetFileName(bf)), overwrite: true);

        // ③ .signal 발행 (배포와 동일 규약 — Collector 자동 재로드)
        File.WriteAllText(Path.Combine(dstDir, "device.json.signal"),
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        var msg = $"{backupName} 시점으로 {backupFiles.Length}개 파일 복원" +
                  (preSaved > 0 ? $" (직전 상태 백업 {preSaved}개)" : "");
        LogManager.Instance.Info("Deploy", $"{target.Name} 롤백 — {msg}");
        return new DeployResult(true, msg);
    }

    private static DeployResult _Deploy(ManagedProcessInfo target,
                                        string sourceDir,
                                        IReadOnlyList<string> files)
    {
        // ① 경로 해석·검증
        var src = ResolvePath(sourceDir);
        if (!Directory.Exists(src))
            return new DeployResult(false, $"소스 폴더 없음: {src}");

        var exePath = ResolvePath(target.ExePath);
        var exeDir  = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(exeDir))
            return new DeployResult(false, $"대상 경로 해석 실패: {target.ExePath}");

        var dstDir = Path.Combine(exeDir, "Config");

        // ② 소스 파일 존재 확인 (하나라도 없으면 배포 중단 — 부분 배포 방지)
        var missing = files.Where(f => !File.Exists(Path.Combine(src, f))).ToList();
        if (missing.Count > 0)
            return new DeployResult(false, $"소스 파일 없음: {string.Join(", ", missing)}");

        Directory.CreateDirectory(dstDir);

        // ③ 기존 파일 백업 (Config\Backup\yyyyMMdd_HHmmss\)
        var backupDir = Path.Combine(dstDir, "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        var backedUp  = 0;
        foreach (var f in files)
        {
            var dstFile = Path.Combine(dstDir, f);
            if (!File.Exists(dstFile)) continue;

            Directory.CreateDirectory(backupDir);
            File.Copy(dstFile, Path.Combine(backupDir, f), overwrite: true);
            backedUp++;
        }

        // ④ 복사
        foreach (var f in files)
            File.Copy(Path.Combine(src, f), Path.Combine(dstDir, f), overwrite: true);

        // ⑤ .signal 발행 — Studio 저장 규약과 동일 (Collector FSW "*.signal" 감지)
        File.WriteAllText(Path.Combine(dstDir, "device.json.signal"),
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        var msg = $"{files.Count}개 파일 배포 완료" +
                  (backedUp > 0 ? $" (백업 {backedUp}개)" : "");
        LogManager.Instance.Info("Deploy", $"{target.Name} ← {msg} [{dstDir}]");
        return new DeployResult(true, msg);
    }
}
