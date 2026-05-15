// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Migration/ConfigMigration.cs
//  역할: 설정 파일 버전 간 마이그레이션 등록·실행 정적 헬퍼
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Migration;

/// <summary>
/// 설정 파일 버전 간 마이그레이션 실행기.
/// </summary>
/// <remarks>
/// 버전 키는 설정 파일 내 <c>[Meta]</c> 섹션의 <c>Version</c> 키로 관리됩니다.
/// 등록된 마이그레이션 경로를 따라 순차적으로 적용하며, 완료 후 버전 키를 갱신합니다.
/// <example><code>
/// // ── 마이그레이션 규칙 등록 ─────────────────────────
/// ConfigMigration.Register("1.0", "2.0", rules =>
/// {
///     rules.Rename("Network", "ServerIP",  "Host");
///     rules.Move  ("DB",      "Password",  "Credentials", "DbPassword", isEncrypted: true);
///     rules.Delete("Legacy",  "OldFlag");
///     rules.Add   ("App",     "LogLevel",  "Info");
/// });
///
/// ConfigMigration.Register("2.0", "3.0", rules =>
/// {
///     rules.Transform("Network", "Port",   v => (int.Parse(v) + 100).ToString());
///     rules.Add      ("Monitor", "Enabled","true");
/// });
///
/// // ── 마이그레이션 실행 ──────────────────────────────
/// MigrationReport report = ConfigMigration.Migrate(
///     store,
///     currentVersion: "1.0",
///     targetVersion:  "3.0");
///
/// Console.WriteLine(report);
/// // 마이그레이션 완료: 1.0 → 3.0  (2단계 / 6개 규칙 적용)
/// </code></example>
/// </remarks>
public static class ConfigMigration
{
    #region §1 ─ 등록 저장소

    // (fromVersion, toVersion) → 규칙 목록
    private static readonly Dictionary<(string From, string To), List<MigrationRule>>
        _strRegistry = new();

    #endregion

    #region §2 ─ 등록 API

    /// <summary>
    /// 버전 간 마이그레이션 규칙을 등록합니다.
    /// </summary>
    /// <param name="fromVersion">원본 버전 문자열.</param>
    /// <param name="toVersion">대상 버전 문자열.</param>
    /// <param name="configure">규칙 빌더 콜백.</param>
    public static void Register(
        string fromVersion,
        string toVersion,
        Action<MigrationRuleBuilder> configure)
    {
        var builder = new MigrationRuleBuilder();
        configure(builder);
        _strRegistry[(fromVersion, toVersion)] = builder.Rules;
    }

    /// <summary>등록된 마이그레이션 경로 목록을 반환합니다.</summary>
    public static IReadOnlyList<(string From, string To)> RegisteredPaths =>
        _strRegistry.Keys.ToList();

    /// <summary>등록된 모든 규칙을 초기화합니다 (테스트 용도).</summary>
    public static void ClearAll() => _strRegistry.Clear();

    #endregion

    #region §3 ─ 실행 API

    /// <summary>
    /// 등록된 마이그레이션 경로를 따라 <paramref name="store"/> 를 변환합니다.
    /// </summary>
    /// <param name="store">변환 대상 설정 저장소.</param>
    /// <param name="currentVersion">현재 버전.</param>
    /// <param name="targetVersion">목표 버전.</param>
    /// <returns>실행 결과 <see cref="MigrationReport"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// 현재 버전에서 목표 버전까지의 경로를 찾을 수 없는 경우.
    /// </exception>
    public static MigrationReport Migrate(
        ConfigStore store,
        string currentVersion,
        string targetVersion)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (currentVersion == targetVersion)
            return MigrationReport.NoOp(currentVersion);

        // BFS로 버전 경로 탐색
        var path = FindPath(currentVersion, targetVersion);
        if (path is null)
            throw new InvalidOperationException(
                $"마이그레이션 경로를 찾을 수 없습니다: {currentVersion} → {targetVersion}");

        var appliedSteps = new List<MigrationStep>();
        var strCurrentVer = currentVersion;

        // 각 단계 순서대로 적용
        for (int i = 0; i < path.Count - 1; i++)
        {
            var from = path[i];
            var to = path[i + 1];
            var rules = _strRegistry[(from, to)];

            var applied = ApplyStep(store, rules);
            appliedSteps.Add(new MigrationStep(from, to, applied));
            strCurrentVer = to;
        }

        // 버전 키 갱신
        store.Set("Meta", "Version", targetVersion);
        store.Set("Meta", "MigratedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        store.Set("Meta", "PreviousVersion", currentVersion);

        return new MigrationReport(
            FromVersion: currentVersion,
            ToVersion: targetVersion,
            Steps: appliedSteps);
    }

    /// <summary>
    /// 설정 파일에서 버전을 읽어 자동 마이그레이션합니다.
    /// </summary>
    /// <param name="store">대상 저장소.</param>
    /// <param name="targetVersion">목표 버전.</param>
    /// <param name="versionSection">버전 키가 있는 섹션. 기본 <c>"Meta"</c>.</param>
    /// <param name="versionKey">버전 키 이름. 기본 <c>"Version"</c>.</param>
    public static MigrationReport MigrateAuto(
        ConfigStore store,
        string targetVersion,
        string versionSection = "Meta",
        string versionKey = "Version")
    {
        var strCurrentVer = store.Get(versionSection, versionKey) ?? "1.0";
        return Migrate(store, strCurrentVer, targetVersion);
    }

    #endregion

    #region §4 ─ 내부 헬퍼

    // 각 단계 적용 → 적용된 규칙 목록 반환
    private static List<MigrationRule> ApplyStep(
        ConfigStore store, List<MigrationRule> rules)
    {
        var applied = new List<MigrationRule>();

        foreach (var rule in rules)
        {
            bool ok = rule.RuleType switch
            {
                MigrationRuleType.Rename => ApplyRename(store, rule),
                MigrationRuleType.Move => ApplyMove(store, rule),
                MigrationRuleType.Delete => ApplyDelete(store, rule),
                MigrationRuleType.Add => ApplyAdd(store, rule),
                MigrationRuleType.Transform => ApplyTransform(store, rule),
                _ => false
            };
            if (ok) applied.Add(rule);
        }
        return applied;
    }

    private static bool ApplyRename(ConfigStore store, MigrationRule rule)
    {
        var val = store.Get(rule.SrcSection, rule.SrcKey);
        if (val is null) return false;
        var entry = store.GetEntry(rule.SrcSection, rule.SrcKey)!;
        store.Set(rule.DstSection!, rule.DstKey!, val, entry.IsEncrypted);
        store.Remove(rule.SrcSection, rule.SrcKey);
        return true;
    }

    private static bool ApplyMove(ConfigStore store, MigrationRule rule)
    {
        var val = store.Get(rule.SrcSection, rule.SrcKey);
        if (val is null) return false;
        store.Set(rule.DstSection!, rule.DstKey!, val, rule.IsEncrypted);
        store.Remove(rule.SrcSection, rule.SrcKey);
        return true;
    }

    private static bool ApplyDelete(ConfigStore store, MigrationRule rule) =>
        store.Remove(rule.SrcSection, rule.SrcKey);

    private static bool ApplyAdd(ConfigStore store, MigrationRule rule)
    {
        if (store.Contains(rule.DstSection!, rule.DstKey!)) return false;
        store.Set(rule.DstSection!, rule.DstKey!, rule.DefaultValue ?? string.Empty,
                  rule.IsEncrypted);
        return true;
    }

    private static bool ApplyTransform(ConfigStore store, MigrationRule rule)
    {
        var val = store.Get(rule.SrcSection, rule.SrcKey);
        if (val is null || rule.Transform is null) return false;
        var entry = store.GetEntry(rule.SrcSection, rule.SrcKey)!;
        var transformed = rule.Transform(val);
        store.Set(rule.SrcSection, rule.SrcKey, transformed, entry.IsEncrypted);
        return true;
    }

    // BFS 경로 탐색
    private static List<string>? FindPath(string from, string to)
    {
        // 직접 경로
        if (_strRegistry.ContainsKey((from, to))) return new List<string> { from, to };

        var visited = new HashSet<string> { from };
        var queue = new Queue<List<string>>();
        queue.Enqueue(new List<string> { from });

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var cur = path[^1];

            foreach (var key in _strRegistry.Keys.Where(k => k.From == cur))
            {
                if (visited.Contains(key.To)) continue;
                var newPath = new List<string>(path) { key.To };
                if (key.To == to) return newPath;
                visited.Add(key.To);
                queue.Enqueue(newPath);
            }
        }
        return null;
    }

    #endregion
}

// ── MigrationStep / MigrationReport ──────────────────────────────────────

/// <summary>단일 마이그레이션 단계 결과.</summary>
public sealed record MigrationStep(
    string FromVersion,
    string ToVersion,
    List<MigrationRule> AppliedRules);

/// <summary>전체 마이그레이션 실행 결과.</summary>
public sealed class MigrationReport
{
    /// <summary>원본 버전.</summary>
    public string FromVersion { get; init; } = string.Empty;

    /// <summary>최종 버전.</summary>
    public string ToVersion { get; init; } = string.Empty;

    /// <summary>적용된 단계 목록.</summary>
    public IReadOnlyList<MigrationStep> Steps { get; init; } = Array.Empty<MigrationStep>();

    /// <summary>적용된 총 규칙 수.</summary>
    public int TotalRulesApplied => Steps.Sum(s => s.AppliedRules.Count);

    /// <summary>실제 변환이 있었는지 여부.</summary>
    public bool HasChanges => Steps.Count > 0 && TotalRulesApplied > 0;

    public MigrationReport()
    {
    }

    public MigrationReport(string FromVersion, string ToVersion, List<MigrationStep> Steps)
    {
        this.FromVersion = FromVersion;
        this.ToVersion = ToVersion;
        this.Steps = Steps;
    }

    /// <summary>변경 없이 완료된 결과를 생성합니다.</summary>
    internal static MigrationReport NoOp(string version) => new()
    {
        FromVersion = version,
        ToVersion = version,
        Steps = Array.Empty<MigrationStep>()
    };

    /// <inheritdoc/>
    public override string ToString() =>
        HasChanges
            ? $"마이그레이션 완료: {FromVersion} → {ToVersion}  " +
              $"({Steps.Count}단계 / {TotalRulesApplied}개 규칙 적용)"
            : $"마이그레이션 불필요: 이미 최신 버전({FromVersion})";
}