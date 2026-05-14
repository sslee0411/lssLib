// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Profile/ConfigProfile.cs
//  역할: 환경별 설정 프로파일 관리 (base → env → local 오버라이드 계층)
// ══════════════════════════════════════════════════════════════════════════
using System.IO;

namespace lssLib.Config.Profile;

/// <summary>
/// 환경별 설정 프로파일 관리자.
/// </summary>
/// <remarks>
/// Base → 환경(Environment) → 로컬(Local) 3단계 오버라이드 계층을 지원합니다.
/// 뒤에 로드된 파일이 앞의 값을 덮어씁니다.
/// <example><code>
/// // ── 프로파일 등록 ──────────────────────────────────
/// var profiles = new ConfigProfileManager();
///
/// profiles.Define("development",
///     baseFile:  "config/base.json",
///     envFile:   "config/development.json",
///     localFile: "config/local.json",     // optional: true
///     description: "개발 환경");
///
/// profiles.Define("production",
///     baseFile:  "config/base.json",
///     envFile:   "config/production.json",
///     description: "운영 환경");
///
/// // ── 프로파일 활성화 ────────────────────────────────
/// ConfigStore store = profiles.Activate("development");
///
/// // ── ConfigManager 에 적용 ─────────────────────────
/// ConfigManager.Instance.Store.Merge(store);
///
/// // ── 현재 프로파일 조회 ────────────────────────────
/// Console.WriteLine(profiles.ActiveProfile?.Name);  // "development"
/// </code></example>
/// </remarks>
public sealed class ConfigProfileManager
{
    #region §1 ─ 필드

    private readonly Dictionary<string, ConfigProfileDef> _strDefs = new(StringComparer.OrdinalIgnoreCase);
    private ConfigProfileDef? _strActive;

    #endregion

    #region §2 ─ 이벤트

    /// <summary>활성 프로파일이 전환될 때 발생합니다.</summary>
    public event Action<string, ConfigStore>? ProfileSwitched;
    // args: (프로파일 이름, 로드된 ConfigStore)

    #endregion

    #region §3 ─ 프로파일 정의

    /// <summary>
    /// 프로파일을 정의합니다.
    /// </summary>
    /// <param name="name">프로파일 이름 (예: "development", "production").</param>
    /// <param name="baseFile">기본 설정 파일 경로 (필수).</param>
    /// <param name="envFile">환경별 오버라이드 파일 경로 (선택).</param>
    /// <param name="localFile">로컬 오버라이드 파일 경로 (optional — 없으면 무시).</param>
    /// <param name="format">파일 형식. <see langword="null"/> 이면 확장자 자동 감지.</param>
    /// <param name="description">프로파일 설명.</param>
    public ConfigProfileManager Define(
        string name,
        string baseFile,
        string? envFile = null,
        string? localFile = null,
        ConfigFormat? format = null,
        string? description = null)
    {
        _strDefs[name] = new ConfigProfileDef(
            Name: name,
            BaseFile: baseFile,
            EnvFile: envFile,
            LocalFile: localFile,
            Format: format,
            Description: description);
        return this;
    }

    /// <summary>환경 변수 기반 프로파일 이름을 자동 등록합니다.</summary>
    /// <param name="envVarName">프로파일 이름이 담긴 환경 변수. 기본 <c>"APP_ENV"</c>.</param>
    /// <param name="configDir">설정 파일 디렉터리. 기본 <c>"config"</c>.</param>
    /// <param name="baseFileName">기본 파일 이름. 기본 <c>"base.json"</c>.</param>
    public ConfigProfileManager DefineFromEnv(
        string envVarName = "APP_ENV",
        string configDir = "config",
        string baseFileName = "base.json")
    {
        var env = Environment.GetEnvironmentVariable(envVarName) ?? "development";
        Define(
            name: env,
            baseFile: Path.Combine(configDir, baseFileName),
            envFile: Path.Combine(configDir, $"{env}.json"),
            localFile: Path.Combine(configDir, "local.json"));
        return this;
    }

    #endregion

    #region §4 ─ 프로파일 활성화

    /// <summary>
    /// 지정 프로파일을 활성화하고 병합된 <see cref="ConfigStore"/> 를 반환합니다.
    /// </summary>
    /// <param name="name">활성화할 프로파일 이름.</param>
    /// <returns>병합된 설정 저장소.</returns>
    /// <exception cref="KeyNotFoundException">등록되지 않은 프로파일 이름.</exception>
    public ConfigStore Activate(string name)
    {
        if (!_strDefs.TryGetValue(name, out var def))
            throw new KeyNotFoundException(
                $"등록되지 않은 프로파일: '{name}'  " +
                $"(등록됨: {string.Join(", ", _strDefs.Keys)})");

        var store = LoadProfile(def);
        _strActive = def;
        ProfileSwitched?.Invoke(name, store);
        return store;
    }

    /// <summary>
    /// 환경 변수에서 프로파일 이름을 읽어 자동 활성화합니다.
    /// </summary>
    /// <param name="envVarName">환경 변수 이름. 기본 <c>"APP_ENV"</c>.</param>
    /// <param name="fallback">환경 변수 없을 때 기본 프로파일 이름. 기본 <c>"development"</c>.</param>
    public ConfigStore ActivateFromEnv(
        string envVarName = "APP_ENV",
        string fallback = "development")
    {
        var name = Environment.GetEnvironmentVariable(envVarName) ?? fallback;
        return Activate(name);
    }

    #endregion

    #region §5 ─ 상태 조회

    /// <summary>현재 활성 프로파일 정의. 활성화 전이면 <see langword="null"/>.</summary>
    public ConfigProfileDef? ActiveProfile => _strActive;

    /// <summary>등록된 모든 프로파일 이름 목록.</summary>
    public IReadOnlyList<string> ProfileNames => _strDefs.Keys.ToList();

    /// <summary>지정 이름의 프로파일이 등록되어 있는지 확인합니다.</summary>
    public bool HasProfile(string name) => _strDefs.ContainsKey(name);

    #endregion

    #region §6 ─ 내부 헬퍼

    private static ConfigStore LoadProfile(ConfigProfileDef def)
    {
        var merged = new ConfigStore();

        // Layer 1 — Base (필수)
        LoadFile(merged, def.BaseFile, def.Format, optional: false);

        // Layer 2 — Env override (선택)
        if (def.EnvFile is not null)
            LoadFile(merged, def.EnvFile, def.Format, optional: true);

        // Layer 3 — Local override (항상 선택)
        if (def.LocalFile is not null)
            LoadFile(merged, def.LocalFile, def.Format, optional: true);

        // 프로파일 메타 기록
        merged.Set("Profile", "Name", def.Name);
        merged.Set("Profile", "LoadedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return merged;
    }

    private static void LoadFile(ConfigStore target, string path, ConfigFormat? fmt, bool optional)
    {
        if (!File.Exists(path))
        {
            if (!optional)
                throw new FileNotFoundException($"프로파일 기본 파일을 찾을 수 없습니다: {path}", path);
            return;
        }

        // ConfigManager 싱글톤을 통해 로드 후 Store만 추출
        var temp = ConfigManager.CreateNew();
        temp.Load(path, fmt);
        target.Merge(temp.Store, overwrite: true);
    }

    #endregion
}

// ── ConfigProfileDef ──────────────────────────────────────────────────────

/// <summary>프로파일 정의 값 객체.</summary>
public sealed record ConfigProfileDef(
    string Name,
    string BaseFile,
    string? EnvFile = null,
    string? LocalFile = null,
    ConfigFormat? Format = null,
    string? Description = null)
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"Profile[{Name}]  base={Path.GetFileName(BaseFile)}" +
        (EnvFile is not null ? $"  env={Path.GetFileName(EnvFile)}" : "") +
        (LocalFile is not null ? $"  local={Path.GetFileName(LocalFile)}" : "");
}