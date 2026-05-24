// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · TagNodeViewModel.cs
//  역할: PLC 레지스터 주소 노드 ViewModel (수집 레이어)
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — Tag/Sensor 이중 레이어 구조 반영
//        ScaleConfigId, AlarmGroupId → Sensor로 이동
//        Tag는 순수 수집 주소만 보유
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// PLC 레지스터 주소 노드 — 수집 레이어.
///
/// Tag는 "어디서 읽느냐"만 알고, "무엇을 보여주느냐"는 모릅니다.
/// ScaleConfig, AlarmConfig는 Sensor(물리 레이어)가 소유합니다.
///
/// 배치: Plc 하위에만 위치 (Plc → Tag)
///
/// 예시:
///   🔌 PLC-SIEMENS
///     📋 temp_raw  Address=MW100  DataType=Int16  PollRateMs=1000
///     📋 press_hi  Address=MW102  DataType=Float  PollRateMs=500
/// </summary>
public partial class TagNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 수집 주소 속성 ─────────────────────────────────────

    /// <summary>
    /// PLC 레지스터 주소.
    /// Modbus: "40001", "D100"
    /// OPC-UA: "ns=2;i=1003"
    /// Siemens: "MW100", "M0.0"
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private string _address = "";

    /// <summary>
    /// 데이터 타입 (BufType).
    /// lssLib.Binary.BufType 열거형 문자열.
    /// 예: "FloatBE", "Int16BE", "UInt16BE", "Bool"
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private string _bufType = "FloatBE";

    /// <summary>
    /// 폴링 주기 (밀리초).
    /// 0이면 PLC의 기본 주기를 따름.
    /// </summary>
    [ObservableProperty] private int _pollMs = 1000;

    /// <summary>
    /// DeadBand — 이 범위 이내 변화는 수집 생략 (원시값 기준).
    /// 값이 작을수록 민감, 0이면 모든 변화 수집.
    /// </summary>
    [ObservableProperty] private double _deadBand;

    // §2 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Tag;

    public override string IconGlyph => "📋";

    /// <summary>Tag는 리프 노드 — 자식 없음</summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds => [];

    /// <summary>BufType 배지 (예: FloatBE, Int16BE)</summary>
    public override string? Badge => string.IsNullOrEmpty(BufType) ? null : BufType;

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public TagNodeViewModel(string name = "새 태그")
    {
        Name = name;
    }
}