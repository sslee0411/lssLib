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
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

using System.Windows.Controls;

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
}
