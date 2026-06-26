// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/SaveMemoDialog.xaml.cs
//  역할: 저장 메모 입력 다이얼로그
//  S-27: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using System.Windows;

namespace IIoT.Studio.Views;

// §1 ─ 이력 항목 레코드 ───────────────────────────────────

public sealed record SaveHistoryItem(string SavedAt, string Memo);

// §2 ─ 다이얼로그 ─────────────────────────────────────────

public partial class SaveMemoDialog : Window
{
    // §2-1 ─ 결과 프로퍼티 ────────────────────────────────────

    /// <summary>입력된 변경 메모 (저장 클릭 시 설정)</summary>
    public string ResultMemo { get; private set; } = string.Empty;

    // §2-2 ─ 생성자 ───────────────────────────────────────────

    public SaveMemoDialog(
        IEnumerable<SaveHistoryItem> history,
        Window                       owner)
    {
        InitializeComponent();
        Owner = owner;

        // 이력 목록 (최신순 5개)
        HistoryList.ItemsSource = history
            .Take(5)
            .ToList();

        // 입력 포커스
        Loaded += (_, _) => TxtMemo.Focus();
    }

    // §2-3 ─ 이벤트 ───────────────────────────────────────────

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ResultMemo   = TxtMemo.Text?.Trim() ?? string.Empty;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
