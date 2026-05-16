// ══════════════════════════════════════════════════════════
//  IIoT.UI.Themes · ThemeSelectorViewModel.cs
//  역할: 테마 선택 패널 ViewModel
//  수정: 2025-05-16 v1.1
//    FIX 5 — IDisposable 구현: ThemeChanged 정적 이벤트 구독 해제
//             미해제 시 ViewModel이 GC 대상에서 제외되는 메모리 누수 발생
// ══════════════════════════════════════════════════════════
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace IIoT.UI.Themes;

/// <summary>
/// 테마 선택 UI ViewModel.
/// ★ IDisposable 구현 — ThemeManager.ThemeChanged(정적 이벤트) 구독 해제 필수.
///   정적 이벤트 미해제 시 ViewModel 인스턴스가 GC되지 않아 메모리 누수 발생.
///   사용측(View)에서 Unloaded 이벤트 또는 Window 닫힐 때 Dispose() 호출.
/// </summary>
public sealed partial class ThemeSelectorViewModel : ObservableObject, IDisposable
{
    // §1 ─ 테마 목록 ─────────────────────────────────────────
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

    // §3 ─ 구독 상태 추적 (Dispose 안전성) ────────────────────
    private bool _disposed;
    private readonly Action<ThemeKind> _themeChangedHandler;

    // §4 ─ 생성자 ─────────────────────────────────────────────
    public ThemeSelectorViewModel()
    {
        // 현재 테마를 초기 선택값으로 설정
        _selectedTheme = Themes.FirstOrDefault(t => t.Kind == ThemeManager.Current);

        // ★ 핸들러를 필드로 보관해야 동일 인스턴스로 -= 해제 가능
        _themeChangedHandler = OnThemeChanged;
        ThemeManager.ThemeChanged += _themeChangedHandler;
    }

    // §5 ─ 명령 ───────────────────────────────────────────────

    [RelayCommand]
    private void SelectTheme(ThemeItemViewModel? item)
    {
        if (item == null || item.Kind == ThemeManager.Current) return;
        ThemeManager.Apply(item.Kind);
        SelectedTheme = item;
        foreach (var t in Themes)
            t.UpdateSelected(item.Kind);
    }

    [RelayCommand]
    private void NextTheme()
    {
        var next = (ThemeKind)(((int)ThemeManager.Current + 1) % ThemeManager.AllThemes.Count);
        SelectTheme(Themes.First(t => t.Kind == next));
    }

    [RelayCommand]
    private void PrevTheme()
    {
        var count = ThemeManager.AllThemes.Count;
        var prev  = (ThemeKind)(((int)ThemeManager.Current - 1 + count) % count);
        SelectTheme(Themes.First(t => t.Kind == prev));
    }

    // §6 ─ 외부 테마 변경 처리 ────────────────────────────────
    private void OnThemeChanged(ThemeKind kind)
    {
        SelectedTheme = Themes.FirstOrDefault(t => t.Kind == kind);
        foreach (var item in Themes)
            item.UpdateSelected(kind);
    }

    // §7 ─ ★ FIX 5: IDisposable 구현 ─────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        ThemeManager.ThemeChanged -= _themeChangedHandler;
        _disposed = true;
    }
}

// ── 개별 테마 항목 ViewModel ──────────────────────────────

public sealed partial class ThemeItemViewModel : ObservableObject
{
    private readonly ThemeInfo _info;

    public ThemeKind Kind        => _info.Kind;
    public string DisplayName    => _info.DisplayName;
    public string Description    => _info.Description;
    public bool   IsDark         => _info.IsDark;
    public string AccentHex      =>
        $"#{_info.AccentColor.R:X2}{_info.AccentColor.G:X2}{_info.AccentColor.B:X2}";
    public string ModeLabel      => IsDark ? "Dark" : "Light";

    [ObservableProperty] private bool _isSelected;

    public ThemeItemViewModel(ThemeInfo info)
    {
        _info       = info;
        _isSelected = info.Kind == ThemeManager.Current;
    }

    public void UpdateSelected(ThemeKind current)
        => IsSelected = Kind == current;
}
