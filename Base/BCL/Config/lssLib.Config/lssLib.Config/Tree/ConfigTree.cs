// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Tree/ConfigTree.cs
//  역할: 설정 트리 관리자 — CRUD, 탐색, JSON/XML 직렬화, 파일 연동
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace lssLib.Config.Tree;

/// <summary>
/// 설정 트리 관리자.
/// </summary>
/// <remarks>
/// 계층 구조(<see cref="NodeType.Root"/> → <see cref="NodeType.Group"/>
/// → <see cref="NodeType.Device"/> → <see cref="NodeType.Sensor"/> / <see cref="NodeType.Tag"/>)를
/// 관리하며 JSON·XML 파일 연동을 제공합니다.
/// <example><code>
/// var tree = new ConfigTree();
///
/// // 트리 구성
/// var line1 = tree.AddGroup("Line-1", "Building-A");
/// var plc   = tree.AddDevice(line1, "PLC-001", ip: "192.168.1.10", port: "502");
/// var temp  = tree.AddSensor(plc,   "TempSensor-01", address: "0x0001", scale: "0.1");
/// var tag   = tree.AddTag(plc,      "M0.0", address: "M0.0");
///
/// // 저장/로드
/// tree.SaveJson("devices.json");
/// tree.LoadJson("devices.json");
///
/// // 탐색
/// ConfigNode? node = tree.FindById("dev-xxx");
/// IEnumerable&lt;ConfigNode&gt; all = tree.FindAll(NodeType.Device);
/// </code></example>
/// </remarks>
public sealed class ConfigTree
{
    #region §1 ─ 루트

    /// <summary>트리 루트 노드. 직접 수정하지 않고 API 를 통해 조작하세요.</summary>
    public ConfigNode Root { get; private set; } = new("Root", NodeType.Root);

    /// <summary>트리 이름 (파일 저장 시 메타데이터로 기록).</summary>
    public string TreeName { get; set; } = "DeviceTree";

    #endregion

    #region §2 ─ 이벤트

    /// <summary>트리 구조가 변경될 때 발생합니다 (추가/제거/이동).</summary>
    public event Action<ConfigNode, string>? NodeChanged;
    // args: (변경된 노드, 변경 유형: "Added" | "Removed" | "Moved" | "Modified")

    // 외부에서 이벤트를 발생시킬 수 있도록 메서드 제공
    public void NotifyNodeChanged(object srv, string state)
    {
        // 클래스 내부이므로 Invoke 호출이 가능합니다.
        NodeChanged?.Invoke((ConfigNode)srv, state);
    }

    #endregion

    #region §3 ─ 노드 추가 (팩토리)

    /// <summary>
    /// 그룹 노드를 루트에 추가합니다.
    /// </summary>
    /// <param name="name">그룹 이름.</param>
    /// <param name="location">위치 설명 (선택).</param>
    public ConfigNode AddGroup(string name, string? location = null)
    {
        var node = new ConfigNode(name, NodeType.Group);
        if (location is not null) node.SetProperty("location", location);
        Root.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    /// <summary>
    /// 그룹 노드를 지정 부모에 추가합니다.
    /// </summary>
    public ConfigNode AddGroup(ConfigNode parent, string name, string? location = null)
    {
        var node = new ConfigNode(name, NodeType.Group);
        if (location is not null) node.SetProperty("location", location);
        parent.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    /// <summary>
    /// 장비 노드를 지정 부모에 추가합니다.
    /// </summary>
    /// <param name="parent">부모 노드 (Group 또는 Root).</param>
    /// <param name="name">장비 이름.</param>
    /// <param name="ip">IP 주소 (선택).</param>
    /// <param name="port">포트 (선택).</param>
    /// <param name="protocol">프로토콜 (선택, 예: "Modbus", "EtherNet/IP").</param>
    public ConfigNode AddDevice(ConfigNode parent, string name,
        string? ip = null, string? port = null, string? protocol = null)
    {
        var node = new ConfigNode(name, NodeType.Device);
        if (ip is not null) node.SetProperty("ip", ip);
        if (port is not null) node.SetProperty("port", port);
        if (protocol is not null) node.SetProperty("protocol", protocol);
        parent.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    /// <summary>
    /// 센서 노드를 지정 부모에 추가합니다.
    /// </summary>
    /// <param name="parent">부모 노드 (Device).</param>
    /// <param name="name">센서 이름.</param>
    /// <param name="address">메모리/레지스터 주소 (선택).</param>
    /// <param name="scale">스케일 인수 (선택).</param>
    /// <param name="unit">공학 단위 (선택, 예: "°C", "bar").</param>
    public ConfigNode AddSensor(ConfigNode parent, string name,
        string? address = null, string? scale = null, string? unit = null)
    {
        var node = new ConfigNode(name, NodeType.Sensor);
        if (address is not null) node.SetProperty("address", address);
        if (scale is not null) node.SetProperty("scale", scale);
        if (unit is not null) node.SetProperty("unit", unit);
        parent.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    /// <summary>
    /// 태그 노드를 지정 부모에 추가합니다.
    /// </summary>
    /// <param name="parent">부모 노드 (Device).</param>
    /// <param name="name">태그 이름.</param>
    /// <param name="address">PLC 주소 또는 OPC-UA NodeId (선택).</param>
    /// <param name="dataType">데이터 타입 문자열 (선택, 예: "Bool", "Int16").</param>
    public ConfigNode AddTag(ConfigNode parent, string name,
        string? address = null, string? dataType = null)
    {
        var node = new ConfigNode(name, NodeType.Tag);
        if (address is not null) node.SetProperty("address", address);
        if (dataType is not null) node.SetProperty("dataType", dataType);
        parent.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    /// <summary>
    /// 범용 노드를 지정 부모에 추가합니다.
    /// </summary>
    public ConfigNode AddNode(ConfigNode parent, string name, NodeType type,
        Dictionary<string, string>? properties = null)
    {
        var node = new ConfigNode(name, type);
        if (properties is not null)
            foreach (var kv in properties)
                node.SetProperty(kv.Key, kv.Value);
        parent.AddChild(node);
        NodeChanged?.Invoke(node, "Added");
        return node;
    }

    #endregion

    #region §4 ─ 노드 제거 / 이동

    /// <summary>
    /// 노드를 트리에서 제거합니다. 자식 노드도 함께 제거됩니다.
    /// </summary>
    /// <param name="node">제거할 노드.</param>
    /// <returns>제거 성공 여부.</returns>
    public bool Remove(ConfigNode node)
    {
        if (node.IsRoot) return false;
        if (node.Parent is null) return false;
        bool ok = node.Parent.RemoveChild(node);
        if (ok) NodeChanged?.Invoke(node, "Removed");
        return ok;
    }

    /// <summary>
    /// 노드를 다른 부모 아래로 이동합니다.
    /// </summary>
    /// <param name="node">이동할 노드.</param>
    /// <param name="newParent">이동할 대상 부모 노드.</param>
    /// <exception cref="InvalidOperationException">순환 참조가 발생하는 경우.</exception>
    public void Move(ConfigNode node, ConfigNode newParent)
    {
        if (node.IsAncestorOf(newParent))
            throw new InvalidOperationException("순환 참조: 자신의 자식을 부모로 설정할 수 없습니다.");
        node.Parent?.Children.Remove(node);
        newParent.AddChild(node);
        NodeChanged?.Invoke(node, "Moved");
    }

    #endregion

    #region §5 ─ 탐색

    /// <summary>Id 로 노드를 검색합니다 (DFS).</summary>
    public ConfigNode? FindById(string id) => Root.FindById(id);

    /// <summary>이름으로 노드를 검색합니다 (DFS, 대소문자 무시).</summary>
    public ConfigNode? FindByName(string name) => Root.FindByName(name);

    /// <summary>특정 유형의 모든 노드를 반환합니다.</summary>
    public IEnumerable<ConfigNode> FindAll(NodeType type) => Root.FindAll(type);

    /// <summary>모든 노드를 평탄화하여 반환합니다 (루트 포함).</summary>
    public IEnumerable<ConfigNode> Flatten() => Root.Flatten();

    /// <summary>트리 전체 노드 수 (루트 제외).</summary>
    public int Count => Root.Flatten().Count() - 1;

    #endregion

    #region §6 ─ JSON 직렬화

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 트리를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string ToJson()
    {
        var dto = new TreeDto { Name = TreeName, Root = Root };
        return JsonSerializer.Serialize(dto, _jsonOptions);
    }

    /// <summary>
    /// JSON 문자열에서 트리를 복원합니다. 기존 트리는 초기화됩니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">JSON 파싱 실패.</exception>
    public void FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<TreeDto>(json, _jsonOptions)
                  ?? throw new InvalidOperationException("트리 JSON 파싱 실패.");
        TreeName = dto.Name ?? TreeName;
        Root = dto.Root ?? new ConfigNode("Root", NodeType.Root);
        RestoreParentRefs(Root, null);
    }

    /// <summary>
    /// JSON 파일에서 트리를 로드합니다.
    /// </summary>
    public void LoadJson(string filePath) =>
        FromJson(File.ReadAllText(filePath, Encoding.UTF8));

    /// <summary>
    /// 트리를 JSON 파일로 저장합니다.
    /// </summary>
    public void SaveJson(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)
                                  ?? Directory.GetCurrentDirectory());
        File.WriteAllText(filePath, ToJson(), Encoding.UTF8);
    }

    #endregion

    #region §7 ─ XML 직렬화

    /// <summary>
    /// 트리를 XML 문자열로 직렬화합니다.
    /// </summary>
    public string ToXml()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("ConfigTree",
                new XAttribute("name", TreeName),
                NodeToXml(Root)));
        return doc.ToString();
    }

    /// <summary>
    /// XML 문자열에서 트리를 복원합니다.
    /// </summary>
    public void FromXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("XML 루트 요소가 없습니다.");
        TreeName = root.Attribute("name")?.Value ?? TreeName;

        var nodeElem = root.Element("Node")
                       ?? throw new InvalidOperationException("Node 요소를 찾을 수 없습니다.");
        Root = XmlToNode(nodeElem);
        RestoreParentRefs(Root, null);
    }

    /// <summary>
    /// XML 파일에서 트리를 로드합니다.
    /// </summary>
    public void LoadXml(string filePath) =>
        FromXml(File.ReadAllText(filePath, Encoding.UTF8));

    /// <summary>
    /// 트리를 XML 파일로 저장합니다.
    /// </summary>
    public void SaveXml(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)
                                  ?? Directory.GetCurrentDirectory());
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("ConfigTree",
                new XAttribute("name", TreeName),
                NodeToXml(Root)));
        doc.Save(filePath);
    }

    #endregion

    #region §8 ─ 초기화

    /// <summary>
    /// 트리를 초기화합니다 (루트만 남음).
    /// </summary>
    public void Clear()
    {
        Root = new ConfigNode("Root", NodeType.Root);
    }

    #endregion

    #region §9 ─ 내부 헬퍼

    private static XElement NodeToXml(ConfigNode node)
    {
        var elem = new XElement("Node",
            new XAttribute("id", node.Id),
            new XAttribute("name", node.Name),
            new XAttribute("type", node.Type.ToString()),
            new XAttribute("enabled", node.Enabled),
            new XAttribute("order", node.Order));

        if (node.Description is not null)
            elem.Add(new XAttribute("description", node.Description));

        if (node.Properties.Count > 0)
        {
            var props = new XElement("Properties");
            foreach (var kv in node.Properties)
                props.Add(new XElement("Property",
                    new XAttribute("key", kv.Key),
                    new XAttribute("value", kv.Value)));
            elem.Add(props);
        }

        if (node.Children.Count > 0)
        {
            var children = new XElement("Children");
            foreach (var child in node.Children.OrderBy(c => c.Order))
                children.Add(NodeToXml(child));
            elem.Add(children);
        }

        return elem;
    }

    private static ConfigNode XmlToNode(XElement elem)
    {
        var node = new ConfigNode
        {
            Id = elem.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N")[..12],
            Name = elem.Attribute("name")?.Value ?? string.Empty,
            Type = Enum.TryParse<NodeType>(elem.Attribute("type")?.Value, out var t) ? t : NodeType.Other,
            Enabled = elem.Attribute("enabled")?.Value != "false",
            Description = elem.Attribute("description")?.Value,
            Order = int.TryParse(elem.Attribute("order")?.Value, out var o) ? o : 0
        };

        foreach (var prop in elem.Element("Properties")?.Elements("Property") ?? Enumerable.Empty<XElement>())
        {
            var key = prop.Attribute("key")?.Value;
            var val = prop.Attribute("value")?.Value;
            if (key is not null && val is not null)
                node.Properties[key] = val;
        }

        foreach (var childElem in elem.Element("Children")?.Elements("Node") ?? Enumerable.Empty<XElement>())
        {
            var child = XmlToNode(childElem);
            child.Parent = node;
            node.Children.Add(child);
        }

        return node;
    }

    private static void RestoreParentRefs(ConfigNode node, ConfigNode? parent)
    {
        node.Parent = parent;
        foreach (var child in node.Children)
            RestoreParentRefs(child, node);
    }

    #endregion

    #region §10 ─ DTO (JSON 직렬화 전용)

    private sealed class TreeDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("root")]
        public ConfigNode? Root { get; set; }
    }

    #endregion
}