// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/ProtocolLibraryViewModel.cs
//  역할: 프로토콜 라이브러리 ViewModel — CRUD + 선택 관리
//        Scale/Alarm/Comm 라이브러리와 동일한 Add/Delete/MoveUp/MoveDown
//        커맨드 관례를 프로토콜 자체 + 읽기블록 + 쓰기블록 + 필드,
//        4단계 계층 모두에 동일하게 적용한다.
//  S-프로토콜01: 신규
//  S-프로토콜01 Step B 후속(2026-07-20): ScaleLibraryViewModel 주입 추가 —
//    필드 편집 UI(ProtocolLibraryView)에서 ScaleEntryId 콤보의 ItemsSource 로
//    ScaleLibrary.Entries 를 바인딩하기 위함(DeviceTreeViewModel 과 동일 패턴).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class ProtocolLibraryViewModel : ObservableObject
{
    // §0 ─ 라이브러리 참조 (★ S-프로토콜01 Step B 후속) ────────

    public ScaleLibraryViewModel ScaleLibrary { get; }

    public ProtocolLibraryViewModel(ScaleLibraryViewModel scaleLibrary)
    {
        ScaleLibrary = scaleLibrary;
    }

    // §1 ─ 목록 ───────────────────────────────────────────────

    public ObservableCollection<ProtocolEntry> Entries { get; } = new();

    // §2 ─ 선택 항목 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ProtocolEntry? _selectedEntry;

    public bool HasSelection => SelectedEntry is not null;

    /// <summary>프로토콜 전환 시 하위(블록·필드) 선택을 초기화 — 다른 항목의
    /// 블록이 잘못 선택된 상태로 남는 것을 방지.</summary>
    partial void OnSelectedEntryChanged(ProtocolEntry? value)
    {
        SelectedReadBlock  = null;
        SelectedWriteBlock = null;
    }

    [RelayCommand]
    private void AddEntry()
    {
        var entry = new ProtocolEntry { Name = $"프로토콜 {Entries.Count + 1}" };
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

    // §3 ─ 읽기 블록 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadBlockSelection))]
    private ProtocolBlock? _selectedReadBlock;

    public bool HasReadBlockSelection => SelectedReadBlock is not null;

    partial void OnSelectedReadBlockChanged(ProtocolBlock? value) => SelectedReadField = null;

    [RelayCommand]
    private void AddReadBlock()
    {
        if (SelectedEntry is null) return;
        var block = new ProtocolBlock { Name = $"읽기블록 {SelectedEntry.ReadBlocks.Count + 1}" };
        SelectedEntry.ReadBlocks.Add(block);
        SelectedReadBlock = block;
    }

    [RelayCommand]
    private void DeleteReadBlock()
    {
        if (SelectedEntry is null || SelectedReadBlock is null) return;
        SelectedEntry.ReadBlocks.Remove(SelectedReadBlock);
        SelectedReadBlock = SelectedEntry.ReadBlocks.LastOrDefault();
    }

    // §4 ─ 쓰기 블록 ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWriteBlockSelection))]
    private ProtocolBlock? _selectedWriteBlock;

    public bool HasWriteBlockSelection => SelectedWriteBlock is not null;

    partial void OnSelectedWriteBlockChanged(ProtocolBlock? value) => SelectedWriteField = null;

    [RelayCommand]
    private void AddWriteBlock()
    {
        if (SelectedEntry is null) return;
        var block = new ProtocolBlock { Name = $"쓰기블록 {SelectedEntry.WriteBlocks.Count + 1}" };
        SelectedEntry.WriteBlocks.Add(block);
        SelectedWriteBlock = block;
    }

    [RelayCommand]
    private void DeleteWriteBlock()
    {
        if (SelectedEntry is null || SelectedWriteBlock is null) return;
        SelectedEntry.WriteBlocks.Remove(SelectedWriteBlock);
        SelectedWriteBlock = SelectedEntry.WriteBlocks.LastOrDefault();
    }

    // §5 ─ 필드 (읽기/쓰기 블록 공용 — 선택된 블록 기준) ─────

    [ObservableProperty] private ProtocolField? _selectedReadField;
    [ObservableProperty] private ProtocolField? _selectedWriteField;

    [RelayCommand]
    private void AddReadField()
    {
        if (SelectedReadBlock is null) return;
        var field = new ProtocolField { Name = $"필드{SelectedReadBlock.Fields.Count + 1}" };
        SelectedReadBlock.Fields.Add(field);
        SelectedReadField = field;
    }

    [RelayCommand]
    private void DeleteReadField()
    {
        if (SelectedReadBlock is null || SelectedReadField is null) return;
        SelectedReadBlock.Fields.Remove(SelectedReadField);
        SelectedReadField = SelectedReadBlock.Fields.LastOrDefault();
    }

    [RelayCommand]
    private void AddWriteField()
    {
        if (SelectedWriteBlock is null) return;
        var field = new ProtocolField { Name = $"필드{SelectedWriteBlock.Fields.Count + 1}" };
        SelectedWriteBlock.Fields.Add(field);
        SelectedWriteField = field;
    }

    [RelayCommand]
    private void DeleteWriteField()
    {
        if (SelectedWriteBlock is null || SelectedWriteField is null) return;
        SelectedWriteBlock.Fields.Remove(SelectedWriteField);
        SelectedWriteField = SelectedWriteBlock.Fields.LastOrDefault();
    }

    // ★ S-프로토콜01 Step B 후속: 필드 스케일 연결 해제 (읽기/쓰기 공용 —
    //   대상 필드를 CommandParameter 로 직접 전달받으므로 블록 구분 불필요)
    [RelayCommand]
    private void ClearFieldScale(ProtocolField? field)
    {
        if (field is null) return;
        field.ScaleEntryId = null;
    }
}
