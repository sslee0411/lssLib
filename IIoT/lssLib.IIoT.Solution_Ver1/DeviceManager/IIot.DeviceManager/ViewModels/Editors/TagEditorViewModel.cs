// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Editors/TagEditorViewModel.cs
//  역할: Tag 노드 속성 편집 ViewModel
//  Phase 3: 편집기 패널
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using System.Net;
using System.Xml.Linq;

namespace IIoT.DeviceManager.ViewModels.Editors;

/// <summary>Tag 노드 속성 편집 ViewModel.</summary>
public partial class TagEditorViewModel : ObservableObject
{
    private TagNodeViewModel? _target;

    // §1 ─ 수집 주소 ──────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _address = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BufTypeDescription))]
    private string _bufType = "FloatBE";

    [ObservableProperty] private int _pollMs = 1000;
    [ObservableProperty] private double _deadBand = 0.0;

    // §2 ─ 수집 주체 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOwnerFixed))]
    [NotifyPropertyChangedFor(nameof(OwnerModeText))]
    private string? _ownerDeviceId;

    public bool IsOwnerFixed => !string.IsNullOrEmpty(OwnerDeviceId);
    public string OwnerModeText => IsOwnerFixed
        ? "🔒 명시 고정 (트리 상향 탐색 생략)"
        : "🔍 자동 (트리 상향 탐색)";

    // §3 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private string _resolvedOwnerName = "";

    public string TargetLabel => _target is not null
        ? $"📋  {_target.Name}  @ {_target.Address}"
        : "Tag 선택 없음";

    // §4 ─ BufType 목록 + 설명 ────────────────────────────────
    public IReadOnlyList<string> BufTypeList =>
    [
        "FloatBE", "FloatLE",
        "DoubleBE", "DoubleLE",
        "Int16BE",  "Int16LE",
        "UInt16BE", "UInt16LE",
        "Int32BE",  "Int32LE",
        "UInt32BE", "UInt32LE",
        "Int64BE",  "Int64LE",
        "Bool",     "Byte"
    ];

    public string BufTypeDescription => BufType switch
    {
        "FloatBE" or "FloatLE" => "32비트 부동소수 (4 Byte)",
        "DoubleBE" or "DoubleLE" => "64비트 부동소수 (8 Byte)",
        "Int16BE" or "Int16LE" => "16비트 정수 부호 있음 (2 Byte)",
        "UInt16BE" or "UInt16LE" => "16비트 정수 부호 없음 (2 Byte)",
        "Int32BE" or "Int32LE" => "32비트 정수 부호 있음 (4 Byte)",
        "UInt32BE" or "UInt32LE" => "32비트 정수 부호 없음 (4 Byte)",
        "Int64BE" or "Int64LE" => "64비트 정수 (8 Byte)",
        "Bool" => "불리언 (1 Bit / 1 Byte)",
        "Byte" => "단일 바이트 (1 Byte)",
        _ => ""
    };

    // §5 ─ Load / Apply ───────────────────────────────────────
    public void Load(TagNodeViewModel node,
                     IEnumerable<DeviceNodeViewModel> allNodes)
    {
        _target = node;
        Name = node.Name;
        Address = node.Address;
        BufType = node.BufType;
        PollMs = node.PollMs;
        DeadBand = node.DeadBand;
        OwnerDeviceId = node.OwnerDeviceId;

        // 수집 주체 이름 표시 (자동 탐색 결과)
        var owner = node.ResolveOwner();
        ResolvedOwnerName = owner?.Name ?? "(수집 주체 없음 — 설정 오류)";

        HasChanges = false;
        OnPropertyChanged(nameof(TargetLabel));
    }

    [RelayCommand]
    private void Apply()
    {
        if (_target is null) return;
        _target.Name = Name.Trim();
        _target.Address = Address.Trim();
        _target.BufType = BufType;
        _target.PollMs = PollMs;
        _target.DeadBand = DeadBand;
        _target.OwnerDeviceId = string.IsNullOrEmpty(OwnerDeviceId)
                                ? null : OwnerDeviceId;
        HasChanges = false;
    }

    /// <summary>OwnerDeviceId 를 null 로 초기화 (자동 탐색으로 전환)</summary>
    [RelayCommand]
    private void ClearOwner()
    {
        OwnerDeviceId = null;
        HasChanges = true;
    }

    [RelayCommand]
    private void Reset()
    {
        if (_target is not null) Load(_target, []);
    }

    // §6 ─ 변경 감지 ──────────────────────────────────────────
    partial void OnNameChanged(string v) => HasChanges = true;
    partial void OnAddressChanged(string v) => HasChanges = true;
    partial void OnBufTypeChanged(string v) => HasChanges = true;
    partial void OnPollMsChanged(int v) => HasChanges = true;
    partial void OnDeadBandChanged(double v) => HasChanges = true;
    partial void OnOwnerDeviceIdChanged(string? v) => HasChanges = true;
}