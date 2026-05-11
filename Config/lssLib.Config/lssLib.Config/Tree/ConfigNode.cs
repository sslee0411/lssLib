// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config · Tree/ConfigNode.cs
//  역할: 설정 트리의 단일 노드 (계층 구조, 프로퍼티, 직렬화 지원)
// ══════════════════════════════════════════════════════════════════════════
using System.Text.Json.Serialization;

namespace lssLib.Config.Tree;

/// <summary>
/// 설정 트리의 단일 노드.
/// </summary>
/// <remarks>
/// 계층 구조: <see cref="NodeType.Root"/> → <see cref="NodeType.Group"/>
///           → <see cref="NodeType.Device"/> → <see cref="NodeType.Sensor"/> | <see cref="NodeType.Tag"/>
/// <para>
/// 각 노드는 임의의 키-값 프로퍼티(<see cref="Properties"/>)를 가질 수 있습니다.
/// IP 주소, 포트, 스케일 인수 등 장비/센서 설정값을 자유롭게 저장합니다.
/// </para>
/// <example><code>
/// var plc = new ConfigNode("PLC-001", NodeType.Device)
/// {
///     Description = "1번 라인 메인 PLC"
/// };
/// plc.SetProperty("ip",   "192.168.1.10");
/// plc.SetProperty("port", "502");
///
/// var sensor = new ConfigNode("TempSensor-01", NodeType.Sensor);
/// sensor.SetProperty("address", "0x0001");
/// sensor.SetProperty("scale",   "0.1");
///
/// plc.AddChild(sensor);
/// </code></example>
/// </remarks>
public sealed class ConfigNode
{
    #region §1 ─ 기본 속성

    /// <summary>노드 고유 식별자 (GUID 문자열). 생성 시 자동 부여.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>노드 표시 이름.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>노드 유형.</summary>
    [JsonPropertyName("type")]
    public NodeType Type { get; set; } = NodeType.Other;

    /// <summary>노드 설명 (선택).</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>노드 활성화 여부.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>노드 정렬 순서 (같은 부모 내 표시 순서).</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;

    #endregion

    #region §2 ─ 프로퍼티

    /// <summary>
    /// 노드 프로퍼티 딕셔너리 (키-값 설정).
    /// </summary>
    /// <remarks>
    /// IP, 포트, Modbus 주소, 스케일 인수 등 장비/센서별 임의 설정을 저장합니다.
    /// </remarks>
    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>프로퍼티 값을 설정합니다.</summary>
    public void SetProperty(string key, string value) =>
        Properties[key] = value;

    /// <summary>프로퍼티 값을 반환합니다. 없으면 <see langword="null"/>.</summary>
    public string? GetProperty(string key) =>
        Properties.TryGetValue(key, out var v) ? v : null;

    /// <summary>프로퍼티 값을 반환합니다. 없으면 <paramref name="fallback"/>.</summary>
    public string GetPropertyOr(string key, string fallback) =>
        Properties.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>프로퍼티가 존재하는지 확인합니다.</summary>
    public bool HasProperty(string key) => Properties.ContainsKey(key);

    /// <summary>프로퍼티를 제거합니다.</summary>
    public bool RemoveProperty(string key) => Properties.Remove(key);

    #endregion

    #region §3 ─ 트리 구조

    /// <summary>부모 노드. 루트이면 <see langword="null"/>.</summary>
    [JsonIgnore]
    public ConfigNode? Parent { get; internal set; }

    /// <summary>자식 노드 목록.</summary>
    [JsonPropertyName("children")]
    public List<ConfigNode> Children { get; set; } = new();

    /// <summary>자식 노드를 추가합니다.</summary>
    /// <param name="child">추가할 자식 노드.</param>
    /// <exception cref="InvalidOperationException">순환 참조가 발생하는 경우.</exception>
    public void AddChild(ConfigNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (IsAncestorOf(child))
            throw new InvalidOperationException("순환 참조: 자신의 부모를 자식으로 추가할 수 없습니다.");

        child.Parent?.Children.Remove(child);
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>자식 노드를 제거합니다.</summary>
    public bool RemoveChild(ConfigNode child)
    {
        if (!Children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    /// <summary>루트 노드인지 확인합니다.</summary>
    [JsonIgnore]
    public bool IsRoot => Parent is null;

    /// <summary>리프 노드(자식 없음)인지 확인합니다.</summary>
    [JsonIgnore]
    public bool IsLeaf => Children.Count == 0;

    /// <summary>트리 깊이 (루트=0).</summary>
    [JsonIgnore]
    public int Depth
    {
        get
        {
            int d = 0;
            var cur = Parent;
            while (cur is not null) { d++; cur = cur.Parent; }
            return d;
        }
    }

    /// <summary>루트부터 현재 노드까지의 경로 문자열 (구분자: <c>/</c>).</summary>
    [JsonIgnore]
    public string Path
    {
        get
        {
            var parts = new List<string>();
            var cur = this;
            while (cur is not null) { parts.Insert(0, cur.Name); cur = cur.Parent; }
            return string.Join("/", parts);
        }
    }

    /// <summary>지정 노드가 현재 노드의 조상인지 확인합니다.</summary>
    public bool IsAncestorOf(ConfigNode node)
    {
        var cur = node.Parent;
        while (cur is not null)
        {
            if (ReferenceEquals(cur, this)) return true;
            cur = cur.Parent;
        }
        return false;
    }

    #endregion

    #region §4 ─ 탐색

    /// <summary>
    /// Id 로 하위 노드를 검색합니다 (DFS, 대소문자 무시).
    /// </summary>
    public ConfigNode? FindById(string id)
    {
        if (Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return this;
        foreach (var child in Children)
        {
            var found = child.FindById(id);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// 이름으로 하위 노드를 검색합니다 (DFS, 대소문자 무시).
    /// </summary>
    public ConfigNode? FindByName(string name)
    {
        if (Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return this;
        foreach (var child in Children)
        {
            var found = child.FindByName(name);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// 특정 유형의 모든 하위 노드를 반환합니다 (DFS).
    /// </summary>
    public IEnumerable<ConfigNode> FindAll(NodeType type)
    {
        if (Type == type) yield return this;
        foreach (var child in Children)
            foreach (var n in child.FindAll(type))
                yield return n;
    }

    /// <summary>
    /// 모든 하위 노드를 평탄화하여 반환합니다 (DFS, 자신 포함).
    /// </summary>
    public IEnumerable<ConfigNode> Flatten()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var n in child.Flatten())
                yield return n;
    }

    #endregion

    #region §5 ─ 복제

    /// <summary>
    /// 현재 노드를 깊은 복사합니다. 부모 참조는 포함되지 않습니다.
    /// </summary>
    public ConfigNode DeepClone()
    {
        var clone = new ConfigNode
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Description = Description,
            Enabled = Enabled,
            Order = Order,
            Properties = new Dictionary<string, string>(Properties),
            Parent = null
        };
        foreach (var child in Children)
        {
            var childClone = child.DeepClone();
            childClone.Parent = clone;
            clone.Children.Add(childClone);
        }
        return clone;
    }

    #endregion

    #region §6 ─ 생성자

    /// <summary>
    /// 기본 생성자 (JSON 역직렬화용).
    /// </summary>
    public ConfigNode() { }

    /// <summary>
    /// 이름과 유형을 지정하여 노드를 생성합니다.
    /// </summary>
    public ConfigNode(string name, NodeType type = NodeType.Other)
    {
        Name = name;
        Type = type;
    }

    #endregion

    #region §7 ─ 문자열 표현

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Type}] {Name} (children={Children.Count}, depth={Depth})";

    #endregion
}