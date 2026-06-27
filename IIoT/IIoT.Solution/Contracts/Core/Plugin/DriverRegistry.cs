// ══════════════════════════════════════════════════════════
//  IIoT.Contracts · Core/Plugin/DriverRegistry.cs
//  역할: driverId → IProtocolPlugin 매핑 레지스트리
//        Collector/Studio 가 드라이버를 이름으로 조회하는 단일 창구
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

namespace IIoT.Contracts.Core.Plugin;

/// <summary>
/// 드라이버 플러그인 레지스트리.
/// <para>
/// PluginLoader.LoadAll() 결과를 받아 driverId → IProtocolPlugin 으로 매핑한다.
/// Collector는 PLC 노드의 driverId 로 이 레지스트리를 조회하여 드라이버를 생성한다.
/// Studio는 등록된 플러그인 목록으로 UI 드롭다운을 채운다.
/// </para>
/// <example>
/// <code>
/// // 초기화 (Collector/Studio App.xaml.cs 또는 DI)
/// var loader   = new PluginLoader();
/// var plugins  = loader.LoadAll(onWarning: msg => LogManager.Instance.Warn("Plugin", msg));
/// var registry = new DriverRegistry(plugins);
///
/// // PLC 노드 드라이버 생성
/// if (registry.TryGet("modbus-tcp", out var plugin))
/// {
///     var driver = plugin!.CreateDriver();
///     await driver.ConnectAsync(config, ct);
/// }
/// </code>
/// </example>
/// </summary>
public sealed class DriverRegistry
{
    // §1 ─ 내부 저장소 ─────────────────────────────────────

    /// <summary>driverId(소문자) → IProtocolPlugin 매핑</summary>
    private readonly Dictionary<string, IProtocolPlugin> _map;

    // §2 ─ 생성자 ──────────────────────────────────────────

    /// <param name="plugins">PluginLoader.LoadAll() 반환 목록</param>
    public DriverRegistry(IReadOnlyList<IProtocolPlugin> plugins)
    {
        _map = new Dictionary<string, IProtocolPlugin>(
            StringComparer.OrdinalIgnoreCase);  // 대소문자 무관

        foreach (var plugin in plugins)
        {
            // driverId 규칙: 플러그인 이름을 소문자+하이픈으로 정규화
            // 실제 driverId 는 각 Plugin 구현이 PluginName 으로 제공
            // → PluginName "modbus-tcp" 그대로 Key 로 사용
            var id = _NormalizeId(plugin.PluginName);
            if (!string.IsNullOrEmpty(id))
                _map[id] = plugin;
        }
    }

    // §3 ─ 공개 API ────────────────────────────────────────

    /// <summary>등록된 모든 플러그인 목록.</summary>
    public IReadOnlyCollection<IProtocolPlugin> All => _map.Values;

    /// <summary>등록된 플러그인 수.</summary>
    public int Count => _map.Count;

    /// <summary>
    /// driverId 로 플러그인을 조회합니다.
    /// </summary>
    /// <param name="driverId">조회할 드라이버 ID (예: "modbus-tcp")</param>
    /// <param name="plugin">찾은 플러그인 (없으면 null)</param>
    /// <returns>존재하면 true</returns>
    public bool TryGet(string? driverId, out IProtocolPlugin? plugin)
    {
        plugin = null;
        if (string.IsNullOrWhiteSpace(driverId)) return false;
        return _map.TryGetValue(driverId.Trim(), out plugin);
    }

    /// <summary>
    /// driverId 로 플러그인을 조회합니다. 없으면 null.
    /// </summary>
    public IProtocolPlugin? Get(string? driverId)
        => TryGet(driverId, out var p) ? p : null;

    /// <summary>
    /// driverId 에 해당하는 드라이버 인스턴스를 생성합니다.
    /// 플러그인이 없으면 null.
    /// </summary>
    public IProtocolDriver? CreateDriver(string? driverId)
        => Get(driverId)?.CreateDriver();

    /// <summary>driverId 가 등록되어 있는지 확인합니다.</summary>
    public bool Contains(string? driverId)
        => TryGet(driverId, out _);

    // §4 ─ 내부 헬퍼 ──────────────────────────────────────

    /// <summary>
    /// 플러그인 이름 → driverId 정규화.
    /// "Modbus TCP" → "modbus-tcp" / "미쓰비시 MC" → "미쓰비시-mc"
    /// PluginName이 이미 "modbus-tcp" 형식이면 그대로 사용.
    /// </summary>
    private static string _NormalizeId(string pluginName)
        => pluginName.Trim()
                     .ToLowerInvariant()
                     .Replace(' ', '-');
}
