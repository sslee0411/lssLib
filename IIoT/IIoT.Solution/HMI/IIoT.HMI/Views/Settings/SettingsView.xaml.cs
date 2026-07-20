// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/Settings/SettingsView.xaml.cs
//  역할: 환경설정 탭 코드비하인드 — DI로 SettingsViewModel 주입,
//        Loaded 시 hmi.json 로드(InitializeAsync) 수행
//        (Monitor Views/Settings/SettingsView.xaml.cs 와 동일 패턴)
//  C-SET-01 후속 (HMI)
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.HMI.ViewModels;
using System.Windows.Controls;

namespace IIoT.HMI.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
