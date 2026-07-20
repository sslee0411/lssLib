// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Views/Settings/SettingsView.xaml.cs
//  역할: 환경설정 탭 코드비하인드 — DI로 SettingsViewModel 주입,
//        Loaded 시 monitor.json 로드(InitializeAsync) 수행
//        (CollectorManageView.xaml.cs 와 동일 패턴)
//  C-SET-01 후속 (Monitor)
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.ViewModels;
using System.Windows.Controls;

namespace IIoT.Monitor.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
