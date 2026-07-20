// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/DeviceConfigLoader.cs
//  역할: device.json → ViewModel 역변환 (설정 로드)
//  S-15: 신규
//  S-15 fix: Id 재생성 방식 (AbstractTreeNode.Id는 { get; } — 외부 설정 불가)
//  Studio-P02: _BuildNode PLC 케이스에 CommTypeMigrator + DriverParams 적용
//  S-Virtual01: _BuildNode Tag 케이스에 IsVirtual/Expression 복원 추가
//  S-Virtual02: _BuildNode Tag 케이스에 UseRoslynScript/ScriptCode 복원 추가
//  S-프로토콜01: ProtocolLibraryViewModel 주입 + _RestoreProtocolLibrary 추가,
//               _BuildPlcNode/_BuildDeviceNode 에 ProtocolEntryId 복원 추가
//  생성: 2026-06-19 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Contracts.Migration;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

public sealed class DeviceConfigLoader
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly DeviceTreeViewModel     _treeVm;
    private readonly ScaleLibraryViewModel   _scaleVm;
    private readonly AlarmLibraryViewModel   _alarmVm;
    private readonly CommLibraryViewModel    _commVm;
    // ★ S-프로토콜01
    private readonly ProtocolLibraryViewModel _protocolVm;

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §2 ─ 생성자 ──────────────────────────────────────────

    public DeviceConfigLoader(
        DeviceTreeViewModel      treeVm,
        ScaleLibraryViewModel    scaleVm,
        AlarmLibraryViewModel    alarmVm,
        CommLibraryViewModel     commVm,
        ProtocolLibraryViewModel protocolVm)   // ★ S-프로토콜01
    {
        _treeVm     = treeVm;
        _scaleVm    = scaleVm;
        _alarmVm    = alarmVm;
        _commVm     = commVm;
        _protocolVm = protocolVm;
    }

    // §3 ─ 로드 진입점 ─────────────────────────────────────

    public async Task LoadAsync(string? path = null)
    {
        var filePath = path ?? DeviceConfigService.DeviceJsonPath;
        if (!File.Exists(filePath)) return;

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var root = JsonSerializer.Deserialize<DeviceConfigRoot>(json, _opts);
        if (root is null) return;

        _RestoreScaleLibrary(root.ScaleLibrary);
        _RestoreAlarmLibrary(root.AlarmLibrary);
        _RestoreCommLibrary(root.CommLibrary);
        _RestoreProtocolLibrary(root.ProtocolLibrary);   // ★ S-프로토콜01
        _RestoreTree(root.Tree);
    }

    // §4 ─ 스케일 라이브러리 복원 ─────────────────────────

    private void _RestoreScaleLibrary(List<ScaleEntryDto> dtos)
    {
        _scaleVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            _scaleVm.Entries.Add(new ScaleEntry
            {
                Name          = dto.Name,
                Mode          = Enum.TryParse<ScaleMode>(dto.Mode, out var m) ? m : ScaleMode.Linear,
                RawMin        = dto.RawMin,
                RawMax        = dto.RawMax,
                EngMin        = dto.EngMin,
                EngMax        = dto.EngMax,
                Expression    = dto.Expression,
                Unit          = dto.Unit,
                DecimalPlaces = dto.DecimalPlaces
            });
        }
    }

    // §5 ─ 알람 라이브러리 복원 ──────────────────────────

    private void _RestoreAlarmLibrary(List<AlarmEntryDto> dtos)
    {
        _alarmVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            _alarmVm.Entries.Add(new AlarmEntry
            {
                Name            = dto.Name,
                Description     = dto.Description,
                HhEnabled       = dto.HhEnabled, HhValue = dto.HhValue, HhMessage = dto.HhMessage,
                HEnabled        = dto.HEnabled,  HValue  = dto.HValue,  HMessage  = dto.HMessage,
                LEnabled        = dto.LEnabled,  LValue  = dto.LValue,  LMessage  = dto.LMessage,
                LlEnabled       = dto.LlEnabled, LlValue = dto.LlValue, LlMessage = dto.LlMessage,
                DelayMs         = dto.DelayMs,
                RecoveryDelayMs = dto.RecoveryDelayMs,
                // ★ C-14 신규
                NotifyEmail = dto.NotifyEmail,
                NotifyPhone = dto.NotifyPhone,
                EscalateMinutes = dto.EscalateMinutes
            });
        }
    }

    // §6 ─ 통신 라이브러리 복원 ──────────────────────────

    private void _RestoreCommLibrary(List<CommEntryDto> dtos)
    {
        _commVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            _commVm.Entries.Add(new CommEntry
            {
                Name            = dto.Name,
                Description     = dto.Description,
                Type            = Enum.TryParse<CommType>(dto.Type, out var ct) ? ct : CommType.ModbusTcp,
                Host            = dto.Host,
                Port            = dto.Port,
                SlaveId         = dto.SlaveId,
                ComPort         = dto.ComPort,
                BaudRate        = dto.BaudRate,
                Parity          = dto.Parity,
                DataBits        = dto.DataBits,
                StopBits        = dto.StopBits,
                BrokerHost      = dto.BrokerHost,
                BrokerPort      = dto.BrokerPort,
                ClientId        = dto.ClientId,
                Topic           = dto.Topic,
                UseTls          = dto.UseTls,
                MqttUser        = dto.MqttUser,
                MqttPassword    = dto.MqttPassword,
                EndpointUrl     = dto.EndpointUrl,
                OpcUser         = dto.OpcUser,
                OpcPassword     = dto.OpcPassword,
                PollMs          = dto.PollMs,
                TimeoutMs       = dto.TimeoutMs,
                RetryIntervalMs = dto.RetryIntervalMs
            });
        }
    }

    // §6-1 ─ ★ S-프로토콜01: 프로토콜 라이브러리 복원 ────────

    private void _RestoreProtocolLibrary(List<ProtocolEntryDto> dtos)
    {
        _protocolVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            var entry = new ProtocolEntry
            {
                Name           = dto.Name,
                Description    = dto.Description,
                UseFraming     = dto.UseFraming,
                StxHex         = dto.StxHex,
                HasLengthField = dto.HasLengthField,
                CrcType        = dto.CrcType
            };

            foreach (var b in dto.ReadBlocks)
                entry.ReadBlocks.Add(_BuildProtocolBlock(b));
            foreach (var b in dto.WriteBlocks)
                entry.WriteBlocks.Add(_BuildProtocolBlock(b));

            _protocolVm.Entries.Add(entry);
        }
    }

    private static ProtocolBlock _BuildProtocolBlock(ProtocolBlockDto dto)
    {
        var block = new ProtocolBlock
        {
            Name         = dto.Name,
            Description  = dto.Description,
            StartAddress = dto.StartAddress,
            Length       = dto.Length,
            CmdCode      = dto.CmdCode
        };

        foreach (var f in dto.Fields)
        {
            var field = new ProtocolField
            {
                Name       = f.Name,
                ByteOffset = f.ByteOffset,
                BufType    = f.BufType,
                Unit       = f.Unit,
                ScaleMin   = f.ScaleMin,
                ScaleMax   = f.ScaleMax
            };
            // ★ S-프로토콜01 Step B 후속: ScaleEntryId 복원
            if (Guid.TryParse(f.ScaleEntryId, out var scaleId))
                field.ScaleEntryId = scaleId;

            block.Fields.Add(field);
        }

        return block;
    }

    // §7 ─ 장비 트리 복원 ─────────────────────────────────

    private void _RestoreTree(List<DeviceNodeDto> dtos)
    {
        _treeVm.RootNodes.Clear();
        foreach (var dto in dtos)
        {
            var node = _BuildNode(dto);
            if (node is not null)
                _treeVm.RootNodes.Add(node);
        }
    }

    private static AbstractTreeNode? _BuildNode(DeviceNodeDto dto)
    {
        AbstractTreeNode? node = dto.NodeType switch
        {
            "Group"  => new GroupTreeNode(dto.Name)
            {
                Description = dto.Description
            },
            "Device" => _BuildDeviceNode(dto),
            "PLC"    => _BuildPlcNode(dto),   // ★ Studio-P02: 별도 메서드로 분리
            "Tag"    => new TagTreeNode(dto.Name)
            {
                Description = dto.Description,
                Address     = dto.Address  ?? string.Empty,
                DataType    = dto.DataType ?? "UInt16",
                Unit        = dto.Unit     ?? string.Empty,
                Memo        = dto.Memo     ?? string.Empty,
                IsEnabled   = dto.IsEnabled ?? true,
                // ★ S-Virtual01: 가상(계산) Tag 복원
                IsVirtual   = dto.IsVirtual ?? false,
                Expression  = dto.Expression ?? string.Empty,
                // ★ S-Virtual02: Function 노드 — Roslyn C# 고급 스크립트 모드 복원
                UseRoslynScript = dto.UseRoslynScript ?? false,
                ScriptCode      = dto.ScriptCode ?? string.Empty
            },
            _ => null
        };

        if (node is null) return null;

        foreach (var childDto in dto.Children)
        {
            var child = _BuildNode(childDto);
            if (child is not null)
                node.Children.Add(child);
        }

        return node;
    }

    // §7-1 ─ ★ Studio-P02: PLC 노드 복원 ─────────────────

    private static PlcTreeNode _BuildPlcNode(DeviceNodeDto dto)
    {
        var plc = new PlcTreeNode(dto.Name)
        {
            Description = dto.Description,
            CommType    = Enum.TryParse<NodeCommType>(dto.CommType, out var pct)
                          ? pct : NodeCommType.ModbusTcp,
            Host        = dto.Host   ?? "192.168.0.1",
            Port        = dto.Port   ?? 502,
            PollMs      = dto.PollMs ?? 1000,

            // ★ CommTypeMigrator: driverId 있으면 그대로, commType만 있으면 변환
            //   "ModbusTcp" → "modbus-tcp" / "mitsubishi-mc" → 그대로
            DriverId    = CommTypeMigrator.Resolve(dto.DriverId, dto.CommType),
        };

        // ★ DriverParams: null → 빈 딕셔너리 (non-null 보장)
        //   Dictionary { get; } = new() 이므로 init 불가 → Add 방식
        if (dto.DriverParams is not null)
        {
            foreach (var kv in dto.DriverParams)
                plc.DriverParams[kv.Key] = kv.Value;
        }

        // CommEntryId 복원 (S-28)
        if (Guid.TryParse(dto.CommEntryId, out var commId))
            plc.CommEntryId = commId;

        // ★ S-프로토콜01: ProtocolEntryId 복원
        if (Guid.TryParse(dto.ProtocolEntryId, out var protoId))
            plc.ProtocolEntryId = protoId;

        return plc;
    }

    private static DeviceTreeNode _BuildDeviceNode(DeviceNodeDto dto)
    {
        var dev = new DeviceTreeNode(dto.Name)
        {
            Description = dto.Description,
            Model = dto.Model ?? string.Empty,
            Manufacturer = dto.Manufacturer ?? string.Empty,
            Location = dto.Location ?? string.Empty,
            CommType = Enum.TryParse<NodeCommType>(dto.CommType, out var dct)
                           ? dct : NodeCommType.None,
            Host = dto.Host ?? string.Empty,
            Port = dto.Port ?? 502,
            PollMs = dto.PollMs ?? 1000,
            // ★ Studio-P03b: 플러그인 드라이버
            DriverId = CommTypeMigrator.Resolve(dto.DriverId, dto.CommType),
        };

        if (dto.DriverParams is not null)
            foreach (var kv in dto.DriverParams)
                dev.DriverParams[kv.Key] = kv.Value;

        if (Guid.TryParse(dto.CommEntryId, out var commId))
            dev.CommEntryId = commId;

        // ★ S-프로토콜01: ProtocolEntryId 복원
        if (Guid.TryParse(dto.ProtocolEntryId, out var protoId))
            dev.ProtocolEntryId = protoId;

        return dev;
    }
}
