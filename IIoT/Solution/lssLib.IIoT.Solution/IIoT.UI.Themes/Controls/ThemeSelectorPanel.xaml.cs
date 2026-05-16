// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes · Controls/ThemeSelectorPanel.xaml.cs
//  역할: ThemeSelectorPanel UserControl 코드비하인드
//
//  ★ 컨버터 클래스는 이 파일에 두지 않는다.
//     코드비하인드(.xaml.cs)에 정의된 보조 클래스는 같은 XAML에서
//     참조 불가 (XAML 컴파일러 처리 순서 문제).
//     → ThemeConverters.cs 별도 파일에 정의.
//
//  생성: 2025-05-16
// ══════════════════════════════════════════════════════════
using System.Windows.Controls;
namespace IIoT.UI.Themes.Controls;

/// <summary>
/// 7가지 테마를 카드 형태로 표시하고 런타임 즉시 전환하는 UserControl.
/// </summary>
public partial class ThemeSelectorPanel : UserControl
{
    public ThemeSelectorPanel()
    {
        InitializeComponent();
        DataContext = new ThemeSelectorViewModel();

        // ThemeSelectorViewModel은 정적 이벤트를 구독하므로
        // UserControl 언로드 시 반드시 Dispose 호출
        Unloaded += (_, _) =>
        {
            if (DataContext is IDisposable d)
                d.Dispose();
        };
    }
}
