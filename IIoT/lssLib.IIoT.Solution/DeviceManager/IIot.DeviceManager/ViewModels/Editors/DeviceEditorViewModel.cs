// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Editors/DeviceEditorViewModel.cs
//  역할: 장비(Device) 노드 속성 편집 ViewModel
//  Phase 3: 편집기 패널
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.DataModel;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Windows.Documents;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.Editors;

/// <summary>
/// 장비(Device) 노드 속성 편집 ViewModel.
/// MainViewModel.SelectedEditor 에 바인딩되어 우측 패널에 표시됩니다.
/// </summary>
public partial class DeviceEditorViewModel : ObservableObject
{
    // §1 ─ 편집 대상 ──────────────────────────────────────────
    private DeviceItemViewModel? _target;

    // §2 ─ 기본 속성 ──────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _manufacturer = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _serialNo = "";

    // §3 ─ 통신 설정 참조 ─────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommConfig))]
    private string? _commConfigId;

    [ObservableProperty] private string _commConfigName = "";
    public bool HasCommConfig => !string.IsNullOrEmpty(CommConfigId);

    // §4 ─ 위치 참조 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    private string? _locationId;

    [ObservableProperty] private string _locationName = "";
    public bool HasLocation => !string.IsNullOrEmpty(LocationId);

    // §5 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private bool _hasChanges;

    // §6 ─ 드롭다운 소스 ──────────────────────────────────────
    public List<CommConfig> AvailableCommConfigs { get; } = [];
    public List<Location> AvailableLocations { get; } = [];

    // §7 ─ 편집 대상 노드명 ───────────────────────────────────
    public string TargetLabel => _target is not null
        ? $"📟  {_target.Name}"
        : "장비 선택 없음";

    // §8 ─ Load / Apply ───────────────────────────────────────

    /// <summary>대상 노드의 값을 편집기로 불러옵니다.</summary>
    public void Load(DeviceItemViewModel node,
                     IEnumerable<CommConfig> commConfigs,
                     IEnumerable<Location> locations)
    {
        _target = node;

        Name = node.Name;
        Manufacturer = node.Manufacturer;
        Model = node.Model;
        SerialNo = node.SerialNo;
        CommConfigId = node.CommConfigId;
        LocationId = node.LocationId;
        IsOnline = node.IsOnline;

        AvailableCommConfigs.Clear();
        AvailableCommConfigs.AddRange(commConfigs);

        AvailableLocations.Clear();
        AvailableLocations.AddRange(locations);

        // 참조 이름 갱신
        CommConfigName = AvailableCommConfigs
            .FirstOrDefault(c => c.Id == CommConfigId)?.Name ?? "";
        LocationName = AvailableLocations
            .FirstOrDefault(l => l.Id == LocationId)?.Name ?? "";

        HasChanges = false;
        OnPropertyChanged(nameof(TargetLabel));
    }

    /// <summary>편집된 값을 대상 노드에 적용합니다.</summary>
    [RelayCommand]
    private void Apply()
    {
        if (_target is null) return;

        _target.Name = Name.Trim();
        _target.Manufacturer = Manufacturer;
        _target.Model = Model;
        _target.SerialNo = SerialNo;
        _target.CommConfigId = string.IsNullOrEmpty(CommConfigId) ? null : CommConfigId;
        _target.LocationId = string.IsNullOrEmpty(LocationId) ? null : LocationId;
        _target.IsOnline = IsOnline;

        HasChanges = false;
    }

    /// <summary>편집을 취소하고 원래 값으로 복원합니다.</summary>
    [RelayCommand]
    private void Reset()
    {
        if (_target is not null)
            Load(_target, AvailableCommConfigs, AvailableLocations);
    }

    // §9 ─ 변경 감지 ──────────────────────────────────────────
    partial void OnNameChanged(string v) => HasChanges = true;
    partial void OnManufacturerChanged(string v) => HasChanges = true;
    partial void OnModelChanged(string v) => HasChanges = true;
    partial void OnSerialNoChanged(string v) => HasChanges = true;
    partial void OnCommConfigIdChanged(string? v)
    {
        HasChanges = true;
        CommConfigName = AvailableCommConfigs
            .FirstOrDefault(c => c.Id == v)?.Name ?? "";
    }
    partial void OnLocationIdChanged(string? v)
    {
        HasChanges = true;
        LocationName = AvailableLocations
            .FirstOrDefault(l => l.Id == v)?.Name ?? "";
    }
}