// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainWindow.xaml.cs
//  Fix: namespace "IIot.Studio" → "IIoT.Studio" (대소문자)
//       기본 생성자 제거 → ViewModel 주입 생성자 (WPF 규칙 6)
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
using IIoT.Studio.ViewModels.DeviceTree;
using System.Windows;

namespace IIoT.Studio;   // ★ Fix: IIot → IIoT

public partial class MainWindow : Window
{
    // ★ 규칙 6: 기본 생성자 제거 → ViewModel 주입 생성자 필수
    public MainWindow(StudioMainViewModel vm, DeviceTreeViewModel tree)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
