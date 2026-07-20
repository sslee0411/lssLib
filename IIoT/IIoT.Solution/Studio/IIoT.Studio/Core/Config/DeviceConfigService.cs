// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/DeviceConfigService.cs
//  역할: device.json 저장 + .signal 파일 발행
//        ViewModel → DTO 변환 → 원자적 파일 쓰기
//  S-10: 초기 구현
//  S-14 fix: [한글 깨짐] _jsonOpt에 Encoder 추가
//  S-Virtual01: TagTreeNode.IsVirtual/Expression → DTO 직렬화 추가
//  S-Virtual02: TagTreeNode.UseRoslynScript/ScriptCode → DTO 직렬화 추가
//  S-프로토콜01: ProtocolLibraryViewModel 주입 + ProtocolLibrary 직렬화 추가
//  생성: 2026-06-17 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

// §1 ─ 서비스 ─────────────────────────────────────────────

public sealed class DeviceConfigService
{
    // §1-1 ─ 직렬화 옵션 ─────────────────────────────────────

    // ★ S-14 fix: Encoder 추가 → 한글·특수문자를 \uXXXX 이스케이프 없이 그대로 저장
    private static readonly JsonSerializerOptions _jsonOpt = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §1-2 ─ 경로 ─────────────────────────────────────────────

    /// <summary>Config 폴더 (실행파일 기준)</summary>
    public static string ConfigDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    public static string DeviceJsonPath => Path.Combine(ConfigDir, "device.json");
    public static string SignalFilePath => Path.Combine(ConfigDir, "device.json.signal");

    // §1-3 ─ 주입된 ViewModel ─────────────────────────────────

    private readonly DeviceTreeViewModel      _treeVm;
    private readonly ScaleLibraryViewModel    _scaleVm;
    private readonly AlarmLibraryViewModel    _alarmVm;
    private readonly CommLibraryViewModel     _commVm;
    private readonly ProtocolLibraryViewModel _protocolVm;   // ★ S-프로토콜01

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public DeviceConfigService(
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

    // §3 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// 전체 설정을 device.json 으로 저장하고 .signal 파일을 발행한다.
    /// </summary>
    public async Task<SaveResult> SaveAsync()
    {
        try
        {
            _EnsureConfigDir();

            var root = _BuildDto();

            // ① 1차 직렬화 (Sha256 빈 상태)
            var json = JsonSerializer.Serialize(root, _jsonOpt);

            // ② SHA-256 계산 후 삽입
            root.Sha256 = _ComputeSha256(json);
            json = JsonSerializer.Serialize(root, _jsonOpt);

            // ③ 원자적 쓰기: .tmp → File.Replace → .bak
            _AtomicWrite(DeviceJsonPath, json);

            // ④ .signal 파일 발행 → IIoT.Collector FSW 감지
            await _WriteSignalAsync();

            return SaveResult.Ok(DeviceJsonPath);
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    // §4 ─ 내부 메서드 ────────────────────────────────────────

    private DeviceConfigRoot _BuildDto()
    {
        var root = new DeviceConfigRoot { SavedAt = DateTime.Now };

        // ── 트리 ──────────────────────────────────────────────
        foreach (var node in _treeVm.RootNodes)
            root.Tree.Add(_MapNode(node));

        // ── 스케일 라이브러리 ──────────────────────────────────
        foreach (var s in _scaleVm.Entries)
        {
            root.ScaleLibrary.Add(new ScaleEntryDto
            {
                Id            = s.Id.ToString(),
                Name          = s.Name,
                Mode          = s.Mode.ToString(),
                RawMin        = s.RawMin,
                RawMax        = s.RawMax,
                EngMin        = s.EngMin,
                EngMax        = s.EngMax,
                Expression    = s.Expression ?? string.Empty,
                Unit          = s.Unit ?? string.Empty,
                DecimalPlaces = s.DecimalPlaces
            });
        }

        // ── 알람 라이브러리 ────────────────────────────────────
        foreach (var a in _alarmVm.Entries)
        {
            root.AlarmLibrary.Add(new AlarmEntryDto
            {
                Id              = a.Id.ToString(),
                Name            = a.Name,
                Description     = a.Description ?? string.Empty,
                HhEnabled       = a.HhEnabled,
                HhValue         = a.HhValue,
                HhMessage       = a.HhMessage ?? string.Empty,
                HEnabled        = a.HEnabled,
                HValue          = a.HValue,
                HMessage        = a.HMessage ?? string.Empty,
                LEnabled        = a.LEnabled,
                LValue          = a.LValue,
                LMessage        = a.LMessage ?? string.Empty,
                LlEnabled       = a.LlEnabled,
                LlValue         = a.LlValue,
                LlMessage       = a.LlMessage ?? string.Empty,
                DelayMs         = a.DelayMs,
                RecoveryDelayMs = a.RecoveryDelayMs,
                // ★ C-14 신규
                NotifyEmail     = a.NotifyEmail ?? string.Empty,
                NotifyPhone     = a.NotifyPhone ?? string.Empty,
                EscalateMinutes = a.EscalateMinutes
            });
        }

        // ── 통신 라이브러리 ────────────────────────────────────
        foreach (var c in _commVm.Entries)
        {
            root.CommLibrary.Add(new CommEntryDto
            {
                Id              = c.Id.ToString(),
                Name            = c.Name,
                Description     = c.Description ?? string.Empty,
                Type            = c.Type.ToString(),
                // Modbus TCP
                Host            = c.Host ?? string.Empty,
                Port            = c.Port,
                SlaveId         = c.SlaveId,
                // Serial
                ComPort         = c.ComPort ?? string.Empty,
                BaudRate        = c.BaudRate,
                Parity          = c.Parity ?? string.Empty,
                DataBits        = c.DataBits,
                StopBits        = c.StopBits ?? string.Empty,
                // MQTT
                BrokerHost      = c.BrokerHost ?? string.Empty,
                BrokerPort      = c.BrokerPort,
                ClientId        = c.ClientId ?? string.Empty,
                Topic           = c.Topic ?? string.Empty,
                UseTls          = c.UseTls,
                MqttUser        = c.MqttUser ?? string.Empty,
                MqttPassword    = c.MqttPassword ?? string.Empty,
                // OPC-UA
                EndpointUrl     = c.EndpointUrl ?? string.Empty,
                OpcUser         = c.OpcUser ?? string.Empty,
                OpcPassword     = c.OpcPassword ?? string.Empty,
                // 공통
                PollMs          = c.PollMs,
                TimeoutMs       = c.TimeoutMs,
                RetryIntervalMs = c.RetryIntervalMs
            });
        }

        // ── 프로토콜 라이브러리 (★ S-프로토콜01) ────────────────
        foreach (var p in _protocolVm.Entries)
        {
            root.ProtocolLibrary.Add(new ProtocolEntryDto
            {
                Id             = p.Id.ToString(),
                Name           = p.Name,
                Description    = p.Description ?? string.Empty,
                UseFraming     = p.UseFraming,
                StxHex         = p.StxHex ?? string.Empty,
                HasLengthField = p.HasLengthField,
                CrcType        = p.CrcType ?? "None",
                ReadBlocks     = p.ReadBlocks.Select(_MapBlock).ToList(),
                WriteBlocks    = p.WriteBlocks.Select(_MapBlock).ToList()
            });
        }

        return root;
    }

    private static ProtocolBlockDto _MapBlock(ProtocolBlock b) => new()
    {
        Id           = b.Id.ToString(),
        Name         = b.Name,
        Description  = b.Description ?? string.Empty,
        StartAddress = b.StartAddress ?? string.Empty,
        Length       = b.Length,
        CmdCode      = b.CmdCode ?? string.Empty,
        Fields       = b.Fields.Select(f => new ProtocolFieldDto
        {
            Id         = f.Id.ToString(),
            Name       = f.Name,
            ByteOffset = f.ByteOffset,
            BufType    = f.BufType ?? "UInt16",
            Unit       = f.Unit ?? string.Empty,
            ScaleMin   = f.ScaleMin,
            ScaleMax   = f.ScaleMax,
            ScaleEntryId = f.ScaleEntryId?.ToString()
        }).ToList()
    };

    private static DeviceNodeDto _MapNode(AbstractTreeNode node)
    {
        var dto = new DeviceNodeDto
        {
            Name        = node.Name,
            Description = node.Description ?? string.Empty
        };

        switch (node)
        {
            case GroupTreeNode g:
                dto.NodeType = "Group";
                dto.Id       = g.Id.ToString();
                break;

            case DeviceTreeNode d:
                dto.NodeType = "Device";
                dto.Id = d.Id.ToString();
                dto.Model = d.Model;
                dto.Manufacturer = d.Manufacturer;
                dto.Location = d.Location;
                dto.CommType = d.CommType.ToString();
                dto.Host = d.Host;
                dto.Port = d.Port;
                dto.PollMs = d.PollMs;
                // ★ Studio-P03b: 통신 라이브러리 참조
                dto.CommEntryId = d.CommEntryId?.ToString();
                // ★ S-프로토콜01: 프로토콜 라이브러리 참조
                dto.ProtocolEntryId = d.ProtocolEntryId?.ToString();
                // ★ Studio-P03b: 플러그인 드라이버
                dto.DriverId = string.IsNullOrEmpty(d.DriverId) ? null : d.DriverId;
                dto.DriverParams = d.DriverParams.Count > 0
                                   ? new Dictionary<string, string>(d.DriverParams)
                                   : null;
                break;

            case PlcTreeNode p:
                dto.NodeType = "PLC";
                dto.Id = p.Id.ToString();
                dto.CommType = p.CommType.ToString();
                dto.Host = p.Host;
                dto.Port = p.Port;
                dto.PollMs = p.PollMs;
                // ★ S-28: 통신 라이브러리 참조
                dto.CommEntryId = p.CommEntryId?.ToString();
                // ★ S-프로토콜01: 프로토콜 라이브러리 참조
                dto.ProtocolEntryId = p.ProtocolEntryId?.ToString();
                // ★ Studio-P02: 플러그인 드라이버 ID 직렬화
                //   빈 문자열이면 null → JSON에서 생략됨
                dto.DriverId = string.IsNullOrEmpty(p.DriverId)
                                  ? null
                                  : p.DriverId;
                // ★ Studio-P02: 드라이버 파라미터 직렬화
                //   비어있으면 null → JSON에서 생략됨
                dto.DriverParams = p.DriverParams.Count > 0
                                   ? new Dictionary<string, string>(p.DriverParams)
                                   : null;
                break;

            case TagTreeNode t:
                dto.NodeType     = "Tag";
                dto.Id           = t.Id.ToString();
                dto.Address      = t.Address;
                dto.DataType     = t.DataType;
                dto.Unit         = t.Unit;
                dto.ScaleEntryId = t.ScaleEntryId?.ToString();
                dto.AlarmEntryId = t.AlarmEntryId?.ToString();
                dto.Memo         = t.Memo;
                dto.IsEnabled    = t.IsEnabled;
                // ★ S-Virtual01: 가상(계산) Tag — false/빈 값이면 JSON 생략
                dto.IsVirtual    = t.IsVirtual ? true : null;
                dto.Expression   = string.IsNullOrWhiteSpace(t.Expression) ? null : t.Expression;
                // ★ S-Virtual02: Function 노드 — Roslyn C# 고급 스크립트 모드
                dto.UseRoslynScript = t.UseRoslynScript ? true : null;
                dto.ScriptCode      = string.IsNullOrWhiteSpace(t.ScriptCode) ? null : t.ScriptCode;
                break;
        }

        foreach (var child in node.Children)
            dto.Children.Add(_MapNode(child));

        return dto;
    }

    private static void _EnsureConfigDir()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);
    }

    /// <summary>원자적 쓰기: .tmp → File.Replace → .bak</summary>
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

    /// <summary>.signal 파일 발행 — Collector FSW 감지용</summary>
    private static async Task _WriteSignalAsync()
    {
        await File.WriteAllTextAsync(
            SignalFilePath,
            DateTime.Now.ToString("O"),
            Encoding.UTF8);
    }
}

// §5 ─ 결과 타입 ──────────────────────────────────────────

public sealed record SaveResult(bool IsSuccess, string Message)
{
    public static SaveResult Ok(string path)    => new(true,  $"저장 완료: {Path.GetFileName(path)}");
    public static SaveResult Fail(string error) => new(false, $"저장 실패: {error}");
}
