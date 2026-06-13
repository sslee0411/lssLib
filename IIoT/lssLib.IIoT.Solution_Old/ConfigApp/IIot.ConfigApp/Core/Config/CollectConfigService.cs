// ══════════════════════════════════════════════════════════
//  IIoT.ConfigApp · Core/Config/CollectConfigService.cs
//  역할: collect.json 원자적 저장 + .signal 발신
//        CanvasViewModel → collect.json → CollectorRuntime 재시작
//  Phase 11: 신규
// ══════════════════════════════════════════════════════════

using IIoT.ConfigApp.ViewModels.Canvas;
using lssLib.Log;
using lssLib.Utils;
using System.IO;
using System.Text;

namespace IIoT.ConfigApp.Core.Config;

/// <summary>
/// collect.json 저장 서비스.
/// CanvasViewModel.SerializeToJson() 결과를 원자적으로 저장하고
/// .signal 파일로 CollectorRuntime 재시작을 트리거합니다.
/// </summary>
public sealed class CollectConfigService
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private const string LogSrc = "CollectConfigService";

    private readonly string _collectPath;
    private readonly string _configDir;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public CollectConfigService(string configDirectory)
    {
        Guard.NotWhiteSpace(configDirectory);
        _configDir   = configDirectory;
        _collectPath = Path.Combine(configDirectory, "collect.json");
    }

    // §3 ─ 저장 ───────────────────────────────────────────────

    /// <summary>
    /// CanvasViewModel 의 현재 상태를 collect.json 으로 저장합니다.
    /// 저장 완료 후 .signal 파일로 CollectorRuntime 재시작을 요청합니다.
    /// </summary>
    public void SaveCanvas(CanvasViewModel canvas)
    {
        Guard.NotNull(canvas);
        var json = canvas.SerializeToJson();
        _AtomicWrite(_collectPath, json);
        _WriteSignal("collect.json", "canvas-save");

        LogManager.Instance.Info(LogSrc,
            $"collect.json 저장 완료 — {canvas.Nodes.Count}개 노드, {canvas.Connections.Count}개 연결");
    }

    /// <summary>collect.json 문자열을 직접 로드합니다.</summary>
    public string? LoadRaw()
    {
        if (!File.Exists(_collectPath))
        {
            LogManager.Instance.Info(LogSrc, "collect.json 없음");
            return null;
        }
        return File.ReadAllText(_collectPath, Encoding.UTF8);
    }

    /// <summary>collect.json 이 존재하는지 확인합니다.</summary>
    public bool Exists => File.Exists(_collectPath);

    // §4 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>원자적 쓰기 (.tmp → rename → .bak)</summary>
    private static void _AtomicWrite(string targetPath, string content)
    {
        var tmp = targetPath + ".tmp";
        var bak = targetPath + ".bak";

        File.WriteAllText(tmp, content, Encoding.UTF8);

        if (File.Exists(targetPath))
            File.Replace(tmp, targetPath, bak);
        else
            File.Move(tmp, targetPath);
    }

    /// <summary>
    /// .signal 파일 생성 → CollectorRuntime FileSystemWatcher 가 감지 → 재시작
    /// ConfigWatcher 패턴과 동일 (Phase 6 확정)
    /// </summary>
    private void _WriteSignal(string targetFile, string reason)
    {
        try
        {
            var signalPath = Path.Combine(_configDir, targetFile + ".signal");
            var content    = $$"""
                {
                  "source":    "IIoT.ConfigApp",
                  "target":    "{{targetFile}}",
                  "reason":    "{{reason}}",
                  "timestamp": "{{DateTime.UtcNow:O}}"
                }
                """;
            File.WriteAllText(signalPath, content, Encoding.UTF8);
            LogManager.Instance.Info(LogSrc,
                $".signal 발신 → CollectorRuntime 재시작 예정");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn(LogSrc, $".signal 생성 실패: {ex.Message}");
        }
    }
}
