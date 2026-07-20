// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Views/RemoteSettings/RemoteSettingsView.xaml.cs
//  역할: [원격 설정] 탭 코드비하인드 (DI 생성자만 — SettingsView.xaml.cs 와
//        동일 패턴, 별도 초기화 불필요: 파일 I/O 가 아니라 사용자가 버튼을
//        눌렀을 때만 NamedPipe 호출이 발생하므로 Loaded 훅이 필요 없다)
//  HM-22: 신규
//  생성: 2026-07-20
//  규칙: ★ 기본 생성자 절대 금지 — App.xaml.cs AddSingleton 팩토리 충돌
// ══════════════════════════════════════════════════════════

using IIoT.Manager.ViewModels;
using System.Windows.Controls;

namespace IIoT.Manager.Views.RemoteSettings;

public partial class RemoteSettingsView : UserControl
{
    public RemoteSettingsView(RemoteSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
