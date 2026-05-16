// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes · ThemeSelectorViewModel.cs
//  역할: 테마 선택 패널/팝업 ViewModel
//        MVVM — CommunityToolkit.Mvvm 사용
//  생성: 2025-05-16
// ══════════════════════════════════════════════════════════

// [NUGET: CommunityToolkit.Mvvm]
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace IIoT.UI.Themes;

/// <summary>
/// 테마 선택 UI ViewModel.
/// 7가지 테마 목록을 바인딩하고, 선택 시 즉시 ThemeManager.Apply()를 호출한다.
/// </summary>
public sealed partial class ThemeSelectorViewModel : ObservableObject
{
    // §1 ─ 테마 목록 ─────────────────────────────────────────
    /// <summary>7가지 테마 메타 정보 목록</summary>
    public IReadOnlyList<ThemeItemViewModel> Themes { get; }
        = ThemeManager.AllThemes
            .Select(t => new ThemeItemViewModel(t))
            .ToList();

    // §2 ─ 선택 상태 ─────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentThemeDisplay))]
    private ThemeItemViewModel? _selectedTheme;

    public string CurrentThemeDisplay =>
        SelectedTheme?.DisplayName ?? "테마 선택";

    // §3 ─ 생성자 ─────────────────────────────────────────────
    public ThemeSelectorViewModel()
    {
        // 현재 적용된 테마를 초기 선택값으로
        _selectedTheme = Themes.FirstOrDefault(t => t.Kind == ThemeManager.Current);

        // ThemeManager 외부 변경 구독
        ThemeManager.ThemeChanged += kind =>
        {
            SelectedTheme = Themes.FirstOrDefault(t => t.Kind == kind);
            foreach (var item in Themes)
                item.UpdateSelected(kind);
        };
    }

    // §4 ─ 명령 ───────────────────────────────────────────────

    /// <summary>테마 항목 클릭 시 즉시 적용</summary>
    [RelayCommand]
    private void SelectTheme(ThemeItemViewModel? item)
    {
        if (item == null || item.Kind == ThemeManager.Current) return;

        // ① 즉시 테마 적용 (DynamicResource가 전체 UI 자동 갱신)
        ThemeManager.Apply(item.Kind);

        // ② 선택 상태 UI 갱신
        SelectedTheme = item;
        foreach (var t in Themes)
            t.UpdateSelected(item.Kind);
    }

    /// <summary>다음 테마로 순환 (단축키 등에서 사용)</summary>
    [RelayCommand]
    private void NextTheme()
    {
        var idx = (int)ThemeManager.Current;
        var next = (ThemeKind)((idx + 1) % ThemeManager.AllThemes.Count);
        SelectTheme(Themes.First(t => t.Kind == next));
    }

    /// <summary>이전 테마로 순환</summary>
    [RelayCommand]
    private void PrevTheme()
    {
        var idx = (int)ThemeManager.Current;
        var count = ThemeManager.AllThemes.Count;
        var prev = (ThemeKind)((idx - 1 + count) % count);
        SelectTheme(Themes.First(t => t.Kind == prev));
    }
}

// ── 개별 테마 항목 ViewModel ──────────────────────────────

/// <summary>테마 선택 목록의 개별 항목</summary>
public sealed partial class ThemeItemViewModel : ObservableObject
{
    // §1 ─ 데이터 ────────────────────────────────────────────
    private readonly ThemeInfo _info;

    public ThemeKind Kind => _info.Kind;
    public string DisplayName => _info.DisplayName;
    public string Description => _info.Description;
    public bool IsDark => _info.IsDark;

    /// <summary>액센트 색상 (16진수 문자열 — XAML Brush 변환용)</summary>
    public string AccentHex =>
        $"#{_info.AccentColor.R:X2}{_info.AccentColor.G:X2}{_info.AccentColor.B:X2}";

    /// <summary>다크/라이트 표시 레이블</summary>
    public string ModeLabel => IsDark ? "Dark" : "Light";

    // §2 ─ 선택 상태 ─────────────────────────────────────────
    [ObservableProperty] private bool _isSelected;

    public ThemeItemViewModel(ThemeInfo info)
    {
        _info = info;
        _isSelected = info.Kind == ThemeManager.Current;
    }

    /// <summary>선택 상태를 외부에서 갱신</summary>
    public void UpdateSelected(ThemeKind current)
        => IsSelected = Kind == current;
}