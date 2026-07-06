// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Views/Device/DeviceTreeView.xaml.cs
//  역할: [장비] 탭 코드비하인드
//  C-EX-01-6: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using IIoT.Collector.ViewModels;
using System.Windows.Controls;

namespace IIoT.Collector.Views.Device;

public partial class DeviceTreeView : UserControl
{
    public DeviceTreeView(DeviceTreeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
