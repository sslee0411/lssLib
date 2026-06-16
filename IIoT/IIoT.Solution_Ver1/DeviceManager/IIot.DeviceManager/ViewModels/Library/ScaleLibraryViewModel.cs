// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Library/ScaleLibraryViewModel.cs
//  역할: 스케일 라이브러리 CRUD + 선형 변환 미리보기 ViewModel
//  Phase 4-1: 라이브러리 뷰 신규
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.Core.DataModel;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.Library;

/// <summary>
/// 스케일 라이브러리 편집 ViewModel.
/// scale-library.json 의 ScaleConfig 목록을 CRUD 합니다.
/// Raw→Eng 선형 변환 미리보기를 실시간 계산합니다.
/// </summary>
public partial class ScaleLibraryViewModel : ObservableObject
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "ScaleLibrary";
    private readonly JsonWriteService _writer;

    // §2 ─ 목록 ───────────────────────────────────────────────
    public ObservableCollection<ScaleConfigItem> Items { get; } = [];

    // §3 ─ 선택 항목 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ScaleConfigItem? _selectedItem;

    public bool HasSelection => SelectedItem is not null;

    // §4 ─ 미리보기 ───────────────────────────────────────────
    /// <summary>미리보기용 Raw 입력값</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEngValue))]
    private double _previewRawValue;

    /// <summary>선형 보간 결과 (공학 단위 환산값)</summary>
    public string PreviewEngValue
    {
        get
        {
            if (SelectedItem is null) return "-";
            var s = SelectedItem;
            if (s.RawMax == s.RawMin) return "오류 (RawMax = RawMin)";
            double eng = s.EngMin + (PreviewRawValue - s.RawMin)
                         * (s.EngMax - s.EngMin) / (s.RawMax - s.RawMin);
            return $"{eng:F4} {s.Unit}";
        }
    }

    // §5 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = "준비";
    [ObservableProperty] private bool _hasChanges;

    // §6 ─ 생성자 ─────────────────────────────────────────────
    public ScaleLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
        Items.CollectionChanged += (_, _) => HasChanges = true;
    }

    // §7 ─ 데이터 로드 ────────────────────────────────────────

    /// <summary>ScaleConfig 목록을 ViewModel 항목으로 변환하여 로드합니다.</summary>
    public void Load(IEnumerable<ScaleConfig> scales)
    {
        Items.Clear();
        foreach (var sc in scales)
            Items.Add(new ScaleConfigItem(sc));
        HasChanges = false;
        StatusMessage = $"스케일 {Items.Count}개 로드 완료";
        LogManager.Instance.Info(LogSrc, $"스케일 라이브러리 로드: {Items.Count}개");
    }

    // §8 ─ CRUD 커맨드 ────────────────────────────────────────

    [RelayCommand]
    private void Add()
    {
        var item = new ScaleConfigItem(new ScaleConfig
        {
            Name = "새 스케일",
            RawMin = 0, RawMax = 100,
            EngMin = 0, EngMax = 100,
            Unit = ""
        });
        Items.Add(item);
        SelectedItem = item;
        StatusMessage = "새 스케일 추가됨";
        LogManager.Instance.Info(LogSrc, "스케일 항목 추가");
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        var name = SelectedItem.Name;
        Items.Remove(SelectedItem);
        SelectedItem = Items.LastOrDefault();
        StatusMessage = $"'{name}' 삭제됨";
        LogManager.Instance.Info(LogSrc, $"스케일 항목 삭제: {name}");
    }

    private bool CanDelete() => SelectedItem is not null;

    [RelayCommand]
    private void Save()
    {
        var configs = Items.Select(i => i.ToScaleConfig()).ToList();
        _writer.SaveScaleLibrary(configs);
        HasChanges = false;
        StatusMessage = $"저장 완료 — {configs.Count}개";
        LogManager.Instance.Info(LogSrc, $"스케일 라이브러리 저장: {configs.Count}개");
    }

    // §9 ─ 미리보기 갱신 ──────────────────────────────────────

    /// <summary>선택 항목 변경 시 미리보기 갱신</summary>
    partial void OnSelectedItemChanged(ScaleConfigItem? value)
    {
        PreviewRawValue = value?.RawMin ?? 0;
        OnPropertyChanged(nameof(PreviewEngValue));
    }
}

// ─────────────────────────────────────────────────────────
/// <summary>
/// DataGrid 바인딩용 ScaleConfig 래퍼 — ObservableObject 상속.
/// record 원본을 직접 수정할 수 없으므로 편집용 ViewModel 역할.
/// </summary>
public partial class ScaleConfigItem : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private double _rawMin;
    [ObservableProperty] private double _rawMax;
    [ObservableProperty] private double _engMin;
    [ObservableProperty] private double _engMax;
    [ObservableProperty] private string _unit;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public ScaleConfigItem(ScaleConfig sc)
    {
        Id      = sc.Id;
        _name   = sc.Name;
        _rawMin = sc.RawMin;
        _rawMax = sc.RawMax;
        _engMin = sc.EngMin;
        _engMax = sc.EngMax;
        _unit   = sc.Unit;
    }

    // §3 ─ 검증 ───────────────────────────────────────────────
    public bool IsValid => RawMax != RawMin;

    public string ValidationMessage =>
        IsValid ? string.Empty : "⚠ RawMax 와 RawMin 이 같습니다";

    // §4 ─ 역변환 ─────────────────────────────────────────────
    public ScaleConfig ToScaleConfig() => new()
    {
        Id     = Id,
        Name   = Name,
        RawMin = RawMin,
        RawMax = RawMax,
        EngMin = EngMin,
        EngMax = EngMax,
        Unit   = Unit,
    };
}
