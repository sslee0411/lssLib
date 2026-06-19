// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/TagTemplateViewModel.cs
//  역할: 태그 템플릿 목록 CRUD
//  S-13B: 초기 구현
//  S-14 fix: AddItem/DeleteItem 후 OnPropertyChanged(nameof(Selected)) 추가
//            → Items가 ObservableCollection이어도 Selected 바인딩 재평가 필요
//  생성: 2026-06-18
//  수정: 2026-06-19
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using IIoT.Studio.Models;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels;

public partial class TagTemplateViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly TagTemplateService _svc;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public TagTemplateViewModel(TagTemplateService svc)
    {
        _svc = svc;
        _Load();
    }

    // §3 ─ 컬렉션 ─────────────────────────────────────────────

    public ObservableCollection<TagTemplate> Templates { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelected))]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    private TagTemplate? _selected;

    public bool IsSelected     => Selected is not null;
    public bool IsNoneSelected => Selected is null;

    // §4 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void Add()
    {
        var t = new TagTemplate
        {
            Name        = $"템플릿 {Templates.Count + 1}",
            Description = string.Empty
        };
        // 기본 항목 1개 (ObservableCollection이므로 Add 즉시 반영)
        t.Items.Add(new TagTemplateItem
        {
            Name       = "Tag1",
            ByteOffset = 0,
            BufType    = "FloatLE",
            Unit       = string.Empty
        });
        t.TotalBytes = 4;

        Templates.Add(t);
        Selected = t;
        _svc.Save(Templates);
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null) return;
        Templates.Remove(Selected);
        Selected = Templates.FirstOrDefault();
        _svc.Save(Templates);
    }

    [RelayCommand]
    private void Save() => _svc.Save(Templates);

    [RelayCommand]
    private void AddItem()
    {
        if (Selected is null) return;

        var prev   = Selected.Items.LastOrDefault();
        var offset = prev is not null
            ? prev.ByteOffset + prev.ByteSize
            : 0;

        Selected.Items.Add(new TagTemplateItem
        {
            Name       = $"Tag{Selected.Items.Count + 1}",
            ByteOffset = offset,
            BufType    = "FloatLE"
        });
        Selected.TotalBytes = Selected.Items.Sum(i => i.ByteSize);

        // ★ S-14 fix: Items(ObservableCollection)는 자동 갱신되지만
        //   우측 편집기 헤더(총 바이트·항목 수)는 Selected 바인딩이라
        //   Selected에 대한 PropertyChanged가 필요
        OnPropertyChanged(nameof(Selected));

        _svc.Save(Templates);
    }

    [RelayCommand]
    private void DeleteItem(TagTemplateItem? item)
    {
        if (Selected is null || item is null) return;
        Selected.Items.Remove(item);
        Selected.TotalBytes = Selected.Items.Sum(i => i.ByteSize);

        // ★ S-14 fix: Selected 바인딩 재평가
        OnPropertyChanged(nameof(Selected));

        _svc.Save(Templates);
    }

    // §5 ─ 내부 ───────────────────────────────────────────────

    private void _Load()
    {
        Templates.Clear();
        foreach (var t in _svc.Load())
            Templates.Add(t);
    }
}
