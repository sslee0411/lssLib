// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · Core/Config/DeviceTreeSerializer.cs
//  역할: DeviceNodeViewModel 계층 → ConfigTree 재귀 직렬화
//        Phase 5: 저장 연동 핵심 컴포넌트
//  Phase 5: 신규 추가
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.ViewModels.DeviceTree;
using lssLib.Config.Tree;
using lssLib.Log;
using System.Collections.ObjectModel;

namespace IIoT.DeviceManager.Core.Config;

/// <summary>
/// DeviceNodeViewModel 계층을 lssLib.Config.ConfigTree 로 변환합니다.
///
/// 변환 규칙:
///   GroupNodeViewModel   → ConfigNode (Type=Group)
///   DeviceItemViewModel  → ConfigNode (Type=Device) + Properties 매핑
///   PlcNodeViewModel     → ConfigNode (Type=Plc)    + 통신 Properties
///   TagNodeViewModel     → ConfigNode (Type=Tag)    + 수집 Properties
///   SensorNodeViewModel  → ConfigNode (Type=Sensor) + 물리 Properties
///
/// 재귀 순회로 N단계 중첩을 모두 처리합니다.
/// </summary>
public static class DeviceTreeSerializer
{
    // §1 ─ 상수 ───────────────────────────────────────────────
    private const string LogSrc = "DeviceTreeSerializer";

    // §2 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// RootNodes 컬렉션을 ConfigTree 로 직렬화합니다.
    /// </summary>
    /// <param name="rootNodes">DeviceTreeViewModel.RootNodes</param>
    /// <returns>저장 가능한 ConfigTree</returns>
    public static ConfigTree Serialize(
        ObservableCollection<DeviceNodeViewModel> rootNodes)
    {
        var tree = new ConfigTree();

        // ConfigTree 루트 노드 설정
        var root = tree.Root;
        root.Id   = "root";
        root.Name = "Root";

        int count = 0;
        foreach (var vm in rootNodes)
        {
            var child = _ConvertNode(vm);
            root.AddChild(child);
            count += _CountNodes(vm);
        }

        LogManager.Instance.Info(LogSrc,
            $"직렬화 완료 — {rootNodes.Count}개 루트, 총 {count}개 노드");

        return tree;
    }

    // §3 ─ 노드 변환 (재귀) ───────────────────────────────────

    /// <summary>
    /// DeviceNodeViewModel 단일 노드를 ConfigNode 로 변환합니다.
    /// 자식 노드는 재귀적으로 처리됩니다.
    /// </summary>
    private static ConfigNode _ConvertNode(DeviceNodeViewModel vm)
    {
        var node = vm switch
        {
            GroupNodeViewModel  g => _ConvertGroup(g),
            DeviceItemViewModel d => _ConvertDevice(d),
            PlcNodeViewModel    p => _ConvertPlc(p),
            TagNodeViewModel    t => _ConvertTag(t),
            SensorNodeViewModel s => _ConvertSensor(s),
            _                     => _ConvertGeneric(vm),
        };

        // 자식 재귀 처리
        foreach (var child in vm.Children)
            node.AddChild(_ConvertNode(child));

        return node;
    }

    // §4 ─ 타입별 변환 ────────────────────────────────────────

    private static ConfigNode _ConvertGroup(GroupNodeViewModel vm)
    {
        var node = new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Group,
        };
        _SetIfNotEmpty(node, "description", string.Empty);
        return node;
    }

    private static ConfigNode _ConvertDevice(DeviceItemViewModel vm)
    {
        var node = new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Device,
        };
        _SetIfNotEmpty(node, "commConfigId",  vm.CommConfigId);
        _SetIfNotEmpty(node, "locationId",    vm.LocationId);
        _SetIfNotEmpty(node, "manufacturer",  vm.Manufacturer);
        _SetIfNotEmpty(node, "model",         vm.Model);
        _SetIfNotEmpty(node, "serialNo",      vm.SerialNo);
        node.SetProperty("isOnline", vm.IsOnline ? "true" : "false");
        return node;
    }

    private static ConfigNode _ConvertPlc(PlcNodeViewModel vm)
    {
        var node = new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Device, // lssLib ConfigTree: Plc는 Device 타입으로 저장
        };
        node.SetProperty("nodeKind",     "Plc");
        node.SetProperty("slotNo",       vm.SlotNo.ToString());
        node.SetProperty("unitId",       vm.UnitId.ToString());
        node.SetProperty("protocolType", vm.ProtocolType);
        return node;
    }

    private static ConfigNode _ConvertTag(TagNodeViewModel vm)
    {
        var node = new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Tag,
        };
        _SetIfNotEmpty(node, "address",       vm.Address);
        _SetIfNotEmpty(node, "bufType",       vm.BufType);
        node.SetProperty("pollMs",   vm.PollMs.ToString());
        node.SetProperty("deadBand", vm.DeadBand.ToString("G"));
        _SetIfNotEmpty(node, "ownerDeviceId", vm.OwnerDeviceId);
        return node;
    }

    private static ConfigNode _ConvertSensor(SensorNodeViewModel vm)
    {
        var node = new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Sensor,
        };
        _SetIfNotEmpty(node, "unit",        vm.Unit);
        _SetIfNotEmpty(node, "sensorType",  vm.SensorType);
        _SetIfNotEmpty(node, "description", vm.Description);
        _SetIfNotEmpty(node, "formula",     vm.Formula);
        _SetIfNotEmpty(node, "scaleConfigId", vm.ScaleConfigId);
        _SetIfNotEmpty(node, "alarmGroupId",  vm.AlarmGroupId);

        // 알람 임계값
        if (vm.AlarmHighHigh.HasValue)
            node.SetProperty("alarmHH", vm.AlarmHighHigh.Value.ToString("G"));
        if (vm.AlarmHigh.HasValue)
            node.SetProperty("alarmH",  vm.AlarmHigh.Value.ToString("G"));
        if (vm.AlarmLow.HasValue)
            node.SetProperty("alarmL",  vm.AlarmLow.Value.ToString("G"));
        if (vm.AlarmLowLow.HasValue)
            node.SetProperty("alarmLL", vm.AlarmLowLow.Value.ToString("G"));
        if (vm.AlarmDeadBand != 0)
            node.SetProperty("alarmDeadBand", vm.AlarmDeadBand.ToString("G"));

        // TagRef 목록 직렬화 (JSON 배열 → comma-separated 문자열)
        if (vm.TagRefs.Count > 0)
        {
            var refs = vm.TagRefs
                .Select(r => $"{r.Role}:{r.TagId}")
                .ToList();
            node.SetProperty("tagRefs", string.Join(";", refs));
        }

        return node;
    }

    private static ConfigNode _ConvertGeneric(DeviceNodeViewModel vm)
    {
        LogManager.Instance.Warn(LogSrc,
            $"알 수 없는 노드 타입: {vm.GetType().Name} ({vm.Name}) — Generic으로 저장");
        return new ConfigNode
        {
            Id   = vm.Id,
            Name = vm.Name,
            Type = NodeType.Group,
        };
    }

    // §5 ─ 역직렬화 (ConfigTree → ViewModel) ─────────────────

    /// <summary>
    /// ConfigTree 를 DeviceNodeViewModel 목록으로 역직렬화합니다.
    /// JsonConfigLoader.LoadDeviceTree() 결과를 트리 뷰에 로드할 때 사용합니다.
    /// </summary>
    public static List<DeviceNodeViewModel> Deserialize(ConfigTree tree)
    {
        var result = new List<DeviceNodeViewModel>();

        if (tree?.Root == null)
            return result;

        foreach (var child in tree.Root.Children)
        {
            var vm = _ConvertConfigNode(child);
            if (vm != null)
                result.Add(vm);
        }

        LogManager.Instance.Info(LogSrc,
            $"역직렬화 완료 — {result.Count}개 루트 노드");

        return result;
    }

    private static DeviceNodeViewModel? _ConvertConfigNode(ConfigNode node)
    {
        // nodeKind 속성으로 Plc 구분 (ConfigTree에서 Plc는 Device 타입으로 저장됨)
        bool isPlc = node.GetProperty("nodeKind") == "Plc";

        DeviceNodeViewModel vm = node.Type switch
        {
            NodeType.Group  => _DeserializeGroup(node),
            NodeType.Device => isPlc ? _DeserializePlc(node) : _DeserializeDevice(node),
            NodeType.Tag    => _DeserializeTag(node),
            NodeType.Sensor => _DeserializeSensor(node),
            _               => new GroupNodeViewModel(node.Name),
        };

        // 자식 재귀 처리
        foreach (var child in node.Children)
        {
            var childVm = _ConvertConfigNode(child);
            if (childVm != null)
                vm.AddChild(childVm);
        }

        return vm;
    }

    private static GroupNodeViewModel _DeserializeGroup(ConfigNode node)
        => new(node.Name) { };

    private static DeviceItemViewModel _DeserializeDevice(ConfigNode node)
    {
        var vm = new DeviceItemViewModel(node.Name)
        {
            CommConfigId = node.GetProperty("commConfigId"),
            LocationId   = node.GetProperty("locationId"),
            Manufacturer = node.GetProperty("manufacturer") ?? string.Empty,
            Model        = node.GetProperty("model")        ?? string.Empty,
            SerialNo     = node.GetProperty("serialNo")     ?? string.Empty,
            IsOnline     = node.GetProperty("isOnline") == "true",
        };
        return vm;
    }

    private static PlcNodeViewModel _DeserializePlc(ConfigNode node)
    {
        int.TryParse(node.GetProperty("slotNo"), out int slotNo);
        byte.TryParse(node.GetProperty("unitId"), out byte unitId);
        var vm = new PlcNodeViewModel(node.Name, slotNo)
        {
            UnitId       = unitId,
            ProtocolType = node.GetProperty("protocolType") ?? "Modbus",
        };
        return vm;
    }

    private static TagNodeViewModel _DeserializeTag(ConfigNode node)
    {
        int.TryParse(node.GetProperty("pollMs"), out int pollMs);
        double.TryParse(node.GetProperty("deadBand"), out double deadBand);
        return new TagNodeViewModel(node.Name)
        {
            Address       = node.GetProperty("address")       ?? string.Empty,
            BufType       = node.GetProperty("bufType")       ?? "FloatBE",
            PollMs        = pollMs,
            DeadBand      = deadBand,
            OwnerDeviceId = node.GetProperty("ownerDeviceId"),
        };
    }

    private static SensorNodeViewModel _DeserializeSensor(ConfigNode node)
    {
        static double? ParseOpt(string? s)
            => !string.IsNullOrEmpty(s) && double.TryParse(s, out var v) ? v : null;

        double.TryParse(node.GetProperty("alarmDeadBand"), out double deadBand);

        var vm = new SensorNodeViewModel(node.Name)
        {
            Unit          = node.GetProperty("unit")         ?? string.Empty,
            SensorType    = node.GetProperty("sensorType")   ?? "Generic",
            Description   = node.GetProperty("description")  ?? string.Empty,
            Formula       = node.GetProperty("formula"),
            ScaleConfigId = node.GetProperty("scaleConfigId"),
            AlarmGroupId  = node.GetProperty("alarmGroupId"),
            AlarmHighHigh = ParseOpt(node.GetProperty("alarmHH")),
            AlarmHigh     = ParseOpt(node.GetProperty("alarmH")),
            AlarmLow      = ParseOpt(node.GetProperty("alarmL")),
            AlarmLowLow   = ParseOpt(node.GetProperty("alarmLL")),
            AlarmDeadBand = deadBand,
        };

        // TagRef 역직렬화
        var tagRefsStr = node.GetProperty("tagRefs");
        if (!string.IsNullOrEmpty(tagRefsStr))
        {
            foreach (var part in tagRefsStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIdx = part.IndexOf(':');
                if (colonIdx > 0)
                {
                    var role  = part[..colonIdx];
                    var tagId = part[(colonIdx + 1)..];
                    vm.AddTagRef(tagId, string.Empty, role);
                }
            }
        }

        return vm;
    }

    // §6 ─ 내부 헬퍼 ──────────────────────────────────────────

    private static void _SetIfNotEmpty(ConfigNode node, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            node.SetProperty(key, value);
    }

    private static int _CountNodes(DeviceNodeViewModel vm)
        => 1 + vm.Children.Sum(c => _CountNodes(c));
}
