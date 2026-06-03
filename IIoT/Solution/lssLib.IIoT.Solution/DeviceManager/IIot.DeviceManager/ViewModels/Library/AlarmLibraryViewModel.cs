// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Library/AlarmLibraryViewModel.cs
//  역할: 알람 규칙 라이브러리 CRUD ViewModel
//  Phase 4-2: 라이브러리 뷰 신규
//  Fix: _hh → _hH, _ll → _lL (CommunityToolkit PascalCase 변환 규칙)
//       _hh → Hh, _ll → Ll 로 생성되어 HH/LL 참조 시 CS0103 발생
//       _hH → HH, _lL → LL 로 생성되어 정상 참조 가능
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.Core.DataModel;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.Library;

/// <summary>
/// 알람 규칙 라이브러리 편집 ViewModel.
/// alarm-library.json 의 AlarmRule 목록을 CRUD 합니다.
/// </summary>
public partial class AlarmLibraryViewModel : ObservableObject
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc = "AlarmLibrary";
    private readonly JsonWriteService _writer;

    // §2 ─ 목록 ───────────────────────────────────────────────
    public ObservableCollection<AlarmRuleItem> Items { get; } = [];

    // §3 ─ 선택 항목 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AlarmRuleItem? _selectedItem;

    public bool HasSelection => SelectedItem is not null;

    // §4 ─ 상태 ───────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = "준비";
    [ObservableProperty] private bool   _hasChanges;

    // §5 ─ 생성자 ─────────────────────────────────────────────
    public AlarmLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
        Items.CollectionChanged += (_, _) => HasChanges = true;
    }

    // §6 ─ 데이터 로드 ────────────────────────────────────────

    public void Load(IEnumerable<AlarmRule> rules)
    {
        Items.Clear();
        foreach (var r in rules)
            Items.Add(new AlarmRuleItem(r));
        HasChanges = false;
        StatusMessage = $"알람 규칙 {Items.Count}개 로드 완료";
        LogManager.Instance.Info(LogSrc, $"알람 라이브러리 로드: {Items.Count}개");
    }

    // §7 ─ CRUD 커맨드 ────────────────────────────────────────

    [RelayCommand]
    private void Add()
    {
        var item = new AlarmRuleItem(new AlarmRule
        {
            Name     = "새 알람 규칙",
            H        = 90,
            L        = 10,
            DeadBand = 1.0,
            Message  = "{tagName} {level} 알람: {value}"
        });
        Items.Add(item);
        SelectedItem = item;
        StatusMessage = "새 알람 규칙 추가됨";
        LogManager.Instance.Info(LogSrc, "알람 규칙 추가");
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        var name = SelectedItem.Name;
        Items.Remove(SelectedItem);
        SelectedItem = Items.LastOrDefault();
        StatusMessage = $"'{name}' 삭제됨";
        LogManager.Instance.Info(LogSrc, $"알람 규칙 삭제: {name}");
    }

    private bool CanDelete() => SelectedItem is not null;

    [RelayCommand]
    private void Save()
    {
        var rules = Items.Select(i => i.ToAlarmRule()).ToList();
        _writer.SaveAlarmLibrary(rules);
        HasChanges = false;
        StatusMessage = $"저장 완료 — {rules.Count}개";
        LogManager.Instance.Info(LogSrc, $"알람 라이브러리 저장: {rules.Count}개");
    }
}

// ─────────────────────────────────────────────────────────
/// <summary>
/// DataGrid 바인딩용 AlarmRule 래퍼.
///
/// ★ CommunityToolkit.Mvvm [ObservableProperty] 필드명 규칙:
///   _abc → PascalCase → Abc  (소문자 시작 필드)
///   _aB  → PascalCase → AB   (첫 두 글자가 대소문자 조합)
///
///   HH / LL 프로퍼티를 생성하려면:
///     _hh  → Hh  ← 잘못된 방법 (CS0103 발생)
///     _hH  → HH  ← 올바른 방법 ★
///     _lL  → LL  ← 올바른 방법 ★
/// </summary>
public partial class AlarmRuleItem : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────
    public string Id { get; }

    [ObservableProperty] private string  _name;

    // ★ Fix: _hh → _hH (HH 프로퍼티 생성), _ll → _lL (LL 프로퍼티 생성)
    [ObservableProperty] private double? _hH;       // → 프로퍼티: HH
    [ObservableProperty] private double? _h;        // → 프로퍼티: H
    [ObservableProperty] private double? _l;        // → 프로퍼티: L
    [ObservableProperty] private double? _lL;       // → 프로퍼티: LL

    [ObservableProperty] private double  _deadBand;
    [ObservableProperty] private string  _message;

    // §2 ─ 생성자 ─────────────────────────────────────────────
    public AlarmRuleItem(AlarmRule r)
    {
        Id        = r.Id;
        _name     = r.Name;
        _hH       = r.HH;       // ★ Fix: _hH (HH)
        _h        = r.H;
        _l        = r.L;
        _lL       = r.LL;       // ★ Fix: _lL (LL)
        _deadBand = r.DeadBand;
        _message  = r.Message;
    }

    // §3 ─ 활성화 요약 (DataGrid 표시용) ──────────────────────
    public string ActiveLevels
    {
        get
        {
            var parts = new List<string>();
            if (HH.HasValue) parts.Add($"HH={HH:G}");
            if (H.HasValue)  parts.Add($"H={H:G}");
            if (L.HasValue)  parts.Add($"L={L:G}");
            if (LL.HasValue) parts.Add($"LL={LL:G}");
            return parts.Count > 0 ? string.Join("  ", parts) : "(없음)";
        }
    }

    // §4 ─ 역변환 ─────────────────────────────────────────────
    public AlarmRule ToAlarmRule() => new()
    {
        Id       = Id,
        Name     = Name,
        HH       = HH,       // ★ Fix: 정상 참조
        H        = H,
        L        = L,
        LL       = LL,       // ★ Fix: 정상 참조
        DeadBand = DeadBand,
        Message  = Message,
    };
}
