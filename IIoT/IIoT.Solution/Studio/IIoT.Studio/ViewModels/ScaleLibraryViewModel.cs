// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/ScaleLibraryViewModel.cs
//  역할: 스케일 라이브러리 ViewModel
//        목록 CRUD + 선택 편집기 관리
//  S-06: 초기 구현
//  S-26: 미리보기 계산기 프로퍼티 추가
//        PreviewRaw  → PreviewEng  (Raw → 공학단위 계산)
//        ReverseEng  → ReverseRaw  (공학단위 → Raw 역방향 계산)
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class ScaleLibraryViewModel : ObservableObject
{
    // §1 ─ 목록 ───────────────────────────────────────────────

    public ObservableCollection<ScaleEntry> Entries { get; } = new();

    // §2 ─ 선택 항목 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    // ★ S-26: 선택 변경 시 미리보기 갱신
    [NotifyPropertyChangedFor(nameof(PreviewEng))]
    [NotifyPropertyChangedFor(nameof(ReverseRaw))]
    [NotifyPropertyChangedFor(nameof(PreviewEngText))]
    [NotifyPropertyChangedFor(nameof(ReverseRawText))]
    private ScaleEntry? _selectedEntry;

    public bool HasSelection => SelectedEntry is not null;

    // §3 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void AddEntry()
    {
        var entry = new ScaleEntry { Name = $"스케일 {Entries.Count + 1}" };
        Entries.Add(entry);
        SelectedEntry = entry;
    }

    [RelayCommand]
    private void DeleteEntry()
    {
        if (SelectedEntry is null) return;
        Entries.Remove(SelectedEntry);
        SelectedEntry = Entries.LastOrDefault();
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx <= 0) return;
        Entries.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx < 0 || idx >= Entries.Count - 1) return;
        Entries.Move(idx, idx + 1);
    }

    // §4 ─ ★ S-26: 미리보기 계산기 ──────────────────────────

    /// <summary>Raw → 공학단위 계산 입력값</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEng))]
    [NotifyPropertyChangedFor(nameof(PreviewEngText))]
    private string _previewRaw = "0";

    /// <summary>공학단위 → Raw 역방향 계산 입력값</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReverseRaw))]
    [NotifyPropertyChangedFor(nameof(ReverseRawText))]
    private string _reverseEng = "0";

    /// <summary>Raw → 공학단위 계산 결과 (double)</summary>
    public double PreviewEng
    {
        get
        {
            var s = SelectedEntry;
            if (s is null) return 0;
            if (!double.TryParse(PreviewRaw, out var raw)) return 0;
            return _CalcEng(s, raw);
        }
    }

    /// <summary>공학단위 → Raw 역방향 계산 결과 (double)</summary>
    public double ReverseRaw
    {
        get
        {
            var s = SelectedEntry;
            if (s is null) return 0;
            if (!double.TryParse(ReverseEng, out var eng)) return 0;
            return _CalcRaw(s, eng);
        }
    }

    /// <summary>표시용 문자열 (소수점 + 단위 포함)</summary>
    public string PreviewEngText
    {
        get
        {
            var s = SelectedEntry;
            if (s is null) return "-";
            if (!double.TryParse(PreviewRaw, out var raw)) return "입력 오류";
            var eng  = _CalcEng(s, raw);
            var dp   = s.DecimalPlaces;
            var unit = string.IsNullOrEmpty(s.Unit) ? "" : $" {s.Unit}";
            var fmt  = "F" + dp;
            return $"{eng.ToString(fmt)}{unit}";
        }
    }

    /// <summary>역방향 표시용 문자열</summary>
    public string ReverseRawText
    {
        get
        {
            var s = SelectedEntry;
            if (s is null) return "-";
            if (!double.TryParse(ReverseEng, out var eng)) return "입력 오류";
            var raw = _CalcRaw(s, eng);
            return $"{raw:F2}";
        }
    }

    // §5 ─ 내부 계산 헬퍼 ─────────────────────────────────────

    /// <summary>Raw → 공학단위 선형 변환</summary>
    private static double _CalcEng(ScaleEntry s, double raw)
    {
        if (s.Mode == ScaleMode.Expression)
        {
            // Expression 모드: NCalc 미지원 → 근사 선형 계산 (Collector에서 NCalc 사용)
            // 편집기 미리보기는 선형으로 대체
        }
        if (s.RawMax == s.RawMin) return s.EngMin;
        return s.EngMin + (raw - s.RawMin)
               * (s.EngMax - s.EngMin) / (s.RawMax - s.RawMin);
    }

    /// <summary>공학단위 → Raw 역방향 선형 변환</summary>
    private static double _CalcRaw(ScaleEntry s, double eng)
    {
        if (s.EngMax == s.EngMin) return s.RawMin;
        return s.RawMin + (eng - s.EngMin)
               * (s.RawMax - s.RawMin) / (s.EngMax - s.EngMin);
    }
}
