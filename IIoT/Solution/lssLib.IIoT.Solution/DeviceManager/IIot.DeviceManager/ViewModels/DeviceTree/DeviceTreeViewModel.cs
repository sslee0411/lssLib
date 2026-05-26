// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeViewModel.cs
//  역할: 장비 트리 전체 CRUD + 선택 관리 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 ~ v6 — 트리 구조, Sibling, 이중레이어 등
//  수정: 2025-05-26 v7 — IsEditing 잔존 버그 최종 수정
//        [근본 원인]
//        OnSelectedNodeChanged 훅은 [ObservableProperty] 소스제너레이터가
//        SelectedNode setter 내부에서 호출하므로, Add 커맨드 내부에서
//        SelectedNode = newNode 대입 시점에 oldValue 포인터가
//        이미 변경된 상태일 수 있음 → 프레임워크 훅 의존 제거
//        [해결]
//        _CommitCurrentEdit() 헬퍼를 Add* 커맨드 최상단에서 직접 호출
//        SelectedNode 변경 전에 현재 편집 노드를 명시적으로 확정
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

public partial class DeviceTreeViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private const string LogSrc = "DeviceTree";

    // §2 ─ 바인딩 속성 ────────────────────────────────────────

    public ObservableCollection<DeviceNodeViewModel> RootNodes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(AddGroupCommand),
        nameof(AddDeviceCommand),
        nameof(AddPlcCommand),
        nameof(AddTagCommand),
        nameof(AddSensorCommand),
        nameof(DeleteNodeCommand),
        nameof(MoveUpCommand),
        nameof(MoveDownCommand),
        nameof(AddSiblingGroupCommand),
        nameof(AddSiblingDeviceCommand),
        nameof(AddSiblingPlcCommand))]
    private DeviceNodeViewModel? _selectedNode;

    public int TotalNodeCount => RootNodes.SelectMany(r => r.Flatten()).Count();

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeViewModel()
    {
        RootNodes.CollectionChanged +=
            (_, _) => OnPropertyChanged(nameof(TotalNodeCount));
    }

    // §3-1 ─ SelectedNode 변경 훅 (보조용) ────────────────────

    /// <summary>
    /// ★ v7: OnSelectedNodeChanged 는 보조 역할만 담당.
    ///
    /// 주요 IsEditing 처리는 각 Add* 커맨드 내부의
    /// _CommitCurrentEdit() 직접 호출이 담당.
    ///
    /// 이 훅은 Add* 이외 경로(DeviceTreeView.xaml.cs 의 SelectedItemChanged,
    /// 기타 직접 SelectedNode 변경) 에서 oldValue.IsEditing 을 확정하는
    /// 보조 안전망으로 유지.
    /// </summary>
    partial void OnSelectedNodeChanged(DeviceNodeViewModel? oldValue, DeviceNodeViewModel? newValue)
    {
        if (oldValue is { IsEditing: true })
            oldValue.CommitEditCommand.Execute(null);

        _ = newValue;
    }

    // §3-2 ─ 편집 확정 헬퍼 (★ 핵심 수정) ────────────────────

    /// <summary>
    /// 현재 SelectedNode 가 편집 중이면 즉시 확정합니다.
    ///
    /// ★ 이 메서드를 모든 Add* 커맨드 최상단에서 호출합니다.
    ///
    /// 이유: [ObservableProperty] 소스제너레이터의 OnSelectedNodeChanged 훅은
    ///   SelectedNode setter 완료 후에 호출되므로, 대입 전 oldValue 가
    ///   정확히 전달되지 않을 수 있음.
    ///   직접 호출은 SelectedNode 변경 이전에 현재 노드의 IsEditing 상태를
    ///   명시적으로 확정하여 잔존 TextBox 문제를 원천 차단.
    /// </summary>
    private void _CommitCurrentEdit()
    {
        if (SelectedNode is { IsEditing: true } node)
        {
            node.CommitEditCommand.Execute(null);
            LogManager.Instance.Debug(LogSrc,
                $"[CommitEdit] {node.Name} ({node.Kind}) 편집 확정 → 새 노드 추가 전");
        }
    }

    // §4 ─ 하위(Child) 추가 커맨드 ───────────────────────────

    [RelayCommand(CanExecute = nameof(CanAddGroup))]
    private void AddGroup()
    {
        _CommitCurrentEdit(); // ★ 반드시 최상단 호출

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
        LogManager.Instance.Info(LogSrc, $"[하위] 그룹 추가: {group.Name}");
    }

    private bool CanAddGroup() => SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    [RelayCommand(CanExecute = nameof(CanAddDevice))]
    private void AddDevice()
    {
        _CommitCurrentEdit(); // ★

        var device = new DeviceItemViewModel();
        switch (SelectedNode)
        {
            case GroupNodeViewModel g: g.AddChild(device); g.IsExpanded = true; break;
            case DeviceItemViewModel d: d.AddChild(device); d.IsExpanded = true; break;
            case PlcNodeViewModel p: p.AddChild(device); p.IsExpanded = true; break;
            default: RootNodes.Add(device); device.Parent = null; break;
        }
        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] 장비 추가: {device.Name}");
    }

    private bool CanAddDevice() => SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    [RelayCommand(CanExecute = nameof(CanAddPlc))]
    private void AddPlc()
    {
        _CommitCurrentEdit(); // ★

        int slotNo = SelectedNode switch
        {
            DeviceItemViewModel d => d.Children.OfType<PlcNodeViewModel>().Count(),
            PlcNodeViewModel p => p.Children.OfType<PlcNodeViewModel>().Count(),
            GroupNodeViewModel g => g.Children.OfType<PlcNodeViewModel>().Count(),
            _ => RootNodes.OfType<PlcNodeViewModel>().Count(),
        };
        var plc = new PlcNodeViewModel(slotNo: slotNo);
        switch (SelectedNode)
        {
            case GroupNodeViewModel g: g.AddChild(plc); g.IsExpanded = true; break;
            case DeviceItemViewModel d: d.AddChild(plc); d.IsExpanded = true; break;
            case PlcNodeViewModel p: p.AddChild(plc); p.IsExpanded = true; break;
            default: RootNodes.Add(plc); plc.Parent = null; break;
        }
        SelectedNode = plc;
        plc.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] PLC 추가: {plc.Name}");
    }

    private bool CanAddPlc() => SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        _CommitCurrentEdit(); // ★

        if (SelectedNode is not (PlcNodeViewModel or DeviceItemViewModel or SensorNodeViewModel)) return;
        var tag = new TagNodeViewModel();
        SelectedNode.AddChild(tag);
        SelectedNode.IsExpanded = true;
        SelectedNode = tag;
        tag.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] 태그 추가: {tag.Name}");
    }

    private bool CanAddTag() =>
        SelectedNode is PlcNodeViewModel or DeviceItemViewModel or SensorNodeViewModel;

    [RelayCommand(CanExecute = nameof(CanAddSensor))]
    private void AddSensor()
    {
        _CommitCurrentEdit(); // ★

        if (SelectedNode is not DeviceItemViewModel) return;
        var sensor = new SensorNodeViewModel();
        SelectedNode.AddChild(sensor);
        SelectedNode.IsExpanded = true;
        SelectedNode = sensor;
        sensor.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] 센서 추가: {sensor.Name}");
    }

    private bool CanAddSensor() => SelectedNode is DeviceItemViewModel;

    // §5 ─ 같은 레벨(Sibling) 추가 커맨드 ───────────────────

    [RelayCommand(CanExecute = nameof(CanAddSiblingGroup))]
    private void AddSiblingGroup()
    {
        _CommitCurrentEdit(); // ★

        var group = new GroupNodeViewModel();
        if (SelectedNode is null) { RootNodes.Add(group); group.Parent = null; }
        else _InsertSibling(group);
        SelectedNode = group;
        group.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 그룹 추가: {group.Name}");
    }

    private bool CanAddSiblingGroup()
    {
        if (SelectedNode is null) return true;
        if (SelectedNode is TagNodeViewModel or SensorNodeViewModel) return false;
        return SelectedNode.Parent is null or GroupNodeViewModel;
    }

    [RelayCommand(CanExecute = nameof(CanAddSiblingDevice))]
    private void AddSiblingDevice()
    {
        _CommitCurrentEdit(); // ★

        var device = new DeviceItemViewModel();
        if (SelectedNode is null) { RootNodes.Add(device); device.Parent = null; }
        else _InsertSibling(device);
        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 장비 추가: {device.Name}");
    }

    private bool CanAddSiblingDevice() =>
        SelectedNode is null ||
        SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    [RelayCommand(CanExecute = nameof(CanAddSiblingPlc))]
    private void AddSiblingPlc()
    {
        _CommitCurrentEdit(); // ★

        int slotNo = SelectedNode is null
            ? RootNodes.OfType<PlcNodeViewModel>().Count()
            : _GetSiblingPlcCount();
        var plc = new PlcNodeViewModel(slotNo: slotNo);
        if (SelectedNode is null) { RootNodes.Add(plc); plc.Parent = null; }
        else _InsertSibling(plc);
        SelectedNode = plc;
        plc.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] PLC 추가: {plc.Name}");
    }

    private bool CanAddSiblingPlc() =>
        SelectedNode is null ||
        SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    // §6 ─ 삭제 커맨드 ────────────────────────────────────────

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

    // §7 ─ 이동 커맨드 ────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedNode is null) return;
        var list = GetSiblingList(SelectedNode);
        if (list is null) return;
        int idx = list.IndexOf(SelectedNode);
        if (idx <= 0) return;
        list.Move(idx, idx - 1);
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveUp()
    {
        if (SelectedNode is null) return false;
        var list = GetSiblingList(SelectedNode);
        return list is not null && list.IndexOf(SelectedNode) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedNode is null) return;
        var list = GetSiblingList(SelectedNode);
        if (list is null) return;
        int idx = list.IndexOf(SelectedNode);
        if (idx < 0 || idx >= list.Count - 1) return;
        list.Move(idx, idx + 1);
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveDown()
    {
        if (SelectedNode is null) return false;
        var list = GetSiblingList(SelectedNode);
        return list is not null &&
               list.IndexOf(SelectedNode) < list.Count - 1;
    }

    // §8 ─ 펼치기/접기 ────────────────────────────────────────

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var node in RootNodes.SelectMany(n => n.Flatten()))
            node.IsExpanded = true;
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in RootNodes.SelectMany(n => n.Flatten()))
            node.IsExpanded = false;
    }

    // §9 ─ 샘플 데이터 ────────────────────────────────────────

    [RelayCommand]
    private void LoadSample()
    {
        RootNodes.Clear();

        var g1 = new GroupNodeViewModel("Line-1 (생산라인)");
        var dev1 = new DeviceItemViewModel("압연기-001") { Manufacturer = "POSCO", Model = "RM-100" };

        var plc1 = new PlcNodeViewModel("PLC-SIEMENS", slotNo: 0) { UnitId = 1, ProtocolType = "ModbusTCP" };
        plc1.AddChild(new TagNodeViewModel("temp_raw") { Address = "40001", BufType = "Int16BE", PollMs = 1000 });
        plc1.AddChild(new TagNodeViewModel("press_hi") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        plc1.AddChild(new TagNodeViewModel("press_lo") { Address = "40005", BufType = "FloatBE", PollMs = 500 });
        plc1.AddChild(new TagNodeViewModel("motor_run") { Address = "M0.0", BufType = "Bool", PollMs = 200 });
        dev1.AddChild(plc1);

        var sen1 = new SensorNodeViewModel("베어링온도1")
        {
            SensorType = "Temperature",
            Unit = "°C",
            Description = "압연기 1번 베어링 온도",
            AlarmHigh = 120.0,
            AlarmHighHigh = 140.0,
            AlarmLow = 5.0
        };
        sen1.AddTagRef("temp_raw_id", "temp_raw", "primary");
        dev1.AddChild(sen1);

        var sen2 = new SensorNodeViewModel("차압센서1")
        {
            SensorType = "Pressure",
            Unit = "kPa",
            Formula = "high - low",
            AlarmHigh = 85.0,
            AlarmLow = 2.0
        };
        sen2.AddTagRef("press_hi_id", "press_hi", "high");
        sen2.AddTagRef("press_lo_id", "press_lo", "low");
        dev1.AddChild(sen2);

        g1.AddChild(dev1);
        RootNodes.Add(g1);

        var rootPlc = new PlcNodeViewModel("PLC-001 (루트직속)", slotNo: 0) { UnitId = 1 };
        rootPlc.AddChild(new TagNodeViewModel("온도_CH1") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        rootPlc.AddChild(new TagNodeViewModel("압력_CH1") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        RootNodes.Add(rootPlc);

        SelectedNode = g1;
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, "샘플 데이터 로드 완료");
    }

    // §10 ─ 내부 헬퍼 ─────────────────────────────────────────

    private void _InsertSibling(DeviceNodeViewModel newNode)
    {
        if (SelectedNode is null) return;
        var list = GetSiblingList(SelectedNode);
        if (list is null) return;
        int idx = list.IndexOf(SelectedNode);
        newNode.Parent = SelectedNode.Parent;
        if (idx >= 0 && idx < list.Count - 1)
            list.Insert(idx + 1, newNode);
        else
            list.Add(newNode);
    }

    private int _GetSiblingPlcCount()
    {
        if (SelectedNode is null) return 0;
        var list = GetSiblingList(SelectedNode);
        return list?.OfType<PlcNodeViewModel>().Count() ?? 0;
    }

    private ObservableCollection<DeviceNodeViewModel>? GetSiblingList(DeviceNodeViewModel node)
    {
        if (node.Parent is not null) return node.Parent.Children;
        if (RootNodes.Contains(node)) return RootNodes;
        return null;
    }
}