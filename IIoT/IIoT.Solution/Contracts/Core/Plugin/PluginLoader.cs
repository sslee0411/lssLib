// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Core/Plugin/PluginLoader.cs
//  역할: Plugins/ 폴더 스캔 → 드라이버 dll 격리 로드
//        실패한 dll은 건너뛰고 나머지 계속 처리
//  CS0103 수정: using System.IO / System.Reflection 명시 추가
//  생성: 2026-06-27 / 수정: 2026-06-27
// ══════════════════════════════════════════════════════════

using System.IO;
using System.Reflection;

namespace IIoT.Contracts.Core.Plugin;

/// <summary>
/// 드라이버 플러그인 로더.
/// <para>
/// Plugins/ 폴더의 모든 dll을 스캔하여 IProtocolPlugin 구현체를 탐색,
/// AssemblyLoadContext 격리 컨텍스트에 로드한다.
/// </para>
/// <para>
/// 로드 실패한 dll은 로그에 경고를 남기고 건너뛴다 (전체 로드 중단 없음).
/// </para>
/// </summary>
public sealed class PluginLoader
{
    // §1 ─ 기본 경로 ───────────────────────────────────────

    /// <summary>기본 플러그인 디렉터리 (실행 파일 옆 Plugins/)</summary>
    public static readonly string DefaultPluginDir =
        Path.Combine(AppContext.BaseDirectory, "Plugins");

    // §2 ─ 로드된 컨텍스트 추적 (언로드용) ────────────────

    private readonly List<WeakReference> _loadedContexts = new();

    // §3 ─ 공개 API ────────────────────────────────────────

    /// <summary>
    /// 지정 폴더의 모든 dll을 스캔하여 IProtocolPlugin 구현체를 로드합니다.
    /// </summary>
    /// <param name="pluginDir">스캔할 폴더 경로 (null = DefaultPluginDir)</param>
    /// <param name="onWarning">로드 경고 콜백 (null = 무시)</param>
    /// <returns>로드 성공한 플러그인 목록 (실패 dll 제외)</returns>
    public IReadOnlyList<IProtocolPlugin> LoadAll(
        string?         pluginDir = null,
        Action<string>? onWarning = null)
    {
        var dir = pluginDir ?? DefaultPluginDir;

        // 폴더 없으면 자동 생성
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            return Array.Empty<IProtocolPlugin>();
        }

        var plugins = new List<IProtocolPlugin>();
        var dlls    = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);

        foreach (var dll in dlls)
        {
            try
            {
                var plugin = _LoadSingle(dll);
                if (plugin is not null)
                    plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                // ★ 로드 실패 → 경고만 남기고 나머지 계속 처리
                onWarning?.Invoke(
                    $"[PluginLoader] 로드 실패: {Path.GetFileName(dll)} — {ex.Message}");
            }
        }

        return plugins;
    }

    // §4 ─ 내부: 단일 dll 로드 ────────────────────────────

    private IProtocolPlugin? _LoadSingle(string dllPath)
    {
        var ctx = new PluginLoadContext(dllPath);
        _loadedContexts.Add(new WeakReference(ctx));  // 언로드 추적

        var asm = ctx.LoadFromAssemblyPath(dllPath);

        // IProtocolPlugin 구현 타입 탐색 (비추상, 비인터페이스)
        var pluginType = asm.GetTypes().FirstOrDefault(t =>
            typeof(IProtocolPlugin).IsAssignableFrom(t)
            && !t.IsAbstract
            && !t.IsInterface);

        if (pluginType is null)
            return null;  // IProtocolPlugin 없는 dll → 건너뜀

        return (IProtocolPlugin)Activator.CreateInstance(pluginType)!;
    }
}
