// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/ConfigBundle.cs
//  역할: JsonConfigLoader.LoadAll() 반환값 컨테이너
//        설정 파일 5종을 한 객체로 묶어 DI / ViewModel 에 전달
//  Phase 1 Update: 신규 추가
// ══════════════════════════════════════════════════════════

using lssLib.Config.Tree;
using IIoT.DeviceManager.Core.DataModel;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// 설정 파일 5종 전체를 담는 컨테이너입니다.
/// JsonConfigLoader.LoadAll() 이 반환하며,
/// ViewModel 과 런타임 서비스에 주입됩니다.
/// </summary>
public sealed class ConfigBundle
{
    // §1 ─ 장비 트리 ──────────────────────────────────────────
    /// <summary>lssLib.Config.ConfigTree — Group/Device/Tag N단계 계층</summary>
    public ConfigTree DeviceTree { get; init; } = new();

    /// <summary>device.json SHA-256 무결성 검증 결과</summary>
    public bool IsIntegrityOk { get; init; } = true;

    // §2 ─ 라이브러리 ─────────────────────────────────────────
    public List<ScaleConfig> Scales { get; init; } = [];
    public List<AlarmRule> AlarmRules { get; init; } = [];
    public List<CommConfig> CommConfigs { get; init; } = [];
    public List<Location> Locations { get; init; } = [];

    // §3 ─ 빠른 조회 딕셔너리 (ID → 항목) ────────────────────
    private Dictionary<string, ScaleConfig>? _scaleMap;
    private Dictionary<string, AlarmRule>? _alarmMap;
    private Dictionary<string, CommConfig>? _commMap;
    private Dictionary<string, Location>? _locationMap;

    public IReadOnlyDictionary<string, ScaleConfig> ScaleMap
        => _scaleMap ??= Scales.ToDictionary(s => s.Id);

    public IReadOnlyDictionary<string, AlarmRule> AlarmMap
        => _alarmMap ??= AlarmRules.ToDictionary(a => a.Id);

    public IReadOnlyDictionary<string, CommConfig> CommMap
        => _commMap ??= CommConfigs.ToDictionary(c => c.Id);

    public IReadOnlyDictionary<string, Location> LocationMap
        => _locationMap ??= Locations.ToDictionary(l => l.Id);

    // §4 ─ 편의 메서드 ────────────────────────────────────────
    public ScaleConfig? FindScale(string? id)
        => id is not null && ScaleMap.TryGetValue(id, out var v) ? v : null;

    public AlarmRule? FindAlarm(string? id)
        => id is not null && AlarmMap.TryGetValue(id, out var v) ? v : null;

    public CommConfig? FindComm(string? id)
        => id is not null && CommMap.TryGetValue(id, out var v) ? v : null;

    public Location? FindLocation(string? id)
        => id is not null && LocationMap.TryGetValue(id, out var v) ? v : null;
}