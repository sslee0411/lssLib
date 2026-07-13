// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/ConfigDeployService.cs
//  역할: 설정 배포 실행자 (요구사항 4-2-7)
//        소스(Studio Config) → 대상 프로그램 {exe폴더}\Config 로
//        ① 기존 파일 백업 → ② 파일 복사 → ③ .signal 발행(자동 재시작 연동)
//  MG-06: 신규
//  설계 메모:
//    - 백업: 대상 Config\Backup\yyyyMMdd_HHmmss\ 에 기존 파일 보관 (롤백용)
//    - .signal: Collector ConfigReloadWatcher 가 "*.signal" FSW 감지 →
//      설정 자동 재로드 (Studio 저장 시와 동일한 규약: device.json.signal)
//    - 파일 IO 는 Task.Run — UI 스레드 비블로킹
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using lssLib.Log;
using System.IO;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)

namespace IIoT.Manager.Core;

/// <summary>배포 1회 결과.</summary>
public readonly record struct DeployResult(bool Ok, string Message);

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

    // §2 ─ 내부 메서드 ────────────────────────────────────────

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
