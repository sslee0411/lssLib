// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/ScaleLibraryViewModel.cs
//  역할: 스케일 라이브러리 ViewModel
//        목록 CRUD + 선택 편집기 관리
//  S-06: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class ScaleLibraryViewModel : ObservableObject
{
    // §1 ─ 목록 ───────────────────────────────────────────────

    /// <summary>스케일 항목 목록 (ListView ItemsSource)</summary>
    public ObservableCollection<ScaleEntry> Entries { get; } = new();

    // §2 ─ 선택 항목 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ScaleEntry? _selectedEntry;

    /// <summary>항목 선택 여부 (편집기 표시 제어)</summary>
    public bool HasSelection => SelectedEntry is not null;

    // §3 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>새 스케일 항목 추가</summary>
    [RelayCommand]
    private void AddEntry()
    {
        var entry = new ScaleEntry
        {
            Name = $"스케일 {Entries.Count + 1}"
        };
        Entries.Add(entry);
        SelectedEntry = entry;
    }

    /// <summary>선택 항목 삭제</summary>
    [RelayCommand]
    private void DeleteEntry()
    {
        if (SelectedEntry is null) return;
        Entries.Remove(SelectedEntry);
        SelectedEntry = Entries.LastOrDefault();
    }

    /// <summary>선택 항목 위로 이동</summary>
    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx <= 0) return;
        Entries.Move(idx, idx - 1);
    }

    /// <summary>선택 항목 아래로 이동</summary>
    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx < 0 || idx >= Entries.Count - 1) return;
        Entries.Move(idx, idx + 1);
    }
}
