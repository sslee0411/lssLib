// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/DeviceControlBase.xaml.cs
//  역할: 장비 아이콘 카드 공통 베이스 컨트롤 (코드비하인드)
//        DataContext 는 AbstractLayoutNode 파생 노드 모델(Motor/Conveyor/
//        Tank/Valve/GenericIcon 등)이 그대로 흘러 들어온다 — 이 클래스는
//        시각 껍데기(카드 프레임)만 담당하고 데이터 바인딩은 XAML 에서 처리.
//
//        abstract 로 선언 — 반드시 파생 클래스(MotorControl 등)를 통해서만
//        인스턴스화된다 (직접 <dc:DeviceControlBase/> 로 XAML 배치 불가).
//  HM-04: 신규
//  HM-06: OnDeviceControlLoaded() 가상 메서드 추가 — 파생 클래스(MotorControl 등)가
//         이 컨트롤이 화면에 표시된 시점(Loaded)에 IconText/LevelTrack/LevelFill
//         같은 베이스의 명명된 요소에 접근해 회전/흐름/수위/개폐 애니메이션을
//         초기화할 수 있는 훅. 파생 클래스는 여전히 별도 XAML이 없는 순수 C#
//         클래스이므로(오직 DeviceControlBase만 x:Class를 가짐) 상속 계층에서
//         IComponentConnector.Connect 충돌 없이 이 요소들을 그대로 참조할 수 있다.
//  HM-08: 알람 배지/ACK 버튼 Click 핸들러 추가.
//         ★ Popup(AlarmPopup)은 별도의 시각 트리 루트로 렌더링되므로, Popup 내부에
//         있는 ACK 버튼에서 RelativeSource(AncestorType=UserControl) 로 상위
//         LayoutCanvasView 를 찾는 XAML 바인딩은 신뢰할 수 없다(WPF의 잘 알려진
//         제약). 대신 _OnAckButtonClick() 에서 this(DeviceControlBase, 정상적으로
//         메인 시각 트리에 있음)를 기준으로 VisualTreeHelper 로 직접 탐색한다.
//         _FindAncestorUserControl() 은 특정 View 타입을 하드코딩하지 않고 범용
//         UserControl 을 찾으므로 DeviceControlBase 의 재사용성을 해치지 않는다.
//  HM-20: 코드 변경 없음 — IconText(TextBlock) → IconHost(Grid)+IconGlyphText 로
//         XAML 구조만 바뀌었고(DeviceControlBase.xaml 참조), OnDeviceControlLoaded()
//         훅 자체는 그대로다. 실제 벡터 아이콘 구성은 각 파생 클래스(Motor/
//         Conveyor/Tank/Valve) 코드비하인드에서 처리한다.
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using IIoT.HMI.Core.Layout;
using IIoT.HMI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>
/// 모든 장비 아이콘 컨트롤(Motor/Conveyor/Tank/Valve/GenericIcon 등)의 공통 베이스.
/// 카드 프레임·선택 강조·카테고리 색상 바·아이콘 글리프·라벨 렌더링을 제공한다.
/// 신규 장비 타입을 추가할 때는 이 클래스를 상속하는 빈 클래스 1개만 만들면 되고,
/// 향후 장비별 고유 시각효과(회전 애니메이션 등, HM-06)가 필요해지면 해당 파생
/// 클래스에서만 확장하면 된다 — 공통 카드 프레임(DeviceControlBase.xaml)은 그대로 재사용.
/// </summary>
public abstract partial class DeviceControlBase : UserControl
{
    protected DeviceControlBase()
    {
        InitializeComponent();
        Loaded += (_, _) => OnDeviceControlLoaded();
    }

    /// <summary>
    /// ★ HM-06: 파생 클래스가 재정의하여 장비 전용 애니메이션(회전/흐름/수위/개폐 등)을
    /// 초기화하는 훅. 컨트롤이 Loaded 된 시점에 1회 호출되며, 이 시점부터
    /// IconText/LevelTrack/LevelFill 등 베이스의 명명된 요소에 안전하게 접근할 수 있다.
    /// 기본 구현은 아무 것도 하지 않는다(placeholder 장비 GenericIconControl 등).
    /// </summary>
    protected virtual void OnDeviceControlLoaded() { }

    // ── HM-08: 알람 배지/팝업 이벤트 핸들러 ──────────────────

    /// <summary>알람 배지(⚠) 클릭 시 상세 팝업 토글</summary>
    private void _OnAlarmBadgeClick(object sender, RoutedEventArgs e)
    {
        AlarmPopup.IsOpen = !AlarmPopup.IsOpen;
    }

    /// <summary>
    /// ACK 버튼 클릭. Popup 내부라 RelativeSource(AncestorType=UserControl) 바인딩이
    /// 신뢰할 수 없으므로, this(DeviceControlBase)를 기준으로 VisualTreeHelper 로
    /// 직접 상위 UserControl(LayoutCanvasView)을 찾아 그 DataContext(LayoutCanvasViewModel)의
    /// AcknowledgeAlarmCommand 를 실행한다.
    /// </summary>
    private void _OnAckButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not AbstractLayoutNode node) return;

        var host = _FindAncestorUserControl(this);
        if (host?.DataContext is LayoutCanvasViewModel vm && vm.AcknowledgeAlarmCommand.CanExecute(node))
        {
            vm.AcknowledgeAlarmCommand.Execute(node);
            AlarmPopup.IsOpen = false;
        }
    }

    /// <summary>
    /// this(DeviceControlBase)의 상위 방향으로 VisualTree 를 탐색해 가장 가까운
    /// UserControl(자기 자신 제외)을 찾는다. LayoutCanvasView 를 직접 참조하지 않고
    /// 범용적으로 탐색해 DeviceControlBase 의 재사용성을 유지한다.
    /// </summary>
    private static UserControl? _FindAncestorUserControl(DependencyObject start)
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is UserControl uc) return uc;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
