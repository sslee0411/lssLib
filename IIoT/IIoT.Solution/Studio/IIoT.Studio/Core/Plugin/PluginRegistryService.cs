// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Plugin/PluginRegistryService.cs
//  역할: Studio 시작 시 Plugins/ 폴더 스캔 → 드라이버 등록
//        PlcEditorView 드롭다운 · 동적 파라미터 폼에 사용
//  Studio-P01: 초기 구현
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using IIoT.Contracts.Core.Plugin;
using lssLib.Log;

namespace IIoT.Studio.Core.Plugin;

/// <summary>
/// 드라이버 플러그인 레지스트리 서비스 (Studio 전용, DI 싱글턴).
/// <para>
/// App.xaml.cs OnStartup 에서 <c>LoadPlugins()</c> 를 호출하면
/// Plugins/ 폴더를 스캔하여 등록한다.
/// PlcEditorView 는 이 서비스를 통해 드라이버 목록과 파라미터 스키마를 조회한다.
/// </para>
/// </summary>
public sealed class PluginRegistryService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly PluginLoader    _loader   = new();
    private          DriverRegistry? _registry;

    // §2 ─ 공개 프로퍼티 ───────────────────────────────────

    /// <summary>로드된 플러그인 수. 초기화 전에는 0.</summary>
    public int PluginCount => _registry?.Count ?? 0;

    /// <summary>
    /// 등록된 모든 플러그인 목록.
    /// PlcEditorView 드라이버 선택 드롭다운에 사용.
    /// </summary>
    public IReadOnlyCollection<IProtocolPlugin> Plugins
        => _registry?.All ?? Array.Empty<IProtocolPlugin>();

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// Plugins/ 폴더를 스캔하여 드라이버를 로드합니다.
    /// App.xaml.cs OnStartup 에서 호출합니다.
    /// </summary>
    /// <param name="pluginDir">
    ///   스캔 폴더 (null = 실행파일 옆 Plugins/).
    ///   개발 시 bin/Debug/net8.0-windows/Plugins/ 에 dll 복사 후 테스트.
    /// </param>
    public void LoadPlugins(string? pluginDir = null)
    {
        var plugins = _loader.LoadAll(
            pluginDir,
            onWarning: msg => LogManager.Instance.Warn("PluginRegistry", msg));

        _registry = new DriverRegistry(plugins);

        LogManager.Instance.Info("PluginRegistry",
            $"드라이버 플러그인 로드 완료: {_registry.Count}개");

        foreach (var p in _registry.All)
            LogManager.Instance.Info("PluginRegistry",
                $"  [{p.PluginName}] v{p.PluginVersion} — {p.SupportedVendor}");
    }

    // §4 ─ 드라이버 조회 API ───────────────────────────────

    /// <summary>
    /// driverId 로 플러그인을 조회합니다.
    /// </summary>
    public IProtocolPlugin? GetPlugin(string? driverId)
        => _registry?.Get(driverId);

    /// <summary>
    /// driverId 에 해당하는 파라미터 스키마를 반환합니다.
    /// PlcEditorView 동적 폼 렌더링에 사용.
    /// driverId 가 없거나 미등록 드라이버면 빈 목록 반환.
    /// </summary>
    public IReadOnlyList<ParameterDefinition> GetSchema(string? driverId)
        => GetPlugin(driverId)?.GetParameterSchema()
           ?? Array.Empty<ParameterDefinition>();

    /// <summary>driverId 가 등록된 드라이버인지 확인합니다.</summary>
    public bool IsKnownDriver(string? driverId)
        => _registry?.Contains(driverId) ?? false;

    /// <summary>
    /// 플러그인 이름(PluginName) 목록을 반환합니다.
    /// ComboBox ItemsSource 에 직접 바인딩할 수 있습니다.
    /// </summary>
    public IEnumerable<string> GetDriverIds()
        => Plugins.Select(p => p.PluginName);
}
