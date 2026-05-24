// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · SensorNodeViewModel.cs
//  역할: 물리 센서 노드 ViewModel (물리 레이어)
//  생성: 2025-05-23
//
//  Sensor = 실 물리 센서의 표현
//    · 위치: Device 하위 직접 (PLC/Tag와 독립)
//    · TagRef로 Tag(수집 레이어)를 1개 이상 참조
//    · ScaleConfig, AlarmConfig, Formula 소유
//    · 모니터링 프로그램이 Sensor 단위로 바라봄
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.DeviceManager.Core.DataModel;
using System.Collections.ObjectModel;
using System.Data;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// TagRef 항목 — Sensor가 참조하는 Tag 연결 정보
/// </summary>
public partial class TagRefItem : ObservableObject
{
    // §1 ─ 속성 ───────────────────────────────────────────────

    /// <summary>참조할 Tag의 NodeId (TagNodeViewModel.Id)</summary>
    [ObservableProperty] private string _tagId = "";

    /// <summary>
    /// 복합 계산식에서의 역할.
    /// 단일 Tag: "primary"
    /// 복합 계산: "high", "low", "a", "b" 등 Formula에서 사용하는 변수명
    /// </summary>
    [ObservableProperty] private string _role = "primary";

    /// <summary>UI 표시용 — 연결된 Tag 이름 (설정 후 갱신)</summary>
    [ObservableProperty] private string _tagName = "";

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public TagRefItem() { }

    public TagRefItem(string tagId, string role = "primary", string tagName = "")
    {
        _tagId = tagId;
        _role = role;
        _tagName = tagName;
    }

    /// <summary>UI 표시 문자열</summary>
    public string Display => string.IsNullOrEmpty(TagName)
        ? $"{Role}: {TagId[..Math.Min(8, TagId.Length)]}..."
        : $"{Role}: {TagName}";
}

/// <summary>
/// 물리 센서 노드 ViewModel — 물리 레이어.
///
/// Device 하위에 직접 배치되며, Tag(수집 레이어)를 TagRef로 참조합니다.
/// ScaleConfig, AlarmConfig, Formula를 소유합니다.
///
/// 예시 구조:
///   📦 압연기-001 (Device)
///     🔌 PLC-SIEMENS  ← 수집 레이어
///         📋 temp_raw (Tag)
///     🌡️ 베어링온도1  ← 물리 레이어 (이 클래스)
///         TagRef: temp_raw → Scale(0~4095 → 0~150°C) → 28.47°C
/// </summary>
public partial class SensorNodeViewModel : DeviceNodeViewModel
{
    // §1 ─ 물리 센서 속성 ─────────────────────────────────────

    /// <summary>공학 단위 (예: "°C", "kPa", "rpm", "bool")</summary>
    [ObservableProperty] private string _unit = "";

    /// <summary>
    /// 센서 종류 분류 (아이콘/필터용).
    /// 예: Temperature, Pressure, Flow, Vibration, Current, Bool 등
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    private string _sensorType = "Generic";

    /// <summary>센서 설명 (설치 위치, 용도 등)</summary>
    [ObservableProperty] private string _description = "";

    // §2 ─ 복합 계산식 ────────────────────────────────────────

    /// <summary>
    /// 복합 계산식 (null = TagRef[0] 단순 참조).
    /// 예: "high - low" (차압), "a + b * 0.5" (평균 등)
    /// Formula 변수명은 TagRef.Role과 일치해야 함.
    /// </summary>
    [ObservableProperty] private string? _formula;

    // §3 ─ 스케일 참조 ────────────────────────────────────────

    /// <summary>
    /// ScaleConfig.Id 참조 (scale-library.json).
    /// null이면 ScaledValue = RawValue 그대로.
    /// ScaleConfig는 Sensor 소유 — Tag는 물리 단위를 모름.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    private string? _scaleConfigId;

    // §4 ─ 알람 임계값 ────────────────────────────────────────

    /// <summary>알람 임계값 그룹 ID (alarm-library.json 참조)</summary>
    [ObservableProperty] private string? _alarmGroupId;

    /// <summary>인라인 임계값 — HH (긴급 상한, null = 미설정)</summary>
    [ObservableProperty] private double? _alarmHighHigh;

    /// <summary>인라인 임계값 — H (경고 상한)</summary>
    [ObservableProperty] private double? _alarmHigh;

    /// <summary>인라인 임계값 — L (경고 하한)</summary>
    [ObservableProperty] private double? _alarmLow;

    /// <summary>인라인 임계값 — LL (긴급 하한)</summary>
    [ObservableProperty] private double? _alarmLowLow;

    /// <summary>알람 데드밴드 (히스테리시스)</summary>
    [ObservableProperty] private double _alarmDeadBand;

    // §5 ─ TagRef 목록 ─────────────────────────────────────────

    /// <summary>
    /// 참조하는 Tag 목록.
    /// 단순: 1개 (role: "primary")
    /// 복합: 2개 이상 (role: "high", "low" 등)
    /// </summary>
    public ObservableCollection<TagRefItem> TagRefs { get; } = [];

    // §6 ─ 기반 멤버 구현 ─────────────────────────────────────

    public override NodeKind Kind => NodeKind.Sensor;

    /// <summary>센서 종류에 따른 아이콘</summary>
    public override string IconGlyph => SensorType switch
    {
        "Temperature" => "🌡️",
        "Pressure" => "💧",
        "Flow" => "🌊",
        "Vibration" => "📳",
        "Current" => "⚡",
        "Voltage" => "🔋",
        "Bool" => "🔘",
        "Speed" => "⚙️",
        _ => "📡",
    };

    /// <summary>물리 레이어 — 자식 없음 (리프 노드)</summary>
    public override IReadOnlyList<NodeKind> AllowedChildKinds => [];

    /// <summary>스케일 설정 여부 + TagRef 개수 배지</summary>
    public override string? Badge
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Unit)) parts.Add(Unit);
            if (TagRefs.Count > 1) parts.Add($"{TagRefs.Count}T");
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }
    }

    // §7 ─ 생성자 ─────────────────────────────────────────────

    public SensorNodeViewModel(string name = "새 센서")
    {
        Name = name;
        // TagRef 변경 시 Badge 갱신
        TagRefs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Badge));
    }

    // §8 ─ 헬퍼 ──────────────────────────────────────────────

    /// <summary>단일 TagRef 추가 (가장 많이 사용하는 케이스)</summary>
    public void AddTagRef(string tagId, string tagName = "", string role = "primary")
        => TagRefs.Add(new TagRefItem(tagId, role, tagName));

    /// <summary>알람 설정 여부</summary>
    public bool HasAlarm =>
        AlarmHighHigh.HasValue || AlarmHigh.HasValue ||
        AlarmLow.HasValue || AlarmLowLow.HasValue;

    /// <summary>복합 계산 여부</summary>
    public bool IsFormulaSensor => Formula is not null && TagRefs.Count > 1;
}