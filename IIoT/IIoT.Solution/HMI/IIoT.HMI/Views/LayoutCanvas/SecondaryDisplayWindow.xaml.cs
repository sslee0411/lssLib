// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/LayoutCanvas/SecondaryDisplayWindow.xaml.cs
//  역할: 보조 화면 창 코드비하인드 — 생성자로 전달받은 UIElement(두 번째
//        LayoutCanvasView 인스턴스)를 그대로 Host 에 꽂아 넣기만 한다.
//        (ForceWriteDialog/TrendWindow 와 동일하게 DI 없이 직접 new 로
//        생성하는 "일반 창" 패턴)
//  HM-19: 신규
//  생성: 2026-07-19
// ══════════════════════════════════════════════════════════

using System.Windows;

namespace IIoT.HMI.Views.LayoutCanvas;

public partial class SecondaryDisplayWindow : Window
{
    public SecondaryDisplayWindow(UIElement content)
    {
        InitializeComponent();
        Host.Content = content;
    }
}
