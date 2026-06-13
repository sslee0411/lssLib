// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes · Controls/ThemePickerButton.xaml.cs
//  역할: ThemePickerButton UserControl 코드비하인드
//
//  설계 원칙:
//    · DataContext = ThemePickerViewModel (생성자 주입)
//    · Unloaded 이벤트 → ViewModel.Dispose() 로 정적 이벤트 구독 해제
//    · Popup.StaysOpen=False → 외부 클릭 시 WPF 자동 닫힘
//      (별도 MouseDown 후킹 불필요)
//
//  ★ 컨버터 클래스(HexToColorConverter 등)는 ThemeConverters.cs 에 정의.
//     코드비하인드 보조 클래스는 XAML 컴파일러가 인식 못 함.
//
//  생성: 2025-05-17
// ══════════════════════════════════════════════════════════
using System.Windows.Controls;
namespace IIoT.UI.Themes.Controls;

/// <summary>
/// 현재 테마를 표시하고 클릭 시 팝업 드롭다운으로
/// 8가지 테마를 선택할 수 있는 컴팩트 UserControl.
///
/// <para>사용 예시 (다른 WPF 프로그램):</para>
/// <code>
/// xmlns:uc="clr-namespace:IIoT.UI.Themes.Controls;assembly=IIoT.UI.Themes"
/// ...
/// &lt;uc:ThemePickerButton/&gt;
/// </code>
///
/// <para>
/// 테마 전환은 내부적으로 <see cref="ThemeManager.Apply"/> 를 호출하므로
/// 외부에서 별도로 처리할 필요 없음.
/// </para>
/// </summary>
public partial class ThemePickerButton : UserControl
{
    private readonly ThemePickerViewModel _vm;

    public ThemePickerButton()
    {
        InitializeComponent();

        _vm = new ThemePickerViewModel();
        DataContext = _vm;

        // ★ 정적 이벤트 구독 해제 — 반드시 호출
        Unloaded += (_, _) => _vm.Dispose();
    }
}
