// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/CollectConfigService.cs
//  역할: collect.json 저장 + .signal 발행
//  S-11: 초기 구현
//  생성: 2026-06-17
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

// §1 ─ DTO ────────────────────────────────────────────────

public sealed class CollectConfigRoot
{
    public string   Version { get; set; } = "1.0";
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string   Sha256  { get; set; } = string.Empty;
    public List<CanvasNodeDto>       Nodes       { get; set; } = new();
    public List<CanvasConnectionDto> Connections { get; set; } = new();
}

public sealed class CanvasNodeDto
{
    public string NodeId    { get; set; } = string.Empty;
    public string NodeType  { get; set; } = string.Empty;
    public string Label     { get; set; } = string.Empty;
    public double X         { get; set; }
    public double Y         { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public sealed class CanvasConnectionDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = string.Empty;
}

// §2 ─ 서비스 ─────────────────────────────────────────────

public sealed class CollectConfigService
{
    // §2-1 ─ 경로 ─────────────────────────────────────────────

    public static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    public static string CollectJsonPath =>
        Path.Combine(ConfigDir, "collect.json");

    public static string SignalFilePath =>
        Path.Combine(ConfigDir, "collect.json.signal");

    // §2-2 ─ 직렬화 옵션 ─────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpt = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    // §3 ─ 주입 ───────────────────────────────────────────────

    private readonly CanvasViewModel _canvasVm;

    public CollectConfigService(CanvasViewModel canvasVm)
        => _canvasVm = canvasVm;

    // §4 ─ 저장 ───────────────────────────────────────────────

    public async Task<SaveResult> SaveAsync()
    {
        try
        {
            _EnsureConfigDir();

            var root = _BuildDto();

            var json = JsonSerializer.Serialize(root, _jsonOpt);
            root.Sha256 = _ComputeSha256(json);
            json = JsonSerializer.Serialize(root, _jsonOpt);

            _AtomicWrite(CollectJsonPath, json);
            await _WriteSignalAsync();

            return SaveResult.Ok(CollectJsonPath);
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    // §5 ─ 내부 헬퍼 ──────────────────────────────────────────

    private CollectConfigRoot _BuildDto()
    {
        var root = new CollectConfigRoot { SavedAt = DateTime.Now };

        foreach (var node in _canvasVm.Nodes)
        {
            var dto = new CanvasNodeDto
            {
                NodeId   = node.NodeId,
                NodeType = node.NodeType,
                Label    = node.Label,
                X        = node.X,
                Y        = node.Y
            };

            // 노드 타입별 속성 직렬화
            switch (node)
            {
                case ModbusInputNode m:
                    dto.Properties["Host"]     = m.Host;
                    dto.Properties["Port"]     = m.Port;
                    dto.Properties["SlaveId"]  = m.SlaveId;
                    dto.Properties["Register"] = m.Register;
                    dto.Properties["PollMs"]   = m.PollMs;
                    break;
                case TcpInputNode t:
                    dto.Properties["Host"] = t.Host;
                    dto.Properties["Port"] = t.Port;
                    break;
                case BufferParserNode b:
                    dto.Properties["Schema"] = b.Schema;
                    break;
                case ScaleFilterNode s:
                    dto.Properties["RawMin"] = s.RawMin;
                    dto.Properties["RawMax"] = s.RawMax;
                    dto.Properties["EngMin"] = s.EngMin;
                    dto.Properties["EngMax"] = s.EngMax;
                    dto.Properties["Unit"]   = s.Unit;
                    break;
                case DbOutputNode d:
                    dto.Properties["TableName"] = d.TableName;
                    break;
                case MqttOutputNode mq:
                    dto.Properties["Broker"] = mq.Broker;
                    dto.Properties["Port"]   = mq.Port;
                    dto.Properties["Topic"]  = mq.Topic;
                    break;
            }

            root.Nodes.Add(dto);
        }

        foreach (var conn in _canvasVm.Connections)
        {
            root.Connections.Add(new CanvasConnectionDto
            {
                ConnectionId = conn.ConnectionId,
                SourceNodeId = conn.SourceNodeId,
                SourcePortId = conn.SourcePortId,
                TargetNodeId = conn.TargetNodeId,
                TargetPortId = conn.TargetPortId
            });
        }

        return root;
    }

    private static void _EnsureConfigDir()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);
    }

    private static void _AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        var bak = path + ".bak";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        if (File.Exists(path))
            File.Replace(tmp, path, bak);
        else
            File.Move(tmp, path);
    }

    private static string _ComputeSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task _WriteSignalAsync()
    {
        await File.WriteAllTextAsync(
            SignalFilePath,
            DateTime.Now.ToString("O"),
            Encoding.UTF8);
    }
}
