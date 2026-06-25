// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/DeviceConfigLoader.cs
//  역할: device.json → ViewModel 역변환 (설정 로드)
//  S-15: 신규
//  S-15 fix: ScaleEntry.Id / AlarmEntry.Id / CommEntry.Id / AbstractTreeNode.Id
//            모두 { get; } getter only → 생성자에서 Id 지정 불가
//            → Id 재생성 방식으로 처리 (Guid 다시 발급)
//  생성: 2026-06-19
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

/// <summary>
/// device.json → ViewModel 복원 서비스.
/// SaveAsync() 의 역방향.
/// ★ ScaleEntry/AlarmEntry/CommEntry/AbstractTreeNode.Id 는 { get; } 이므로
///   JSON에 저장된 Id를 복원하지 않고 새 Guid를 사용한다.
///   (라이브러리 항목 간 참조는 Tag.ScaleEntryId/AlarmEntryId 로 관리되며,
///    현재 버전에서는 로드 후 재연결은 미지원 — S-16 이후 개선 예정)
/// </summary>
public sealed class DeviceConfigLoader
{
    // §1 ─ 옵션 ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §2 ─ 주입 ───────────────────────────────────────────────

    private readonly DeviceTreeViewModel   _treeVm;
    private readonly ScaleLibraryViewModel _scaleVm;
    private readonly AlarmLibraryViewModel _alarmVm;
    private readonly CommLibraryViewModel  _commVm;

    public DeviceConfigLoader(
        DeviceTreeViewModel   treeVm,
        ScaleLibraryViewModel scaleVm,
        AlarmLibraryViewModel alarmVm,
        CommLibraryViewModel  commVm)
    {
        _treeVm  = treeVm;
        _scaleVm = scaleVm;
        _alarmVm = alarmVm;
        _commVm  = commVm;
    }

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    public async Task LoadAsync()
    {
        var path = DeviceConfigService.DeviceJsonPath;
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var root = JsonSerializer.Deserialize<DeviceConfigRoot>(json, _opt);
            if (root is null) return;

            _RestoreScaleLibrary(root.ScaleLibrary);
            _RestoreAlarmLibrary(root.AlarmLibrary);
            _RestoreCommLibrary(root.CommLibrary);
            _RestoreTree(root.Tree);
        }
        catch
        {
            // 손상된 파일: 빈 상태로 시작
        }
    }

    // §4 ─ 스케일 라이브러리 복원 ────────────────────────────
    // ★ ScaleEntry.Id는 { get; } = Guid.NewGuid() → 외부에서 설정 불가
    //   개별 프로퍼티만 설정

    private void _RestoreScaleLibrary(List<ScaleEntryDto> dtos)
    {
        _scaleVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            var entry = new ScaleEntry
            {
                Name          = dto.Name,
                Mode          = Enum.TryParse<ScaleMode>(dto.Mode, out var m)
                                ? m : ScaleMode.Linear,
                RawMin        = dto.RawMin,
                RawMax        = dto.RawMax,
                EngMin        = dto.EngMin,
                EngMax        = dto.EngMax,
                Expression    = dto.Expression,
                Unit          = dto.Unit,
                DecimalPlaces = dto.DecimalPlaces
            };
            _scaleVm.Entries.Add(entry);
        }
    }

    // §5 ─ 알람 라이브러리 복원 ──────────────────────────────

    private void _RestoreAlarmLibrary(List<AlarmEntryDto> dtos)
    {
        _alarmVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            var entry = new AlarmEntry
            {
                Name            = dto.Name,
                Description     = dto.Description,
                HhEnabled       = dto.HhEnabled,
                HhValue         = dto.HhValue,
                HhMessage       = dto.HhMessage,
                HEnabled        = dto.HEnabled,
                HValue          = dto.HValue,
                HMessage        = dto.HMessage,
                LEnabled        = dto.LEnabled,
                LValue          = dto.LValue,
                LMessage        = dto.LMessage,
                LlEnabled       = dto.LlEnabled,
                LlValue         = dto.LlValue,
                LlMessage       = dto.LlMessage,
                DelayMs         = dto.DelayMs,
                RecoveryDelayMs = dto.RecoveryDelayMs
            };
            _alarmVm.Entries.Add(entry);
        }
    }

    // §6 ─ 통신 라이브러리 복원 ──────────────────────────────

    private void _RestoreCommLibrary(List<CommEntryDto> dtos)
    {
        _commVm.Entries.Clear();
        foreach (var dto in dtos)
        {
            var entry = new CommEntry
            {
                Name            = dto.Name,
                Description     = dto.Description,
                Type            = Enum.TryParse<CommType>(dto.Type, out var ct)
                                  ? ct : CommType.ModbusTcp,
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
            };
            _commVm.Entries.Add(entry);
        }
    }

    // §7 ─ 장비 트리 복원 ─────────────────────────────────────

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
        // ★ AbstractTreeNode.Id는 { get; } = Guid.NewGuid() — 외부 설정 불가
        //   Name/Description 등 [ObservableProperty] 필드만 설정 가능

        AbstractTreeNode node = dto.NodeType switch
        {
            "Group"  => new GroupTreeNode(dto.Name)
            {
                Description = dto.Description
            },
            "Device" => new DeviceTreeNode(dto.Name)
            {
                Description  = dto.Description,
                Model        = dto.Model        ?? string.Empty,
                Manufacturer = dto.Manufacturer ?? string.Empty,
                Location     = dto.Location     ?? string.Empty,
                CommType     = Enum.TryParse<NodeCommType>(dto.CommType, out var dct)
                               ? dct : NodeCommType.None,
                Host         = dto.Host   ?? string.Empty,
                Port         = dto.Port   ?? 502,
                PollMs       = dto.PollMs ?? 1000
            },
            "PLC"    => new PlcTreeNode(dto.Name)
            {
                Description = dto.Description,
                CommType    = Enum.TryParse<NodeCommType>(dto.CommType, out var pct)
                              ? pct : NodeCommType.ModbusTcp,
                Host        = dto.Host   ?? "192.168.0.1",
                Port        = dto.Port   ?? 502,
                PollMs      = dto.PollMs ?? 1000
            },
            "Tag"    => new TagTreeNode(dto.Name)
            {
                Description  = dto.Description,
                Address      = dto.Address  ?? string.Empty,
                DataType     = dto.DataType ?? "UInt16",
                Unit         = dto.Unit     ?? string.Empty,
                Memo         = dto.Memo ?? string.Empty,
                IsEnabled    = dto.IsEnabled ?? true,  // ★ S-25 추가
                // ScaleEntryId / AlarmEntryId: ID 재생성으로 라이브러리 재연결 미지원
                // → S-16 이후 Id 보존 방식으로 개선 예정
            },
            _ => null
        };

        if (node is null) return null;

        // 하위 노드 재귀 복원
        foreach (var childDto in dto.Children)
        {
            var child = _BuildNode(childDto);
            if (child is not null)
                node.Children.Add(child);
        }

        return node;
    }
}
