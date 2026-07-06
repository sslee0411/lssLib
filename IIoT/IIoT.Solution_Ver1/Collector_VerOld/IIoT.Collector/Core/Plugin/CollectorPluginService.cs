// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Plugin/CollectorPluginService.cs
//  역할: Collector 시작 시 Plugins/ 폴더 스캔 → 드라이버 등록
//        Studio의 PluginRegistryService 와 동일 패턴
//        C-01 이후: FlowEngine 이 DriverRegistry 를 통해 드라이버 생성
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using IIoT.Contracts.Core.Plugin;
using lssLib.Log;

namespace IIoT.Collector.Core.Plugin;

/// <summary>
/// Collector 드라이버 플러그인 레지스트리 서비스 (DI 싱글턴).
/// <para>
/// App.xaml.cs Loaded 이벤트에서 <c>LoadPlugins()</c> 를 호출하면
/// Plugins/ 폴더를 스캔하여 IProtocolPlugin 구현체를 자동 등록한다.
/// C-01 이후 FlowEngine 이 이 서비스를 통해 드라이버를 조회·생성한다.
/// </para>
/// </summary>
public sealed class CollectorPluginService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly PluginLoader    _loader   = new();
    private          DriverRegistry? _registry;

    // §2 ─ 공개 프로퍼티 ───────────────────────────────────

    /// <summary>로드된 플러그인 수. 초기화 전에는 0.</summary>
    public int PluginCount => _registry?.Count ?? 0;

    /// <summary>
    /// 등록된 모든 플러그인 목록.
    /// C-01 이후 FlowEngine 드라이버 드롭다운에 사용.
    /// </summary>
    public IReadOnlyCollection<IProtocolPlugin> Plugins
        => _registry?.All ?? Array.Empty<IProtocolPlugin>();

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// Plugins/ 폴더를 스캔하여 드라이버를 로드합니다.
    /// App.xaml.cs win.Loaded 이벤트에서 호출합니다.
    /// </summary>
    /// <param name="pluginDir">
    ///   스캔 폴더 (null = 실행파일 옆 Plugins/).
    ///   개발 시 bin/Debug/net8.0-windows/Plugins/ 에 dll 복사 후 테스트.
    /// </param>
    public void LoadPlugins(string? pluginDir = null)
    {
        var plugins = _loader.LoadAll(
            pluginDir,
            onWarning: msg => LogManager.Instance.Warn("CollectorPlugin", msg));

        _registry = new DriverRegistry(plugins);

        LogManager.Instance.Info("CollectorPlugin",
            $"드라이버 플러그인 로드 완료: {_registry.Count}개");

        foreach (var p in _registry.All)
            LogManager.Instance.Info("CollectorPlugin",
                $"  [{p.PluginName}] v{p.PluginVersion} — {p.SupportedVendor}");

        // Plugins/ 폴더가 없을 때 안내 메시지
        if (_registry.Count == 0)
            LogManager.Instance.Info("CollectorPlugin",
                "Plugins/ 폴더가 비어 있음 — C-02 에서 IIoT.Driver.Virtual 추가 예정");
    }

    // §4 ─ 드라이버 조회 API ───────────────────────────────

    /// <summary>driverId 로 플러그인을 조회합니다.</summary>
    public IProtocolPlugin? GetPlugin(string? driverId)
        => _registry?.Get(driverId);

    /// <summary>driverId 에 해당하는 파라미터 스키마를 반환합니다.</summary>
    public IReadOnlyList<ParameterDefinition> GetSchema(string? driverId)
        => GetPlugin(driverId)?.GetParameterSchema()
           ?? Array.Empty<ParameterDefinition>();

    /// <summary>driverId 가 등록된 드라이버인지 확인합니다.</summary>
    public bool IsKnownDriver(string? driverId)
        => _registry?.Contains(driverId) ?? false;

    /// <summary>
    /// driverId 에 해당하는 드라이버 인스턴스를 생성합니다.
    /// C-01 FlowEngine 에서 PLC 노드별 드라이버 생성 시 호출.
    /// </summary>
    public IProtocolDriver? CreateDriver(string? driverId)
        => _registry?.CreateDriver(driverId);

    /// <summary>등록된 플러그인 이름 목록 (드라이버 선택 목록용).</summary>
    public IReadOnlyList<string> GetPluginNames()
        => Plugins.Select(p => p.PluginName).ToList();
}
