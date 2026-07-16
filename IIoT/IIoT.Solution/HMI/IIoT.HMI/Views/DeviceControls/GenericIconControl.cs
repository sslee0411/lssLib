// ══════════════════════════════════════════════════════════
//  IIoT.HMI · Views/DeviceControls/GenericIconControl.cs
//  역할: GenericIconNode(범용 placeholder 아이콘) 전용 컨트롤
//        DeviceControlBase 를 상속만 하고 별도 XAML/로직 없음 —
//        카드 프레임·글리프·라벨은 전부 베이스가 처리(모델 바인딩으로 자동 반영).
//  HM-04: 신규 (HM-03 placeholder를 베이스 컨트롤 상속 구조로 정리)
//  생성: 2026-07-16
// ══════════════════════════════════════════════════════════

namespace IIoT.HMI.Views.DeviceControls;

/// <summary>범용 아이콘 카드 (GenericIconNode 전용). 베이스 그대로 사용.</summary>
public sealed class GenericIconControl : DeviceControlBase
{
}
