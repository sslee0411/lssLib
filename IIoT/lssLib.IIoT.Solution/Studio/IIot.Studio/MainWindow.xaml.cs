// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainWindow.xaml.cs
//  Fix:
//    ① namespace "IIot.Studio" → "IIoT.Studio"
//    ② 기본 생성자 제거 → ViewModel 주입 생성자 (WPF 규칙 6)
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
using IIoT.Studio.ViewModels.DeviceTree;
using System.Windows;

namespace IIoT.Studio;   // ★ Fix ①: IIot → IIoT

public partial class MainWindow : Window
{
    // ★ Fix ②: 기본 생성자 제거, ViewModel 주입
    public MainWindow(StudioMainViewModel vm, DeviceTreeViewModel tree)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
