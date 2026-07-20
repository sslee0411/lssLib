// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Settings/SettingsView.xaml.cs
//  역할: 환경설정 탭 코드비하인드
//        ★ Studio 의 기존 DeviceTreeView/CanvasView 등과 동일하게 DataContext 는
//          MainWindow.xaml 에서 DataContext="{Binding Settings}" 로 외부 주입되므로
//          이 UserControl 은 매개변수 없는 생성자만 가진다(Collector/Manager 의
//          DI 생성자 주입 패턴과 다름 — Studio 서브 화면 전반의 기존 관례를 따름).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace IIoT.Studio.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
