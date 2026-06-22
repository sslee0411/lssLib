// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/ValidationErrorDialog.xaml.cs
//  역할: 유효성 검사 결과 팝업 코드비하인드
//  S-16: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Config;
using IIoT.Studio.ViewModels;
using System.Windows;

namespace IIoT.Studio.Views;

public partial class ValidationErrorDialog : Window
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly List<ValidationIssue> _issues;
    private readonly DeviceTreeViewModel   _treeVm;

    /// <summary>저장 버튼 클릭 여부 (true = 저장 진행)</summary>
    public bool ShouldSave { get; private set; }

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public ValidationErrorDialog(
        List<ValidationIssue> issues,
        DeviceTreeViewModel   treeVm,
        Window                owner)
    {
        InitializeComponent();
        Owner   = owner;
        _issues = issues;
        _treeVm = treeVm;

        _Init();
    }

    // §3 ─ 초기화 ─────────────────────────────────────────────

    private void _Init()
    {
        var errorCount   = _issues.Count(i => i.Severity == ValidationSeverity.Error);
        var warningCount = _issues.Count(i => i.Severity == ValidationSeverity.Warning);

        // 요약 텍스트
        TxtSummary.Text = errorCount > 0
            ? $"❌ 오류 {errorCount}개  ⚠ 경고 {warningCount}개"
            : $"⚠ 경고 {warningCount}개";

        // 오류가 있으면 [✔ 저장] 버튼 비활성 (체크박스로 해제 가능)
        BtnSave.IsEnabled = errorCount == 0;

        // 경고만 있을 때는 저장 버튼 항상 활성
        ChkIgnoreWarnings.Visibility =
            errorCount > 0 ? Visibility.Collapsed : Visibility.Visible;

        // 오류 먼저, 경고 나중 정렬
        var sorted = _issues
            .OrderBy(i => i.Severity == ValidationSeverity.Error ? 0 : 1)
            .ToList();

        IssueList.ItemsSource = sorted;
    }

    // §4 ─ 이벤트 ─────────────────────────────────────────────

    /// <summary>항목 더블클릭 → 해당 노드 트리에서 선택</summary>
    private void OnIssueDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IssueList.SelectedItem is ValidationIssue issue
            && issue.Node is not null)
        {
            _treeVm.SelectNode(issue.Node);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ShouldSave = true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        ShouldSave   = false;
        DialogResult = false;
    }
}
