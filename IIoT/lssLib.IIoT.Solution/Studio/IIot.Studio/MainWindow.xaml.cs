// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainWindow.xaml.cs
//  Fix: namespace IIoT.Studio (IIot → IIoT)
//  생성: 2026-06-14
// ══════════════════════════════════════════════════════════

using IIoT.Studio.ViewModels;
using IIoT.Studio.ViewModels.DeviceTree;
using System.Windows;

namespace IIoT.Studio;   // ★ IIot → IIoT

public partial class MainWindow : Window
{
    // §1 ─ 생성자 ─────────────────────────────────────────────
    public MainWindow(StudioMainViewModel vm, DeviceTreeViewModel tree)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
