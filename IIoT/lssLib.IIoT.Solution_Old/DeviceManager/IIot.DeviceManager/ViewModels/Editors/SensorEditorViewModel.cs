// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Editors/SensorEditorViewModel.cs
//  역할: Sensor 노드 속성 편집 ViewModel
//  Phase 3: 편집기 패널
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.DataModel;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.Editors;

/// <summary>Sensor 노드 속성 편집 ViewModel.</summary>
public partial class SensorEditorViewModel : ObservableObject
{
    private SensorNodeViewModel? _target;

    // §1 ─ 물리 속성 ──────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private string _sensorType = "Generic";
    [ObservableProperty] private string _description = "";

    // §2 ─ 복합 계산식 ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormula))]
    private string? _formula;

    public bool IsFormula => !string.IsNullOrEmpty(Formula);

    // §3 ─ 스케일 참조 ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScale))]
    private string? _scaleConfigId;

    [ObservableProperty] private string _scaleConfigName = "";
    public bool HasScale => !string.IsNullOrEmpty(ScaleConfigId);

    // §4 ─ 알람 임계값 ────────────────────────────────────────
    [ObservableProperty] private double? _alarmHH;
    [ObservableProperty] private double? _alarmH;
    [ObservableProperty] private double? _alarmL;
    [ObservableProperty] private double? _alarmLL;
    [ObservableProperty] private double _alarmDeadBand;

    public bool HasAlarm => AlarmHH.HasValue || AlarmH.HasValue ||
                            AlarmL.HasValue || AlarmLL.HasValue;

    // §5 ─ TagRef 목록 ─────────────────────────────────────────
    public ObservableCollection<TagRefItem> TagRefs { get; } = [];

    /// <summary>트리에서 선택 가능한 Tag 목록 (TagRef 연결용)</summary>
    public ObservableCollection<TagNodeViewModel> AvailableTags { get; } = [];

    // §6 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private bool _hasChanges;

    public string TargetLabel => _target is not null
        ? $"🌡️  {_target.Name}  [{_target.Unit}]"
        : "Sensor 선택 없음";

    // §7 ─ 센서 타입 / 단위 목록 ──────────────────────────────
    public IReadOnlyList<string> SensorTypeList =>
    [
        "Generic", "Temperature", "Pressure", "Flow",
        "Vibration", "Current", "Voltage", "Speed", "Bool"
    ];

    public IReadOnlyList<string> UnitSuggestions =>
    [
        "°C", "°F", "K",
        "bar", "kPa", "MPa", "psi",
        "m³/h", "L/min",
        "rpm", "Hz",
        "A", "mA", "V", "kV",
        "mm/s", "g",
        "%", "bool", ""
    ];

    // §8 ─ Load / Apply ───────────────────────────────────────
    public void Load(SensorNodeViewModel node,
                     IEnumerable<TagNodeViewModel> availableTags)
    {
        _target = node;
        Name = node.Name;
        Unit = node.Unit;
        SensorType = node.SensorType;
        Description = node.Description;
        Formula = node.Formula;
        ScaleConfigId = node.ScaleConfigId;
        AlarmHH = node.AlarmHighHigh;
        AlarmH = node.AlarmHigh;
        AlarmL = node.AlarmLow;
        AlarmLL = node.AlarmLowLow;
        AlarmDeadBand = node.AlarmDeadBand;

        TagRefs.Clear();
        foreach (var r in node.TagRefs)
            TagRefs.Add(r);

        AvailableTags.Clear();
        foreach (var t in availableTags)
            AvailableTags.Add(t);

        HasChanges = false;
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(HasAlarm));
    }

    [RelayCommand]
    private void Apply()
    {
        if (_target is null) return;

        _target.Name = Name.Trim();
        _target.Unit = Unit;
        _target.SensorType = SensorType;
        _target.Description = Description;
        _target.Formula = string.IsNullOrEmpty(Formula) ? null : Formula;
        _target.ScaleConfigId = string.IsNullOrEmpty(ScaleConfigId) ? null : ScaleConfigId;
        _target.AlarmHighHigh = AlarmHH;
        _target.AlarmHigh = AlarmH;
        _target.AlarmLow = AlarmL;
        _target.AlarmLowLow = AlarmLL;
        _target.AlarmDeadBand = AlarmDeadBand;

        // TagRef 동기화
        _target.TagRefs.Clear();
        foreach (var r in TagRefs)
            _target.TagRefs.Add(r);

        HasChanges = false;
    }

    [RelayCommand]
    private void Reset()
    {
        if (_target is not null) Load(_target, AvailableTags);
    }

    // §9 ─ TagRef CRUD ────────────────────────────────────────
    [RelayCommand]
    private void AddTagRef()
    {
        TagRefs.Add(new TagRefItem("", TagRefs.Count == 0 ? "primary" : $"ref{TagRefs.Count}"));
        HasChanges = true;
    }

    [RelayCommand]
    private void RemoveTagRef(TagRefItem item)
    {
        TagRefs.Remove(item);
        HasChanges = true;
    }

    // §10 ─ 변경 감지 ─────────────────────────────────────────
    partial void OnNameChanged(string v) => HasChanges = true;
    
    partial void OnUnitChanged(string v) => HasChanges = true;
    
    partial void OnSensorTypeChanged(string v) => HasChanges = true;
    
    partial void OnFormulaChanged(string? v) => HasChanges = true;
    
    partial void OnScaleConfigIdChanged(string? v) => HasChanges = true;
    
    partial void OnAlarmHHChanged(double? v) 
    { 
        HasChanges = true; OnPropertyChanged(nameof(HasAlarm)); 
    }
    
    partial void OnAlarmHChanged(double? v)
    { 
        HasChanges = true; OnPropertyChanged(nameof(HasAlarm)); 
    }

    partial void OnAlarmLChanged(double? v)
    {
        HasChanges = true; OnPropertyChanged(nameof(HasAlarm)); 
    }

    partial void OnAlarmLLChanged(double? v)
    { 
        HasChanges = true; OnPropertyChanged(nameof(HasAlarm)); 
    }
}