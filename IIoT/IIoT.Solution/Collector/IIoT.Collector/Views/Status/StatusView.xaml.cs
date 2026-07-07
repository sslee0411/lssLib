// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Status/StatusView.xaml.cs
//  역할: 수집 현황 탭 코드비하인드
//  Col-Base-1: 최소 구현 (레이아웃 뼈대)
//  C-04: StatusViewModel DI 주입 + DataContext 연결
//  C-15: 강제쓰기 버튼 클릭 핸들러 추가 (ForceWriteService DI 주입)
//  생성: 2026-06-29 / 수정: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Engine;
using IIoT.Collector.Models;
using IIoT.Collector.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Status;

public partial class StatusView : UserControl
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly ForceWriteService _forceWriteService;

    // §2 ─ 생성자 ──────────────────────────────────────────

    /// <summary>
    /// ★ DI 생성자.
    /// MainWindow.xaml.cs 에서 DI 컨테이너로부터 StatusViewModel 을 받아
    /// 이 View 를 직접 생성하여 본문 Grid 에 주입한다 (XAML 선언 대신 코드 주입).
    /// </summary>
    public StatusView(StatusViewModel vm, ForceWriteService forceWriteService)
    {
        InitializeComponent();
        DataContext = vm;
        _forceWriteService = forceWriteService;
    }

    // §3 ─ 강제쓰기 (C-15 신규) ────────────────────────────

    /// <summary>
    /// DataGrid "강제쓰기" 버튼 클릭 핸들러.
    /// 클릭된 행의 LiveTagViewModel 을 DataContext 에서 가져와 다이얼로그를 띄우고,
    /// 확인 시 ForceWriteService 를 통해 실제 PLC 에 값을 씁니다.
    /// </summary>
    private async void ForceWriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not LiveTagViewModel tag)
            return;

        var dialog = new ForceWriteDialog(tag.Name, tag.PlcName)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.ResultValue is null)
            return;

        var result = await _forceWriteService.WriteAsync(
            tag.PlcId, tag.TagId, dialog.ResultValue, dialog.ResultApiKey);

        MessageBox.Show(
            Window.GetWindow(this),
            result.IsSuccess
                ? $"'{tag.Name}' 에 값 쓰기 성공."
                : $"쓰기 실패: {result.Error}",
            result.IsSuccess ? "완료" : "오류",
            MessageBoxButton.OK,
            result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
}
