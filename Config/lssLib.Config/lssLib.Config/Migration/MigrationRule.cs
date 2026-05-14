// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Migration/MigrationRule.cs
//  역할: 설정 마이그레이션 단일 규칙 값 객체
// ══════════════════════════════════════════════════════════════════════════
namespace lssLib.Config.Migration;

/// <summary>마이그레이션 규칙 유형.</summary>
public enum MigrationRuleType
{
    /// <summary>키 이름 변경 (섹션은 유지).</summary>
    Rename = 0,
    /// <summary>키를 다른 섹션으로 이동 (키 이름 변경 포함 가능).</summary>
    Move = 1,
    /// <summary>키 삭제.</summary>
    Delete = 2,
    /// <summary>키 추가 (이미 존재하면 무시).</summary>
    Add = 3,
    /// <summary>키 값을 변환 함수로 가공.</summary>
    Transform = 4
}

/// <summary>
/// 단일 설정 마이그레이션 규칙 값 객체.
/// </summary>
/// <remarks>
/// 직접 생성하지 않고 <see cref="MigrationRuleBuilder"/> 를 통해 정의합니다.
/// </remarks>
public sealed record MigrationRule(
    MigrationRuleType RuleType,
    string SrcSection,
    string SrcKey,
    string? DstSection = null,
    string? DstKey = null,
    string? DefaultValue = null,
    bool IsEncrypted = false,
    Func<string, string>? Transform = null)
{
    /// <inheritdoc/>
    public override string ToString() => RuleType switch
    {
        MigrationRuleType.Rename => $"Rename   [{SrcSection}]{SrcKey} → {DstKey}",
        MigrationRuleType.Move => $"Move     [{SrcSection}]{SrcKey} → [{DstSection}]{DstKey}",
        MigrationRuleType.Delete => $"Delete   [{SrcSection}]{SrcKey}",
        MigrationRuleType.Add => $"Add      [{DstSection}]{DstKey} = \"{DefaultValue}\"",
        MigrationRuleType.Transform => $"Transform[{SrcSection}]{SrcKey}",
        _ => $"Unknown"
    };
}

/// <summary>
/// <see cref="MigrationRule"/> 목록 빌더.
/// </summary>
/// <remarks>
/// <see cref="ConfigMigration.Register"/> 콜백의 인자로 전달됩니다.
/// <example><code>
/// ConfigMigration.Register("1.0", "2.0", rules =>
/// {
///     rules.Rename("Network", "ServerIP", "Host");
///     rules.Move  ("DB",      "Password", "Credentials", "DbPassword", isEncrypted: true);
///     rules.Delete("Legacy",  "OldKey");
///     rules.Add   ("App",     "Version",  "2.0.0");
///     rules.Transform("Network", "Port", v => (int.Parse(v) + 100).ToString());
/// });
/// </code></example>
/// </remarks>
public sealed class MigrationRuleBuilder
{
    #region §1 ─ 필드

    internal readonly List<MigrationRule> Rules = new();

    #endregion

    #region §2 ─ 빌더 메서드

    /// <summary>같은 섹션에서 키 이름을 변경합니다.</summary>
    public MigrationRuleBuilder Rename(string section, string oldKey, string newKey)
    {
        Rules.Add(new MigrationRule(
            MigrationRuleType.Rename,
            SrcSection: section, SrcKey: oldKey,
            DstSection: section, DstKey: newKey));
        return this;
    }

    /// <summary>키를 다른 섹션으로 이동합니다. 키 이름도 변경 가능.</summary>
    public MigrationRuleBuilder Move(
        string srcSection, string srcKey,
        string dstSection, string dstKey,
        bool isEncrypted = false)
    {
        Rules.Add(new MigrationRule(
            MigrationRuleType.Move,
            SrcSection: srcSection, SrcKey: srcKey,
            DstSection: dstSection, DstKey: dstKey,
            IsEncrypted: isEncrypted));
        return this;
    }

    /// <summary>키를 삭제합니다.</summary>
    public MigrationRuleBuilder Delete(string section, string key)
    {
        Rules.Add(new MigrationRule(
            MigrationRuleType.Delete,
            SrcSection: section, SrcKey: key));
        return this;
    }

    /// <summary>키가 없으면 기본값으로 추가합니다. 이미 존재하면 무시.</summary>
    public MigrationRuleBuilder Add(
        string section, string key, string defaultValue,
        bool isEncrypted = false)
    {
        Rules.Add(new MigrationRule(
            MigrationRuleType.Add,
            SrcSection: section, SrcKey: key,
            DstSection: section, DstKey: key,
            DefaultValue: defaultValue,
            IsEncrypted: isEncrypted));
        return this;
    }

    /// <summary>키 값을 변환 함수로 가공합니다.</summary>
    public MigrationRuleBuilder Transform(
        string section, string key,
        Func<string, string> converter)
    {
        Rules.Add(new MigrationRule(
            MigrationRuleType.Transform,
            SrcSection: section, SrcKey: key,
            Transform: converter));
        return this;
    }

    #endregion
}