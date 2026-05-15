// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · ConfigStore.cs
//  역할: 스레드 안전 인메모리 설정 저장소 (섹션·키·값 관리)
// ══════════════════════════════════════════════════════════════════════════
using System.Collections.Concurrent;

namespace lssLib.Config;

/// <summary>
/// 스레드 안전 인메모리 설정 저장소.
/// </summary>
/// <remarks>
/// 섹션 → 키 → <see cref="ConfigEntry"/> 의 2단계 Dictionary 구조로 관리됩니다.
/// 모든 키·섹션 비교는 대소문자를 무시합니다.
/// <para>직접 사용보다 <see cref="ConfigManager"/> 를 통한 접근을 권장합니다.</para>
/// <example><code>
/// var store = new ConfigStore();
/// store.Set("Network", "Host", "192.168.1.1");
/// store.Set("Network", "Password", "secret", isEncrypted: true);
///
/// string host = store.Get("Network", "Host") ?? "localhost";
/// bool hasPass = store.Contains("Network", "Password");
/// </code></example>
/// </remarks>
public sealed class ConfigStore
{
    #region §1 ─ 필드

    // section(upper) → key(upper) → entry
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConfigEntry>>
        _data = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    #endregion

    #region §2 ─ 읽기

    /// <summary>
    /// 지정 섹션의 키 값을 반환합니다. 없으면 <see langword="null"/>.
    /// </summary>
    /// <param name="section">섹션 이름 (대소문자 무시).</param>
    /// <param name="key">키 이름 (대소문자 무시).</param>
    public string? Get(string section, string key)
    {
        if (_data.TryGetValue(section, out var sec) &&
            sec.TryGetValue(key, out var entry))
            return entry.Value;
        return null;
    }

    /// <summary>
    /// 키 값을 반환합니다. 없으면 <paramref name="fallback"/> 을 반환합니다.
    /// </summary>
    public string GetOr(string section, string key, string fallback) =>
        Get(section, key) ?? fallback;

    /// <summary>
    /// 키 값을 반환합니다. 없으면 <see cref="InvalidOperationException"/> 을 throw.
    /// </summary>
    public string GetOrThrow(string section, string key) =>
        Get(section, key) ?? throw new InvalidOperationException(
            $"설정 키를 찾을 수 없습니다: [{section}] {key}");

    /// <summary>
    /// 지정 섹션의 <see cref="ConfigEntry"/> 를 반환합니다.
    /// </summary>
    public ConfigEntry? GetEntry(string section, string key)
    {
        if (_data.TryGetValue(section, out var sec) &&
            sec.TryGetValue(key, out var entry))
            return entry;
        return null;
    }

    /// <summary>
    /// 지정 섹션의 모든 <see cref="ConfigEntry"/> 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<ConfigEntry> GetSection(string section)
    {
        if (_data.TryGetValue(section, out var sec))
            return sec.Values.ToList();
        return Array.Empty<ConfigEntry>();
    }

    /// <summary>
    /// 전체 섹션 이름 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<string> GetSections() => _data.Keys.ToList();

    /// <summary>
    /// 지정 섹션이 존재하는지 확인합니다.
    /// </summary>
    public bool HasSection(string section) => _data.ContainsKey(section);

    /// <summary>
    /// 지정 섹션의 키가 존재하는지 확인합니다.
    /// </summary>
    public bool Contains(string section, string key) =>
        _data.TryGetValue(section, out var sec) && sec.ContainsKey(key);

    /// <summary>
    /// 저장된 총 항목 수를 반환합니다.
    /// </summary>
    public int Count => _data.Values.Sum(s => s.Count);

    #endregion

    #region §3 ─ 쓰기

    /// <summary>
    /// 설정 값을 저장합니다. 이미 존재하면 덮어씁니다.
    /// </summary>
    /// <param name="section">섹션 이름.</param>
    /// <param name="key">키 이름.</param>
    /// <param name="value">값 (평문).</param>
    /// <param name="isEncrypted">파일 저장 시 암호화 여부.</param>
    public void Set(string section, string key, string value, bool isEncrypted = false)
    {
        var sec = _data.GetOrAdd(section,
            _ => new ConcurrentDictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase));
        sec[key] = new ConfigEntry(section, key, value, isEncrypted);
    }

    /// <summary>
    /// <see cref="ConfigEntry"/> 를 직접 저장합니다.
    /// </summary>
    public void SetEntry(ConfigEntry entry)
    {
        var sec = _data.GetOrAdd(entry.Section,
            _ => new ConcurrentDictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase));
        sec[entry.Key] = entry;
    }

    /// <summary>
    /// 지정 키를 제거합니다. 제거 성공 여부를 반환합니다.
    /// </summary>
    public bool Remove(string section, string key)
    {
        if (_data.TryGetValue(section, out var sec))
            return sec.TryRemove(key, out _);
        return false;
    }

    /// <summary>
    /// 지정 섹션 전체를 제거합니다.
    /// </summary>
    public bool RemoveSection(string section) =>
        _data.TryRemove(section, out _);

    /// <summary>
    /// 전체 저장소를 초기화합니다.
    /// </summary>
    public void Clear() => _data.Clear();

    #endregion

    #region §4 ─ 병합

    /// <summary>
    /// 다른 <see cref="ConfigStore"/> 의 항목을 현재 저장소에 병합합니다.
    /// 키가 겹치면 <paramref name="overwrite"/> 값에 따라 덮어씁니다.
    /// </summary>
    /// <param name="other">병합 소스 저장소.</param>
    /// <param name="overwrite">키 충돌 시 덮어쓰기 여부. 기본 <see langword="true"/>.</param>
    public void Merge(ConfigStore other, bool overwrite = true)
    {
        foreach (var section in other._data)
        {
            var target = _data.GetOrAdd(section.Key,
                _ => new ConcurrentDictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase));

            foreach (var kv in section.Value)
            {
                if (overwrite || !target.ContainsKey(kv.Key))
                    target[kv.Key] = kv.Value;
            }
        }
    }

    #endregion

    #region §5 ─ 열거

    /// <summary>
    /// 모든 <see cref="ConfigEntry"/> 를 열거합니다.
    /// </summary>
    public IEnumerable<ConfigEntry> GetAll()
    {
        foreach (var sec in _data.Values)
            foreach (var entry in sec.Values)
                yield return entry;
    }

    #endregion
}