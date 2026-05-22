// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeViewModel.cs
//  역할: 장비 트리 전체 CRUD + 선택 관리 ViewModel
//  생성: 2025-05-22
// ══════════════════════════════════════════════════════════

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 장비 트리 루트 ViewModel.
/// Group / Device / PLC / Tag CRUD + 전체 펼치기·접기·이동 지원.
/// </summary>
public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private const string LogSrc = "DeviceTree";

    // §2 ─ 바인딩 속성 ────────────────────────────────────────

    /// <summary>루트 그룹 목록</summary>
    public ObservableCollection<DeviceNodeViewModel> RootNodes { get; } = [];

    /// <summary>
    /// 현재 선택한 항목(SelectedNode)이 무엇이냐에 따라, 화면에 있는 추가·삭제·이동 버튼들의 활성화(클릭 가능) 상태를 자동으로 실시간 업데이트해 주는 코드
    /// 
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(AddDeviceCommand),
        nameof(AddPlcCommand),
        nameof(AddTagCommand),
        nameof(DeleteNodeCommand),
        nameof(MoveUpCommand),
        nameof(MoveDownCommand))]
    private DeviceNodeViewModel? _selectedNode;

    /// <summary>트리 전체 노드 수 (상태표시줄용)</summary>
    public int TotalNodeCount => RootNodes
        .SelectMany(r => r.Flatten())
        .Count();

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeViewModel()
    {
        RootNodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TotalNodeCount));
    }

    // §4 ─ 추가 커맨드 ────────────────────────────────────────

    /// <summary>
    /// 루트 또는 선택 노드 하위에 그룹 추가.
    /// Group 은 항상 추가 가능.
    /// </summary>
    [RelayCommand]
    private void AddGroup()
    {
        var group = new GroupNodeViewModel();
        if (SelectedNode is GroupNodeViewModel parent)
        {
            parent.AddChild(group);
            parent.IsExpanded = true;
        }
        else
        {
            RootNodes.Add(group);
            group.Parent = null;
        }
        SelectedNode = group;
        group.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"그룹 추가: {group.Id}");
    }

    /// <summary>
    /// 선택된 Group 하위에 Device 추가.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddDevice))]
    private void AddDevice()
    {
        if (SelectedNode is not GroupNodeViewModel parent) return;
        var device = new DeviceItemViewModel();
        parent.AddChild(device);
        parent.IsExpanded = true;
        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"장비 추가: {device.Id} → 부모 {parent.Name}");
    }

    private bool CanAddDevice() =>
        SelectedNode is GroupNodeViewModel;

    /// <summary>
    /// 선택된 Device 하위에 PLC 추가.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddPlc))]
    private void AddPlc()
    {
        if (SelectedNode is not DeviceItemViewModel parent) return;
        var plc = new PlcNodeViewModel(slotNo: parent.Children
            .OfType<PlcNodeViewModel>().Count());
        parent.AddChild(plc);
        parent.IsExpanded = true;
        SelectedNode = plc;
        plc.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"PLC 추가: {plc.Id} → 부모 {parent.Name}");
    }

    private bool CanAddPlc() =>
        SelectedNode is DeviceItemViewModel;

    /// <summary>
    /// 선택된 Device 또는 PLC 하위에 Tag 추가.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        if (SelectedNode is not (DeviceItemViewModel or PlcNodeViewModel)) return;
        var tag = new TagNodeViewModel();
        SelectedNode.AddChild(tag);
        SelectedNode.IsExpanded = true;
        SelectedNode = tag;
        tag.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"태그 추가: {tag.Id}");
    }

    private bool CanAddTag() =>
        SelectedNode is DeviceItemViewModel or PlcNodeViewModel;

    // §5 ─ 삭제 커맨드 ────────────────────────────────────────

    /// <summary>
    /// 선택된 노드 삭제 (하위 전체 포함).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteNode()
    {
        if (SelectedNode is null) return;

        var target = SelectedNode;
        var parent = target.Parent;

        if (parent is not null)
        {
            parent.RemoveChild(target);
            SelectedNode = parent;
        }
        else
        {
            RootNodes.Remove(target);
            SelectedNode = RootNodes.LastOrDefault();
        }
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"노드 삭제: {target.Name} ({target.Kind})");
    }

    private bool CanDelete() => SelectedNode is not null;

    // §6 ─ 이동 커맨드 ────────────────────────────────────────

    /// <summary>선택 노드를 같은 부모 내에서 위로 이동</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedNode is null) return;
        var list = GetSiblingList(SelectedNode);
        if (list is null) return;
        int idx = list.IndexOf(SelectedNode);
        if (idx <= 0) return;
        list.Move(idx, idx - 1);
    }

    private bool CanMoveUp()
    {
        if (SelectedNode is null) return false;
        var list = GetSiblingList(SelectedNode);
        return list is not null && list.IndexOf(SelectedNode) > 0;
    }

    /// <summary>선택 노드를 같은 부모 내에서 아래로 이동</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedNode is null) return;
        var list = GetSiblingList(SelectedNode);
        if (list is null) return;
        int idx = list.IndexOf(SelectedNode);
        if (idx < 0 || idx >= list.Count - 1) return;
        list.Move(idx, idx + 1);
    }

    private bool CanMoveDown()
    {
        if (SelectedNode is null) return false;
        var list = GetSiblingList(SelectedNode);
        return list is not null &&
               list.IndexOf(SelectedNode) < list.Count - 1;
    }

    // §7 ─ 펼치기/접기 ────────────────────────────────────────

    /// <summary>트리 전체 펼치기</summary>
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var node in RootNodes.SelectMany(n => n.Flatten()))
            node.IsExpanded = true;
    }

    /// <summary>트리 전체 접기</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in RootNodes.SelectMany(n => n.Flatten()))
            node.IsExpanded = false;
    }

    // §8 ─ 모델 변환 ──────────────────────────────────────────

    /// <summary>
    /// 샘플 데이터 로드 (개발/미리보기용).
    /// Phase 6 에서 JsonConfigLoader 로 대체 예정.
    /// </summary>
    [RelayCommand]
    private void LoadSample()
    {
        RootNodes.Clear();

        var g1 = new GroupNodeViewModel("1공장");
        var d1 = new DeviceItemViewModel("압축기 #1") { Manufacturer = "Atlas Copco", Model = "GA90" };
        var p1 = new PlcNodeViewModel("Modbus Slot", slotNo: 0) { UnitId = 1 };

        p1.AddChild(new TagNodeViewModel("온도") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        p1.AddChild(new TagNodeViewModel("압력") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        p1.AddChild(new TagNodeViewModel("전류") { Address = "40005", BufType = "FloatBE", PollMs = 1000 });

        d1.AddChild(p1);
        g1.AddChild(d1);

        var d2 = new DeviceItemViewModel("냉각기 #1") { Manufacturer = "Danfoss", Model = "FC302" };
        d2.AddChild(new TagNodeViewModel("RPM") { Address = "40001", BufType = "UInt16BE", PollMs = 200 });
        d2.AddChild(new TagNodeViewModel("전압") { Address = "40003", BufType = "FloatBE", PollMs = 1000 });
        g1.AddChild(d2);

        var g2 = new GroupNodeViewModel("2공장");
        var d3 = new DeviceItemViewModel("펌프 #1");
        d3.AddChild(new TagNodeViewModel("유량") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        g2.AddChild(d3);

        RootNodes.Add(g1);
        RootNodes.Add(g2);

        SelectedNode = g1;
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, "샘플 데이터 로드 완료");
    }

    // §9 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>노드가 속한 형제 목록 반환 (루트 또는 부모 Children)</summary>
    private ObservableCollection<DeviceNodeViewModel>? GetSiblingList(DeviceNodeViewModel node)
    {
        if (node.Parent is not null)
            return node.Parent.Children;
        if (RootNodes.Contains(node))
            return RootNodes;
        return null;
    }
}