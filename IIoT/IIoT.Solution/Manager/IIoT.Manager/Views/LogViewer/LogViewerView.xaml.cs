// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/LogViewer/LogViewerView.xaml.cs
//  역할: [로그] 탭 코드비하인드 — DataContext 주입 + 자동 스크롤
//  MG-04: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using IIoT.Manager.Models;
using IIoT.Manager.ViewModels;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace IIoT.Manager.Views.LogViewer;

public partial class LogViewerView : UserControl
{
    // §1 ─ 생성자 ─────────────────────────────────────────────

    public LogViewerView(LogViewerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // ★ 자동 스크롤: 신규 행 추가 시 맨 아래로 (일시정지 중엔 추가 자체가 없음)
        vm.Rows.CollectionChanged += _OnRowsChanged;
    }

    // §2 ─ 내부 메서드 ────────────────────────────────────────

    private void _OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (LogList.Items.Count == 0) return;

        // 필터로 마지막 항목이 안 보일 수 있으므로 뷰 기준 마지막으로 스크롤
        var last = LogList.Items[LogList.Items.Count - 1];
        if (last is LogRow)
            LogList.ScrollIntoView(last);
    }
}
