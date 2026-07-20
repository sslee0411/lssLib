// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/Settings/SettingsView.xaml.cs
//  역할: 환경설정 탭 코드비하인드 (DI 생성자만 — 민감정보 필드 없어
//        Collector SettingsView 와 달리 PasswordBox 동기화 코드 불필요)
//  생성: 2026-07-20
//  규칙: ★ 기본 생성자 절대 금지 — App.xaml.cs AddSingleton 팩토리 충돌
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows.Controls;

namespace IIoT.Manager.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
