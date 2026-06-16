// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/AlarmLibraryViewModel.cs
//  역할: 알람 라이브러리 ViewModel — CRUD + 선택 관리
//  S-07: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class AlarmLibraryViewModel : ObservableObject
{
    // §1 ─ 목록 ───────────────────────────────────────────────

    public ObservableCollection<AlarmEntry> Entries { get; } = new();

    // §2 ─ 선택 항목 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AlarmEntry? _selectedEntry;

    public bool HasSelection => SelectedEntry is not null;

    // §3 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void AddEntry()
    {
        var entry = new AlarmEntry
        {
            Name = $"알람 {Entries.Count + 1}"
        };
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
}
