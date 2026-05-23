// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · DeviceTreeViewModel.cs
//  역할: 장비 트리 전체 CRUD + 선택 관리 ViewModel
//  생성: 2025-05-22
//  수정: 2025-05-23 v2 — 트리 구조 유연화
//        ① AddDevice: 그룹 선택 없이도 루트에 장비 추가 가능
//                     Device / Plc 하위에도 장비 추가 가능
//        ② AddPlc:    그룹·장비 선택 없이도 루트에 PLC 추가 가능
//                     Device / Plc 하위에도 PLC 추가 가능
//        ③ CanAddDevice: Tag 가 아닌 모든 상태에서 활성
//        ④ CanAddPlc:    Tag 가 아닌 모든 상태에서 활성
//        ⑤ CanAddTag:    Device 또는 Plc 선택 시만 활성 (기존 유지)
//  수정: 2025-05-23 v3 — 같은 레벨(Sibling) 추가 커맨드 3종 신규
//        AddSiblingGroupCommand  — 선택 노드와 같은 레벨에 그룹 추가
//        AddSiblingDeviceCommand — 선택 노드와 같은 레벨에 장비 추가
//        AddSiblingPlcCommand    — 선택 노드와 같은 레벨에 PLC 추가
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.ViewModels.DeviceTree;

/// <summary>
/// 장비 트리 루트 ViewModel.
///
/// 지원하는 트리 구조:
///   · 그룹 없이 Device / PLC 를 루트에 바로 배치 가능
///   · Device · PLC 하위에 Device · PLC 중첩 연결 가능
///   · Tag 는 Device 또는 PLC 하위에만 배치 가능
///
/// 예시:
///   [Root]
///   ├─ PLC-001              (루트 직속 장비)
///   │   ├─ PLC-001-CH1      (하위 장비/채널)
///   │   │   ├─ 온도_Tag
///   │   │   └─ 압력_Tag
///   │   └─ 온도_Tag
///   ├─ 1공장 (Group)
///   │   └─ 압출기-001 (Device)
///   │       └─ RPM_Tag
///   └─ PLC-002              (루트 직속 PLC)
/// ── 하위(Child) 추가 ────────────────────────────────────
///   AddGroupCommand   : 선택 노드 하위에 그룹 추가
///   AddDeviceCommand  : 선택 노드 하위에 장비 추가 (선택 없으면 루트)
///   AddPlcCommand     : 선택 노드 하위에 PLC 추가 (선택 없으면 루트)
///   AddTagCommand     : Device/Plc 하위에 태그 추가
///
/// ── 같은 레벨(Sibling) 추가 ─────────────────────────────
///   AddSiblingGroupCommand  : 선택 노드와 같은 레벨에 그룹 추가
///   AddSiblingDeviceCommand : 선택 노드와 같은 레벨에 장비 추가
///   AddSiblingPlcCommand    : 선택 노드와 같은 레벨에 PLC 추가
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
        nameof(MoveDownCommand),
        nameof(AddSiblingGroupCommand),
        nameof(AddSiblingDeviceCommand),
        nameof(AddSiblingPlcCommand))]
    private DeviceNodeViewModel? _selectedNode;

    /// <summary>트리 전체 노드 수 (상태표시줄용)</summary>
    public int TotalNodeCount => RootNodes
        .SelectMany(r => r.Flatten())
        .Count();

    // §3 ─ 생성자 ─────────────────────────────────────────────

    public DeviceTreeViewModel()
    {
        RootNodes.CollectionChanged +=
            (_, _) => OnPropertyChanged(nameof(TotalNodeCount));
    }

    // §4 ─ 하위(Child) 추가 커맨드 ───────────────────────────

    /// <summary>
    /// 그룹 추가.
    /// · Group 선택 시 → 하위 그룹
    /// · 그 외 (선택 없음 / Device / Plc / Tag 선택) → 루트 그룹
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
        LogManager.Instance.Info(LogSrc, $"[하위] 그룹 추가: {group.Name}");
    }

    /// <summary>
    /// 장비(Device) 추가.
    ///
    /// ★ v2: 어디서나 장비 추가 가능
    ///   · 선택 없음 / Tag 선택      → 루트에 추가
    ///   · Group 선택                → 그룹 하위에 추가
    ///   · Device / Plc 선택         → 해당 장비 하위에 추가 (중첩)
    /// 선택 노드 하위에 장비 추가.
    /// 선택 없음 / Tag 선택 → 루트에 추가
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddDevice))]
    private void AddDevice()
    {
        var device = new DeviceItemViewModel();

        switch (SelectedNode)
        {
            case GroupNodeViewModel group:
                group.AddChild(device);
                group.IsExpanded = true;
                break;

            case DeviceItemViewModel parentDevice:
                parentDevice.AddChild(device);
                parentDevice.IsExpanded = true;
                break;

            case PlcNodeViewModel parentPlc:
                parentPlc.AddChild(device);
                parentPlc.IsExpanded = true;
                break;

            default:
                // 선택 없음 또는 Tag 선택 → 루트에 추가
                RootNodes.Add(device);
                device.Parent = null;
                break;
        }

        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));

        LogManager.Instance.Info(LogSrc, $"[하위] 장비 추가: {device.Name}");
    }

    /// <summary>
    /// Tag 가 선택된 경우만 비활성 — 나머지는 항상 장비 추가 가능.
    /// </summary>
    private bool CanAddDevice() => SelectedNode is not TagNodeViewModel;

    /// <summary>
    /// PLC 추가.
    ///
    /// ★ v2: 어디서나 PLC 추가 가능
    ///   · 선택 없음 / Tag 선택      → 루트에 추가
    ///   · Group 선택                → 그룹 하위에 추가
    ///   · Device / Plc 선택         → 해당 장비 하위에 추가 (중첩)
    /// 선택 노드 하위에 PLC 추가.
    /// 선택 없음 / Tag 선택 → 루트에 추가
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddPlc))]
    private void AddPlc()
    {
        // 슬롯 번호: 부모 또는 루트 내 기존 PlcNode 개수
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
            case GroupNodeViewModel group:
                group.AddChild(plc);
                group.IsExpanded = true;
                break;

            case DeviceItemViewModel parentDevice:
                parentDevice.AddChild(plc);
                parentDevice.IsExpanded = true;
                break;

            case PlcNodeViewModel parentPlc:
                parentPlc.AddChild(plc);
                parentPlc.IsExpanded = true;
                break;

            default:
                // 선택 없음 또는 Tag 선택 → 루트에 추가
                RootNodes.Add(plc);
                plc.Parent = null;
                break;
        }

        SelectedNode = plc;
        plc.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] PLC 추가: {plc.Name}");
    }

    /// <summary>
    /// Tag 가 선택된 경우만 비활성 — 나머지는 항상 PLC 추가 가능.
    /// </summary>
    private bool CanAddPlc() => SelectedNode is not TagNodeViewModel;

    /// <summary>
    /// Tag 추가 — Device 또는 Plc 선택 시만 활성 (기존 동작 유지).
    /// Tag 는 Device / Plc 하위에만 배치 가능.
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
        LogManager.Instance.Info(LogSrc, $"[하위] 태그 추가: {tag.Name}");
    }

    private bool CanAddTag() =>
        SelectedNode is DeviceItemViewModel or PlcNodeViewModel;

    // §5 ─ 같은 레벨(Sibling) 추가 커맨드 ───────────────────

    /// <summary>
    /// 선택 노드와 같은 레벨에 그룹 추가.
    /// 활성 조건: 선택 노드의 부모가 없거나(루트) Group인 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingGroup))]
    private void AddSiblingGroup()
    {
        if (SelectedNode is null) return;
        var group = new GroupNodeViewModel();
        _InsertSibling(group);
        SelectedNode = group;
        group.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 그룹 추가: {group.Name}");
    }

    private bool CanAddSiblingGroup()
    {
        if (SelectedNode is null) return false;
        // Group은 루트 또는 Group 하위에만 위치 가능
        return SelectedNode.Parent is null or GroupNodeViewModel;
    }

    /// <summary>
    /// 선택 노드와 같은 레벨에 장비 추가.
    /// 활성 조건: 선택 노드가 있고 Tag가 아닌 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingDevice))]
    private void AddSiblingDevice()
    {
        if (SelectedNode is null) return;
        var device = new DeviceItemViewModel();
        _InsertSibling(device);
        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 장비 추가: {device.Name}");
    }

    private bool CanAddSiblingDevice()
    {
        if (SelectedNode is null) return false;
        // Tag 레벨에서는 같은 레벨 장비 추가 불가
        // (Tag 부모는 Device/Plc이지만, 장비와 Tag를 같은 레벨에 두는 것은 혼란)
        return SelectedNode is not TagNodeViewModel;
    }

    /// <summary>
    /// 선택 노드와 같은 레벨에 PLC 추가.
    /// 활성 조건: 선택 노드가 있고 Tag가 아닌 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingPlc))]
    private void AddSiblingPlc()
    {
        if (SelectedNode is null) return;
        int slotNo = _GetSiblingPlcCount();
        var plc = new PlcNodeViewModel(slotNo: slotNo);
        _InsertSibling(plc);
        SelectedNode = plc;
        plc.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] PLC 추가: {plc.Name}");
    }

    private bool CanAddSiblingPlc()
    {
        if (SelectedNode is null) return false;
        return SelectedNode is not TagNodeViewModel;
    }

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

    // §8 ─ 샘플 데이터 로드 ───────────────────────────────────

    /// <summary>
    /// 샘플 데이터 — 다양한 트리 구조 테스트용.
    /// Phase 6 에서 JsonConfigLoader 로 대체 예정.
    /// </summary>
    [RelayCommand]
    private void LoadSample()
    {
        RootNodes.Clear();

        // ── 케이스 1: 루트 직속 PLC (그룹 없음) ─────────────
        var rootPlc = new PlcNodeViewModel("PLC-001 (루트직속)", slotNo: 0) { UnitId = 1 };
        rootPlc.AddChild(new TagNodeViewModel("온도_CH1") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        rootPlc.AddChild(new TagNodeViewModel("압력_CH1") { Address = "40003", BufType = "FloatBE", PollMs = 500 });

        // PLC 하위에 PLC 중첩
        var subPlc = new PlcNodeViewModel("PLC-001-확장슬롯", slotNo: 1) { UnitId = 2 };
        subPlc.AddChild(new TagNodeViewModel("전류") { Address = "40005", BufType = "FloatBE", PollMs = 1000 });
        rootPlc.AddChild(subPlc);

        RootNodes.Add(rootPlc);

        // ── 케이스 2: 루트 직속 Device (그룹 없음) ──────────
        var rootDevice = new DeviceItemViewModel("냉각기 (루트직속)") { Manufacturer = "Danfoss" };
        rootDevice.AddChild(new TagNodeViewModel("RPM") { Address = "40001", BufType = "UInt16BE", PollMs = 200 });

        // Device 하위에 Device 중첩
        var subDevice = new DeviceItemViewModel("냉각기-보조펌프") { Manufacturer = "Grundfos" };
        subDevice.AddChild(new TagNodeViewModel("유량") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        rootDevice.AddChild(subDevice);

        RootNodes.Add(rootDevice);

        // ── 케이스 3: 그룹 → 장비 → PLC → Tag (기존 구조) ──
        var g1 = new GroupNodeViewModel("1공장");
        var d1 = new DeviceItemViewModel("압출기 #1") { Manufacturer = "Atlas Copco", Model = "GA90" };
        var p1 = new PlcNodeViewModel("Modbus Slot", slotNo: 0) { UnitId = 1 };

        p1.AddChild(new TagNodeViewModel("온도") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        p1.AddChild(new TagNodeViewModel("압력") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        d1.AddChild(p1);
        g1.AddChild(d1);
        RootNodes.Add(g1);

        SelectedNode = rootPlc;
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, "샘플 데이터 로드 완료 (유연 트리 구조)");
    }

    // §10 ─ 내부 헬퍼 ─────────────────────────────────────────

    /// <summary>
    /// 선택 노드의 바로 다음 위치(같은 레벨)에 새 노드를 삽입합니다.
    /// 부모 참조를 선택 노드의 부모와 동일하게 설정합니다.
    /// </summary>
    private void _InsertSibling(DeviceNodeViewModel newNode)
    {
        if (SelectedNode is null) return;

        var list = GetSiblingList(SelectedNode);
        if (list is null) return;

        int idx = list.IndexOf(SelectedNode);
        newNode.Parent = SelectedNode.Parent; // 루트이면 null

        // 선택 노드 바로 다음에 삽입 (끝이면 마지막에 추가)
        if (idx >= 0 && idx < list.Count - 1)
            list.Insert(idx + 1, newNode);
        else
            list.Add(newNode);
    }

    /// <summary>선택 노드와 같은 레벨의 PLC 개수 (슬롯 번호 계산용)</summary>
    private int _GetSiblingPlcCount()
    {
        if (SelectedNode is null) return 0;
        var list = GetSiblingList(SelectedNode);
        return list?.OfType<PlcNodeViewModel>().Count() ?? 0;
    }

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