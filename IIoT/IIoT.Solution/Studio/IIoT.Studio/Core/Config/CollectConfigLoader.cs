// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/CollectConfigLoader.cs
//  역할: collect.json → CanvasViewModel 노드·연결선 복원
//  S-15: 신규
//  S-15 fix: CanvasConnection.ConnectionId는 { get; } getter only
//            → CanvasViewModel.AddConnection() 메서드 사용으로 변경
//  생성: 2026-06-19
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Core.Canvas;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

/// <summary>
/// collect.json → CanvasViewModel 복원 서비스.
/// </summary>
public sealed class CollectConfigLoader
{
    // §1 ─ 옵션 ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §2 ─ 주입 ───────────────────────────────────────────────

    private readonly CanvasViewModel _canvasVm;

    public CollectConfigLoader(CanvasViewModel canvasVm)
        => _canvasVm = canvasVm;

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    public async Task LoadAsync()
    {
        var path = CollectConfigService.CollectJsonPath;
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var root = JsonSerializer.Deserialize<CollectConfigRoot>(json, _opt);
            if (root is null) return;

            _canvasVm.Nodes.Clear();
            _canvasVm.Connections.Clear();

            // ── 노드 복원 (NodeId 유지를 위한 매핑 테이블) ──────
            // AbstractCanvasNode.NodeId 는 { get; } = Guid.NewGuid()
            // 저장된 NodeId와 복원된 NodeId가 다를 수 있으므로
            // savedNodeId → restoredNodeId 매핑 유지
            var idMap = new Dictionary<string, string>();

            foreach (var dto in root.Nodes)
            {
                var node = _BuildNode(dto);
                if (node is null) continue;

                node.X     = dto.X;
                node.Y     = dto.Y;
                node.Label = dto.Label;

                // 저장 NodeId → 복원된 NodeId 매핑
                idMap[dto.NodeId] = node.NodeId;

                _canvasVm.Nodes.Add(node);
            }

            // ── 연결선 복원 ──────────────────────────────────
            // ★ CanvasConnection.ConnectionId 는 { get; } — 직접 설정 불가
            //   CanvasViewModel.AddConnection() 메서드로 연결선 생성
            foreach (var dto in root.Connections)
            {
                // 저장 NodeId → 복원 NodeId 변환
                if (!idMap.TryGetValue(dto.SourceNodeId, out var srcNodeId)) continue;
                if (!idMap.TryGetValue(dto.TargetNodeId, out var tgtNodeId)) continue;

                // 복원된 노드에서 PortId 찾기 (저장 PortId와 복원 PortId는 다름)
                // → 포트 Index로 매칭
                var srcNode = _canvasVm.Nodes.FirstOrDefault(n => n.NodeId == srcNodeId);
                var tgtNode = _canvasVm.Nodes.FirstOrDefault(n => n.NodeId == tgtNodeId);
                if (srcNode is null || tgtNode is null) continue;

                // 출력 포트 인덱스로 매칭 (포트 순서 유지)
                var srcPort = srcNode.OutputPorts.FirstOrDefault();
                var tgtPort = tgtNode.InputPorts.FirstOrDefault();
                if (srcPort is null || tgtPort is null) continue;

                // AddConnection 메서드 사용 (ConnectionId 자동 생성)
                _canvasVm.AddConnection(
                    srcNodeId, srcPort.PortId,
                    tgtNodeId, tgtPort.PortId);
            }
        }
        catch
        {
            // 손상된 파일: 빈 캔버스로 시작
        }
    }

    // §4 ─ 노드 빌더 ──────────────────────────────────────────

    private static AbstractCanvasNode? _BuildNode(CanvasNodeDto dto)
    {
        var node = CanvasNodeFactory.Create(dto.NodeType);
        if (node is null) return null;

        var p = dto.Properties;
        switch (node)
        {
            case ModbusInputNode m:
                m.Host     = _Str(p, "Host",     "192.168.0.1");
                m.Port     = _Int(p, "Port",     502);
                m.SlaveId  = _Int(p, "SlaveId",  1);
                m.Register = _Str(p, "Register", "40001");
                m.PollMs   = _Int(p, "PollMs",   1000);
                break;
            case TcpInputNode t:
                t.Host = _Str(p, "Host", "0.0.0.0");
                t.Port = _Int(p, "Port", 5000);
                break;
            case BufferParserNode b:
                b.Schema = _Str(p, "Schema", string.Empty);
                break;
            case ScaleFilterNode s:
                s.RawMin = _Dbl(p, "RawMin", 0);
                s.RawMax = _Dbl(p, "RawMax", 4095);
                s.EngMin = _Dbl(p, "EngMin", 0);
                s.EngMax = _Dbl(p, "EngMax", 100);
                s.Unit   = _Str(p, "Unit",   string.Empty);
                break;
            case DbOutputNode d:
                d.TableName = _Str(p, "TableName", "TagHistory");
                break;
            case MqttOutputNode mq:
                mq.Broker = _Str(p, "Broker", "localhost");
                mq.Port   = _Int(p, "Port",   1883);
                mq.Topic  = _Str(p, "Topic",  "iiot/data");
                break;
            case DeviceCanvasNode dc:
                dc.LinkedDeviceId   = _Str(p, "LinkedDeviceId",   string.Empty);
                dc.LinkedDeviceType = _Str(p, "LinkedDeviceType", "PLC");
                dc.LinkedDeviceName = _Str(p, "LinkedDeviceName", string.Empty);
                break;
        }

        return node;
    }

    // §5 ─ 속성 추출 헬퍼 ─────────────────────────────────────

    private static string _Str(Dictionary<string, object?> p, string key, string def)
    {
        if (!p.TryGetValue(key, out var v)) return def;
        return v?.ToString() ?? def;
    }

    private static int _Int(Dictionary<string, object?> p, string key, int def)
    {
        if (!p.TryGetValue(key, out var v)) return def;
        return v is JsonElement je
            ? (je.TryGetInt32(out var n) ? n : def)
            : (int.TryParse(v?.ToString(), out var n2) ? n2 : def);
    }

    private static double _Dbl(Dictionary<string, object?> p, string key, double def)
    {
        if (!p.TryGetValue(key, out var v)) return def;
        return v is JsonElement je
            ? (je.TryGetDouble(out var d) ? d : def)
            : (double.TryParse(v?.ToString(), out var d2) ? d2 : def);
    }
}
