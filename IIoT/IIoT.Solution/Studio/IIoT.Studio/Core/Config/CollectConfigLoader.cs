// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/CollectConfigLoader.cs
//  역할: collect.json → CanvasViewModel 노드·연결선 복원
//  S-15: 신규
//  S-15 fix: CanvasConnection.ConnectionId는 { get; } getter only
//            → CanvasViewModel.AddConnection() 메서드 사용으로 변경
//  S-20 (N포트 노드): Splitter/CompositeCalc 포트 라벨·Expression 복원 추가 +
//               연결선 복원을 FirstOrDefault()(항상 첫 포트) 대신 저장된
//               SourcePortIndex/TargetPortIndex 매칭으로 수정(다중 포트 노드의
//               연결선이 정확한 포트로 복원되도록 — 기존 버그 수정)
//  생성: 2026-06-19 / 수정: 2026-07-20
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

                // ★ S-20 fix: 저장된 SourcePortIndex/TargetPortIndex 로 매칭
                //   (다중 포트 노드는 FirstOrDefault() 만으로는 항상 첫 포트에
                //   연결되는 버그가 있었음 — Index 매칭 후 실패 시 첫 포트로 폴백)
                var srcPort = srcNode.OutputPorts.FirstOrDefault(p => p.Index == dto.SourcePortIndex)
                              ?? srcNode.OutputPorts.FirstOrDefault();
                var tgtPort = tgtNode.InputPorts.FirstOrDefault(p => p.Index == dto.TargetPortIndex)
                              ?? tgtNode.InputPorts.FirstOrDefault();
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
            // ★ S-20: 분배기 — 저장된 라벨 개수·이름 그대로 출력 포트 재구성
            //   (팩토리 기본 2개 포트를 지우고 저장값으로 다시 채움)
            case SplitterNode sp:
                var outLabels = _StrList(p, "OutputLabels");
                if (outLabels.Count > 0)
                {
                    sp.OutputPorts.Clear();
                    foreach (var label in outLabels) sp.AddOutputPort(label);
                }
                break;
            // ★ S-20: 복합계산 — 저장된 입력 포트 라벨 + NCalc 식 복원
            case CompositeCalcNode cc:
                var inLabels = _StrList(p, "InputLabels");
                if (inLabels.Count > 0)
                {
                    cc.InputPorts.Clear();
                    foreach (var label in inLabels) cc.AddInputPort(label);
                }
                cc.Expression = _Str(p, "Expression", string.Empty);
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

    // ★ S-20: 문자열 목록 추출(Splitter.OutputLabels / CompositeCalc.InputLabels) —
    //   Properties 는 object? 로 저장되므로 역직렬화 시 JsonElement(배열) 형태로 옴
    private static List<string> _StrList(Dictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v)) return new List<string>();
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray()
                     .Select(e => e.GetString() ?? string.Empty)
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToList();
        return new List<string>();
    }
}
