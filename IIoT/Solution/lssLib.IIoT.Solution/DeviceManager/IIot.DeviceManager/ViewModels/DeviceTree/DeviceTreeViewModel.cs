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
//  수정: 2025-05-23 v4 — 버그 수정 (SelectedNode 동기화, MoveUp/Down)
//  수정: 2025-05-23 v5 — Tag/Sensor 이중 레이어 구조 반영
//  수정: 2025-05-23 v6 — AddTag Device 지원 (PLC 하위 필드 장비)
//        AddSensorCommand 추가 (Device 하위 Sensor 추가)
//        AddTagCommand: Plc 하위에만 추가 (Device 직접 불가)
//        CanAddSensor/CanAddTag CanExecute 조건 업데이트
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
/// ── 하위(Child) 추가 ────────────────────────────────────────
///   AddGroupCommand  : 그룹 추가 (그룹 선택 → 하위, 그 외 → 루트)
///   AddDeviceCommand : 장비 추가
///   AddPlcCommand    : PLC 추가 (수집 레이어)
///   AddTagCommand    : Tag 추가 (Plc 하위에만)
///   AddSensorCommand : Sensor 추가 (Device 하위에만) ★ 물리 레이어
///
/// ── 같은 레벨(Sibling) 추가 ─────────────────────────────────
///   AddSiblingGroup/Device/PlcCommand
///
/// ── 이중 레이어 배치 규칙 ───────────────────────────────────
///   Tag    : Plc 선택 시만 활성  (수집 레이어 — Plc 하위)
///   Sensor : Device 선택 시만 활성  (물리 레이어 — Device 하위 직접)
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

    // §3-1 ─ SelectedNode 변경 시 IsSelected 동기화 ───────────

    /// <summary>
    /// ★ 버그 수정: VM에서 SelectedNode를 직접 바꿀 때
    ///   (AddTag/AddDevice 등) TreeView 시각 선택과 동기화.
    ///
    /// SelectedNode = tag 를 VM에서 설정하면 tag.IsSelected = true 가 되고
    /// TwoWay 바인딩으로 TreeViewItem 의 시각 선택도 변경됨.
    /// 이후 사용자가 장비를 다시 클릭하면 TreeView 가 "변경" 으로 감지하여
    /// SelectedItemChanged 이벤트가 정상 발생 → SelectedNode 가 장비로 업데이트됨.
    /// </summary>
    partial void OnSelectedNodeChanged(DeviceNodeViewModel? oldValue, DeviceNodeViewModel? newValue)
    {
        // ★ IsSelected를 VM에서 직접 설정하지 않음 (버그 원인 제거)
        // IsSelected 바인딩이 OneWayToSource이므로 TreeView → VM 방향만 허용.
        // VM에서 역방향으로 IsSelected를 설정하면 부모 노드도 함께 선택되는 버그 발생.
        //
        // 대신: TreeView.SelectedItemChanged 이벤트가 SelectedNode를 단독으로 관리.
        // ViewModel에서 AddSensor 등으로 SelectedNode를 직접 변경할 때는
        // TreeView가 다음 렌더링에서 자연스럽게 동기화됨.
        _ = oldValue; // 미사용 경고 억제
        _ = newValue;
    }

    // §4 ─ 하위(Child) 추가 커맨드 ───────────────────────────

    /// <summary>그룹 하위에 그룹 / 그 외 루트 그룹 추가</summary>
    [RelayCommand(CanExecute = nameof(CanAddGroup))]
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

    /// <summary>Tag 선택 시 그룹 추가 비활성</summary>
    private bool CanAddGroup() => SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

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

    /// <summary>
    /// Tag 가 선택된 경우만 비활성 — 나머지는 항상 PLC 추가 가능.
    /// </summary>
    private bool CanAddPlc() => SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    /// <summary>
    /// Tag 추가 — Plc / Device / Sensor 선택 시 활성 (수집 레이어).
    ///
    /// Tag를 추가할 수 있는 부모 노드:
    ///   · Plc    : 일반 PLC 레지스터 주소 태그
    ///   · Device : 필드 장비 직접 태그 (HART, Profibus, IO-Link 등)
    ///             예) PLC → 온도변환기(Device) → 측정값(Tag)
    ///   · Sensor : Sensor 전용 수집 태그 (TagRef 연결 후보)
    ///             Sensor 하위에 Tag를 두면 해당 Sensor의 TagRef로 자동 연결 예정
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        if (SelectedNode is not (PlcNodeViewModel or DeviceItemViewModel or SensorNodeViewModel)) return;
        var tag = new TagNodeViewModel();
        SelectedNode.AddChild(tag);
        SelectedNode.IsExpanded = true;
        SelectedNode = tag;
        tag.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] 태그 추가: {tag.Name}");
    }

    /// <summary>Tag는 Plc 또는 Device 하위에 추가 가능</summary>
    /// <summary>
    /// Tag 추가 가능 부모:
    ///   Plc    — 일반 PLC 레지스터 태그
    ///   Device — 필드 장비 직접 태그 (HART/Profibus)
    ///   Sensor — Sensor 전용 수집 태그 (TagRef 연결 대상)
    /// </summary>
    private bool CanAddTag() =>
        SelectedNode is PlcNodeViewModel or DeviceItemViewModel or SensorNodeViewModel;

    /// <summary>
    /// Sensor 추가 — Device 선택 시만 활성 (물리 레이어).
    /// Sensor는 실 물리 센서 표현이므로 Device 하위에 직접 위치.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSensor))]
    private void AddSensor()
    {
        if (SelectedNode is not DeviceItemViewModel) return;
        var sensor = new SensorNodeViewModel();
        SelectedNode.AddChild(sensor);
        SelectedNode.IsExpanded = true;
        SelectedNode = sensor;
        sensor.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[하위] 센서 추가: {sensor.Name}");
    }

    /// <summary>Sensor는 Device 하위에만 추가 가능</summary>
    private bool CanAddSensor() => SelectedNode is DeviceItemViewModel;

    // §5 ─ 같은 레벨(Sibling) 추가 커맨드 ───────────────────

    /// <summary>
    /// 선택 노드와 같은 레벨에 그룹 추가.
    /// 활성 조건: 선택 노드의 부모가 없거나(루트) Group인 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingGroup))]
    private void AddSiblingGroup()
    {
        var group = new GroupNodeViewModel();

        if (SelectedNode is null)
        {
            // 선택 없음 → 루트 마지막에 추가
            RootNodes.Add(group);
            group.Parent = null;
        }
        else
        {
            _InsertSibling(group);
        }

        SelectedNode = group;
        group.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 그룹 추가: {group.Name}");
    }

    private bool CanAddSiblingGroup()
    {
        // 선택 없음 → 루트에 그룹 추가 가능
        if (SelectedNode is null) return true;
        // Tag/Sensor 선택 시 불가
        if (SelectedNode is TagNodeViewModel or SensorNodeViewModel) return false;
        // 루트 노드 또는 그룹 하위 노드일 때만 그룹 추가 가능
        return SelectedNode.Parent is null or GroupNodeViewModel;
    }

    /// <summary>
    /// 선택 노드와 같은 레벨에 장비 추가.
    /// 활성 조건: 선택 노드가 있고 Tag가 아닌 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingDevice))]
    private void AddSiblingDevice()
    {
        var device = new DeviceItemViewModel();

        if (SelectedNode is null)
        {
            // 선택 없음 → 루트 마지막에 추가
            RootNodes.Add(device);
            device.Parent = null;
        }
        else
        {
            _InsertSibling(device);
        }

        SelectedNode = device;
        device.BeginEditCommand.Execute(null);
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, $"[같은레벨] 장비 추가: {device.Name}");
    }

    private bool CanAddSiblingDevice() =>
        // SelectedNode == null 이면 RootNodes 마지막에 추가 (루트 동레벨)
        SelectedNode is null ||
        SelectedNode is not (TagNodeViewModel or SensorNodeViewModel);

    /// <summary>
    /// 선택 노드와 같은 레벨에 PLC 추가.
    /// 활성 조건: 선택 노드가 있고 Tag가 아닌 경우
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSiblingPlc))]
    private void AddSiblingPlc()
    {
        int slotNo = SelectedNode is null
            ? RootNodes.OfType<PlcNodeViewModel>().Count()
            : _GetSiblingPlcCount();

        var plc = new PlcNodeViewModel(slotNo: slotNo);

        if (SelectedNode is null)
        {
            // 선택 없음 → 루트 마지막에 추가
            RootNodes.Add(plc);
            plc.Parent = null;
        }
        else
        {
            _InsertSibling(plc);
        }

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
        // 위치가 바뀌었으므로 CanExecute 수동 갱신
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
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
        // 위치가 바뀌었으므로 CanExecute 수동 갱신
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

    // §9 ─ 샘플 데이터 ────────────────────────────────────────

    /// <summary>
    /// 샘플 데이터 — 다양한 트리 구조 테스트용.
    /// Phase 6 에서 JsonConfigLoader 로 대체 예정.
    /// </summary>

    [RelayCommand]
    private void LoadSample()
    {
        RootNodes.Clear();

        // ── 케이스 1: 이중 레이어 구조 (핵심 패턴) ─────────
        var g1 = new GroupNodeViewModel("Line-1 (생산라인)");
        var dev1 = new DeviceItemViewModel("압연기-001") { Manufacturer = "POSCO", Model = "RM-100" };

        // [수집 레이어] PLC + Tag
        var plc1 = new PlcNodeViewModel("PLC-SIEMENS", slotNo: 0) { UnitId = 1, ProtocolType = "ModbusTCP" };
        plc1.AddChild(new TagNodeViewModel("temp_raw") { Address = "40001", BufType = "Int16BE", PollMs = 1000 });
        plc1.AddChild(new TagNodeViewModel("press_hi") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        plc1.AddChild(new TagNodeViewModel("press_lo") { Address = "40005", BufType = "FloatBE", PollMs = 500 });
        plc1.AddChild(new TagNodeViewModel("motor_run") { Address = "M0.0", BufType = "Bool", PollMs = 200 });
        dev1.AddChild(plc1);

        // [물리 레이어] Sensor (Device 하위 직접)
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

        var sen3 = new SensorNodeViewModel("모터운전상태")
        {
            SensorType = "Bool",
            Unit = ""
        };
        sen3.AddTagRef("motor_run_id", "motor_run", "primary");
        dev1.AddChild(sen3);

        g1.AddChild(dev1);
        RootNodes.Add(g1);

        // ── 케이스 2: 루트 직속 PLC (그룹 없음) ─────────────
        var rootPlc = new PlcNodeViewModel("PLC-001 (루트직속)", slotNo: 0) { UnitId = 1 };
        rootPlc.AddChild(new TagNodeViewModel("온도_CH1") { Address = "40001", BufType = "FloatBE", PollMs = 500 });
        rootPlc.AddChild(new TagNodeViewModel("압력_CH1") { Address = "40003", BufType = "FloatBE", PollMs = 500 });
        RootNodes.Add(rootPlc);

        SelectedNode = g1;
        OnPropertyChanged(nameof(TotalNodeCount));
        LogManager.Instance.Info(LogSrc, "샘플 데이터 로드 완료 (이중 레이어 구조)");
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
        newNode.Parent = SelectedNode.Parent;
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
        if (node.Parent is not null) return node.Parent.Children;
        if (RootNodes.Contains(node)) return RootNodes;
        return null;
    }
}