// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Core/Config/ConfigImportExportService.cs
//  역할: 설정 파일 열기 / 다른 이름으로 저장 / Tag CSV 내보내기
//  S-18: 신규
//  생성: 2026-06-20
// ══════════════════════════════════════════════════════════

using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Studio.Core.Config;

public sealed class ConfigImportExportService
{
    // §1 ─ 옵션 ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions _opt = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // §2 ─ 주입 ───────────────────────────────────────────────

    private readonly DeviceConfigLoader  _loader;
    private readonly DeviceConfigService _saver;
    private readonly DeviceTreeViewModel _treeVm;
    private readonly ScaleLibraryViewModel _scaleVm;
    private readonly AlarmLibraryViewModel _alarmVm;
    private readonly CommLibraryViewModel  _commVm;

    public ConfigImportExportService(
        DeviceConfigLoader    loader,
        DeviceConfigService   saver,
        DeviceTreeViewModel   treeVm,
        ScaleLibraryViewModel scaleVm,
        AlarmLibraryViewModel alarmVm,
        CommLibraryViewModel  commVm)
    {
        _loader  = loader;
        _saver   = saver;
        _treeVm  = treeVm;
        _scaleVm = scaleVm;
        _alarmVm = alarmVm;
        _commVm  = commVm;
    }

    // §3 ─ 열기 ───────────────────────────────────────────────

    /// <summary>
    /// 지정 경로의 device.json을 읽어 ViewModel에 적용.
    /// DeviceConfigLoader.LoadAsync() 를 지정 경로에서 실행.
    /// </summary>
    public async Task<SaveResult> OpenAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return SaveResult.Fail($"파일을 찾을 수 없습니다: {Path.GetFileName(filePath)}");

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var root = JsonSerializer.Deserialize<DeviceConfigRoot>(json, _opt);
            if (root is null)
                return SaveResult.Fail("파일 형식이 올바르지 않습니다.");

            // ViewModel 초기화 후 복원 (기존 데이터 교체)
            _treeVm.RootNodes.Clear();
            _scaleVm.Entries.Clear();
            _alarmVm.Entries.Clear();
            _commVm.Entries.Clear();

            // DeviceConfigLoader의 내부 복원 메서드를 재활용하기 위해 LoadAsync 호출
            // → 임시 파일 복사 후 로드 방식 사용
            var tempPath = Path.Combine(Path.GetTempPath(), $"iiot_open_{Guid.NewGuid()}.json");
            try
            {
                File.Copy(filePath, tempPath, overwrite: true);
                // DeviceConfigService.DeviceJsonPath 를 임시로 교체할 수 없으므로
                // JSON을 직접 파싱하여 복원
                _RestoreFromRoot(root);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            return SaveResult.Ok(filePath);
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"열기 실패: {ex.Message}");
        }
    }

    // §4 ─ 다른 이름으로 저장 ─────────────────────────────────

    /// <summary>
    /// 현재 ViewModel 상태를 지정 경로에 device.json 형식으로 저장.
    /// 기존 DeviceConfigService 의 저장 로직을 재활용하되 경로만 변경.
    /// </summary>
    public async Task<SaveResult> SaveAsAsync(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 1) 기본 경로에 먼저 저장
            var defaultResult = await _saver.SaveAsync();
            if (!defaultResult.IsSuccess) return defaultResult;

            // 2) 기본 저장된 파일을 지정 경로로 복사
            var defaultPath = DeviceConfigService.DeviceJsonPath;
            if (File.Exists(defaultPath))
            {
                File.Copy(defaultPath, filePath, overwrite: true);
                return SaveResult.Ok(filePath);
            }

            return SaveResult.Fail("저장 파일을 찾을 수 없습니다.");
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"저장 실패: {ex.Message}");
        }
    }

    // §5 ─ Tag CSV 내보내기 ───────────────────────────────────

    /// <summary>
    /// 전체 Tag 목록을 CSV로 내보내기.
    /// 컬럼: PLC명, Tag명, 주소, 자료형, 단위, 설명
    /// </summary>
    public async Task<SaveResult> ExportTagsCsvAsync(string filePath)
    {
        try
        {
            var sb = new StringBuilder();
            // 헤더
            sb.AppendLine("PLC명,Tag명,주소,자료형,단위,설명");

            // 트리 재귀 탐색
            _AppendTagsCsv(sb, _treeVm.RootNodes, parentPlcName: string.Empty);

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

            var lineCount = sb.ToString().Split('\n').Length - 2; // 헤더·빈줄 제외
            return SaveResult.Ok($"{lineCount}개 Tag 내보내기 완료 → {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"CSV 내보내기 실패: {ex.Message}");
        }
    }

    // §6 ─ 내부 헬퍼 ──────────────────────────────────────────

    private static void _AppendTagsCsv(
        StringBuilder sb,
        IEnumerable<AbstractTreeNode> nodes,
        string parentPlcName)
    {
        foreach (var node in nodes)
        {
            var plcName = node is PlcTreeNode p ? p.Name : parentPlcName;

            if (node is TagTreeNode tag)
            {
                // CSV 셀 이스케이프 (쉼표·따옴표 포함 시 큰따옴표로 감쌈)
                sb.AppendLine(string.Join(",",
                    _CsvCell(plcName),
                    _CsvCell(tag.Name),
                    _CsvCell(tag.Address),
                    _CsvCell(tag.DataType),
                    _CsvCell(tag.Unit),
                    _CsvCell(tag.Description)));
            }
            else
            {
                _AppendTagsCsv(sb, node.Children, plcName);
            }
        }
    }

    private static string _CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private void _RestoreFromRoot(DeviceConfigRoot root)
    {
        // 스케일
        foreach (var dto in root.ScaleLibrary)
        {
            var entry = new ScaleEntry
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
            };
            _scaleVm.Entries.Add(entry);
        }
        // 알람
        foreach (var dto in root.AlarmLibrary)
        {
            var entry = new AlarmEntry
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
            };
            _alarmVm.Entries.Add(entry);
        }
        // 통신
        foreach (var dto in root.CommLibrary)
        {
            var entry = new CommEntry
            {
                Name     = dto.Name, Description = dto.Description,
                Type     = Enum.TryParse<CommType>(dto.Type, out var ct) ? ct : CommType.ModbusTcp,
                Host     = dto.Host, Port = dto.Port, SlaveId = dto.SlaveId,
                ComPort  = dto.ComPort, BaudRate = dto.BaudRate, Parity = dto.Parity,
                DataBits = dto.DataBits, StopBits = dto.StopBits,
                BrokerHost = dto.BrokerHost, BrokerPort = dto.BrokerPort,
                ClientId = dto.ClientId, Topic = dto.Topic, UseTls = dto.UseTls,
                MqttUser = dto.MqttUser, MqttPassword = dto.MqttPassword,
                EndpointUrl = dto.EndpointUrl, OpcUser = dto.OpcUser, OpcPassword = dto.OpcPassword,
                PollMs = dto.PollMs, TimeoutMs = dto.TimeoutMs, RetryIntervalMs = dto.RetryIntervalMs
            };
            _commVm.Entries.Add(entry);
        }
        // 트리
        foreach (var dto in root.Tree)
        {
            var node = _BuildNode(dto);
            if (node is not null) _treeVm.RootNodes.Add(node);
        }
    }

    private static AbstractTreeNode? _BuildNode(DeviceNodeDto dto)
    {
        AbstractTreeNode? node = dto.NodeType switch
        {
            "Group"  => new GroupTreeNode(dto.Name)  { Description = dto.Description },
            "Device" => new DeviceTreeNode(dto.Name) { Description = dto.Description,
                Model=dto.Model??string.Empty, Manufacturer=dto.Manufacturer??string.Empty,
                Location=dto.Location??string.Empty,
                CommType=Enum.TryParse<NodeCommType>(dto.CommType, out var dct)?dct:NodeCommType.None,
                Host=dto.Host??string.Empty, Port=dto.Port??502, PollMs=dto.PollMs??1000 },
            "PLC"    => new PlcTreeNode(dto.Name)    { Description = dto.Description,
                CommType=Enum.TryParse<NodeCommType>(dto.CommType, out var pct)?pct:NodeCommType.ModbusTcp,
                Host=dto.Host??"192.168.0.1", Port=dto.Port??502, PollMs=dto.PollMs??1000 },
            "Tag"    => new TagTreeNode(dto.Name)    { Description = dto.Description,
                Address=dto.Address??string.Empty, DataType=dto.DataType??"UInt16",
                Unit=dto.Unit??string.Empty },
            _ => null
        };
        if (node is null) return null;
        foreach (var child in dto.Children)
        {
            var c = _BuildNode(child);
            if (c is not null) node.Children.Add(c);
        }
        return node;
    }
}
