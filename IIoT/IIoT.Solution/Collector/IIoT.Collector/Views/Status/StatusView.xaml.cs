// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Status/StatusView.xaml.cs
//  역할: 수집 현황 탭 코드비하인드
//  Col-Base-1: 최소 구현 (레이아웃 뼈대)
//  C-04: StatusViewModel DI 주입 + DataContext 연결
//  생성: 2026-06-29 / 수정: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Status;

public partial class StatusView : UserControl
{
    /// <summary>
    /// ★ DI 생성자.
    /// MainWindow.xaml.cs 에서 DI 컨테이너로부터 StatusViewModel 을 받아
    /// 이 View 를 직접 생성하여 본문 Grid 에 주입한다 (XAML 선언 대신 코드 주입).
    /// </summary>
    public StatusView(StatusViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
