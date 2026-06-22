// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/ValidationService.cs
//  역할: 저장 전 설정 유효성 검사
//  S-16: 신규
//  검사 항목:
//    [오류] PLC/장비 IP 주소 비어있음
//    [오류] 동일 PLC 내 Tag 주소 중복
//    [경고] 스케일 RawMin >= RawMax (역전값)
//    [경고] Tag 이름 비어있음
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.Collections.ObjectModel;

namespace IIoT.Studio.Core.Config;

// §1 ─ 결과 타입 ──────────────────────────────────────────

public enum ValidationSeverity { Error, Warning }

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string             Message,
    AbstractTreeNode?  Node        // 클릭 시 해당 노드 선택용
)
{
    public string SeverityLabel => Severity == ValidationSeverity.Error
        ? "❌ 오류" : "⚠ 경고";

    public string NodePath { get; init; } = string.Empty;
}

// §2 ─ 서비스 ─────────────────────────────────────────────

public sealed class ValidationService
{
    private readonly DeviceTreeViewModel   _treeVm;
    private readonly ScaleLibraryViewModel _scaleVm;

    public ValidationService(
        DeviceTreeViewModel   treeVm,
        ScaleLibraryViewModel scaleVm)
    {
        _treeVm  = treeVm;
        _scaleVm = scaleVm;
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>전체 유효성 검사 실행. 오류·경고 목록 반환.</summary>
    public List<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        _ValidateTree(_treeVm.RootNodes, issues);
        _ValidateScaleLibrary(issues);

        return issues;
    }

    public bool HasErrors(List<ValidationIssue> issues) =>
        issues.Any(i => i.Severity == ValidationSeverity.Error);

    // §4 ─ 트리 검사 ──────────────────────────────────────────

    private void _ValidateTree(
        IEnumerable<AbstractTreeNode> nodes,
        List<ValidationIssue>         issues,
        string                        parentPath = "")
    {
        foreach (var node in nodes)
        {
            var path = string.IsNullOrEmpty(parentPath)
                ? node.Name : $"{parentPath} > {node.Name}";

            switch (node)
            {
                case GroupTreeNode g:
                    _ValidateGroup(g, path, issues);
                    break;

                case DeviceTreeNode d:
                    _ValidateDevice(d, path, issues);
                    break;

                case PlcTreeNode p:
                    _ValidatePlc(p, path, issues);
                    break;

                case TagTreeNode t:
                    _ValidateTag(t, path, issues);
                    break;
            }

            // PLC 하위 Tag 중복 주소 검사
            if (node is PlcTreeNode plc)
                _ValidateDuplicateAddress(plc, path, issues);

            // 재귀
            _ValidateTree(node.Children, issues, path);
        }
    }

    // §4-1 ─ 그룹 ──────────────────────────────────────────────

    private static void _ValidateGroup(
        GroupTreeNode g, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(g.Name))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                $"그룹 이름이 비어 있습니다.",
                g) { NodePath = path });
    }

    // §4-2 ─ 장비 ──────────────────────────────────────────────

    private static void _ValidateDevice(
        DeviceTreeNode d, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(d.Name))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                $"장비 이름이 비어 있습니다.",
                d) { NodePath = path });

        if (d.CommType != NodeCommType.None
            && string.IsNullOrWhiteSpace(d.Host))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"[{path}] 통신 방식이 설정됐지만 IP/주소가 비어 있습니다.",
                d) { NodePath = path });
    }

    // §4-3 ─ PLC ───────────────────────────────────────────────

    private static void _ValidatePlc(
        PlcTreeNode p, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                $"PLC 이름이 비어 있습니다.",
                p) { NodePath = path });

        if (string.IsNullOrWhiteSpace(p.Host))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"[{path}] IP 주소가 비어 있습니다.",
                p) { NodePath = path });

        if (p.Port <= 0 || p.Port > 65535)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"[{path}] 포트 번호가 유효하지 않습니다. (1~65535)",
                p) { NodePath = path });

        if (p.PollMs < 10)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                $"[{path}] 폴링 주기가 10ms 미만입니다. 통신 부하가 발생할 수 있습니다.",
                p) { NodePath = path });
    }

    // §4-4 ─ Tag ───────────────────────────────────────────────

    private static void _ValidateTag(
        TagTreeNode t, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(t.Name))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                $"Tag 이름이 비어 있습니다.",
                t) { NodePath = path });

        if (string.IsNullOrWhiteSpace(t.Address))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"[{path}] Tag 주소가 비어 있습니다.",
                t) { NodePath = path });

        if (!string.IsNullOrWhiteSpace(t.Address)
            && !int.TryParse(t.Address, out _))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"[{path}] Tag 주소가 숫자가 아닙니다: '{t.Address}'",
                t) { NodePath = path });
    }

    // §4-5 ─ PLC 내 Tag 중복 주소 ─────────────────────────────

    private static void _ValidateDuplicateAddress(
        PlcTreeNode plc, string path, List<ValidationIssue> issues)
    {
        var tags = plc.Children.OfType<TagTreeNode>().ToList();
        var seen = new Dictionary<string, TagTreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Address)) continue;

            if (seen.TryGetValue(tag.Address, out var first))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"[{path}] Tag 주소 중복: '{tag.Address}'" +
                    $" ('{first.Name}' 와 '{tag.Name}')",
                    tag) { NodePath = $"{path} > {tag.Name}" });
            }
            else
            {
                seen[tag.Address] = tag;
            }
        }
    }

    // §5 ─ 스케일 라이브러리 검사 ─────────────────────────────

    private void _ValidateScaleLibrary(List<ValidationIssue> issues)
    {
        foreach (var entry in _scaleVm.Entries)
        {
            if (entry.Mode == ScaleMode.Linear && entry.RawMin >= entry.RawMax)
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"스케일 '{entry.Name}': RawMin({entry.RawMin}) ≥ RawMax({entry.RawMax}) — 역전값입니다.",
                    null) { NodePath = $"스케일 > {entry.Name}" });

            if (entry.Mode == ScaleMode.Linear && entry.EngMin >= entry.EngMax)
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"스케일 '{entry.Name}': EngMin({entry.EngMin}) ≥ EngMax({entry.EngMax}) — 역전값입니다.",
                    null) { NodePath = $"스케일 > {entry.Name}" });
        }
    }
}
