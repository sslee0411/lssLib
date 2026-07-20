// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Config/CollectorConfigLoader.cs
//  역할: device.json 로드 → 트리 평탄화 → PlcRuntimeConfig 목록 생성
//        CommTypeMigrator.Resolve() 로 DriverId 확정 (Studio DeviceConfigLoader 동일 패턴)
//        CollectorPluginService.IsKnownDriver() 로 미등록 드라이버 경고
//  C-01: 신규
//  C-05: ScaleLibrary 인덱스 보관 추가 (ScaleEngine.Initialize() 연결용)
//  S-Virtual02: _BuildTagRuntimeConfig 에 UseRoslynScript/ScriptCode 평탄화 추가
//  S-프로토콜01 Step B: ProtocolLibrary 인덱스 추가 + PLC.ProtocolEntryId 해석 →
//               ProtocolBlockSpec 목록 구성 + 블록 필드별 placeholder Tag 합성
//               (FlowEngine 이 IBlockProtocolDriver 로 폴링하고 값을 발행하면
//               DeviceInstance 트리에 일반 Tag 처럼 표시되도록)
//  생성: 2026-06-29 / 수정: 2026-07-20
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Models;
using IIoT.Collector.Core.Plugin;
using IIoT.Contracts;
using IIoT.Contracts.Migration;
using lssLib.Log;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIoT.Collector.Core.Config;

/// <summary>
/// device.json 로드 서비스 (DI 싱글턴).
/// <para>
/// Studio 가 저장한 트리 구조(Group/Device/Plc/Tag)를 읽어
/// FlowEngine 이 바로 사용할 수 있는 평탄화된 <see cref="PlcRuntimeConfig"/> 목록으로 변환한다.
/// 동시에 ScaleLibrary 를 인덱싱하여 C-05 ScaleEngine 이 조회할 수 있도록 보관한다.
/// </para>
/// </summary>
public sealed class CollectorConfigLoader
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorPluginService _pluginService;

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// device.json 기본 경로.
    /// Studio 의 DeviceConfigService.DeviceJsonPath 와 동일 폴더를 가리켜야 함.
    /// (Solution 공통 Config 폴더 — 실행파일 옆 Config/device.json)
    /// </summary>
    public static string DeviceJsonPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "device.json");

    // §2 ─ 결과 보관 ───────────────────────────────────────

    /// <summary>마지막으로 로드된 PLC 런타임 설정 목록 (읽기 전용)</summary>
    public IReadOnlyList<PlcRuntimeConfig> Plcs { get; private set; } = Array.Empty<PlcRuntimeConfig>();

    /// <summary>전체 Tag 수 (Plcs.Sum(p => p.Tags.Count) 캐시)</summary>
    public int TotalTagCount => Plcs.Sum(p => p.Tags.Count);

    /// <summary>
    /// 스케일 라이브러리 — ScaleEntryDto.Id(GUID 문자열) → ScaleEntryDto 인덱스.
    /// C-05 ScaleEngine.Initialize() 가 이 값을 받아 Raw→공학단위 변환에 사용한다.
    /// </summary>
    public IReadOnlyDictionary<string, ScaleEntryDto> ScaleLibrary { get; private set; }
        = new Dictionary<string, ScaleEntryDto>();

    /// <summary>
    /// 알람 라이브러리 — AlarmEntryDto.Id(GUID 문자열) → AlarmEntryDto 인덱스.
    /// C-06 ThresholdDetector 가 Tag.AlarmEntryId 로 조회하는 데 사용.
    /// </summary>
    public IReadOnlyDictionary<string, AlarmEntryDto> AlarmLibrary { get; private set; }
        = new Dictionary<string, AlarmEntryDto>();

    /// <summary>
    /// ★ S-프로토콜01 Step B: 프로토콜 라이브러리 — ProtocolEntryDto.Id(GUID 문자열)
    /// → ProtocolEntryDto 인덱스. PLC/장비의 ProtocolEntryId 로 조회한다.
    /// </summary>
    public IReadOnlyDictionary<string, ProtocolEntryDto> ProtocolLibrary { get; private set; }
        = new Dictionary<string, ProtocolEntryDto>();

    // §3 ─ 생성자 ──────────────────────────────────────────

    public CollectorConfigLoader(CollectorPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    // §4 ─ 로드 진입점 ─────────────────────────────────────

    /// <summary>
    /// device.json 을 로드하여 PLC/Tag 런타임 모델 + ScaleLibrary 인덱스로 변환합니다.
    /// 파일이 없으면 빈 목록으로 초기화하고 경고 로그만 남깁니다 (예외 없음).
    /// </summary>
    /// <param name="path">device.json 경로 (null = 기본 경로)</param>
    public async Task LoadAsync(string? path = null)
    {
        var filePath = path ?? DeviceJsonPath;

        if (!File.Exists(filePath))
        {
            LogManager.Instance.Warn("ConfigLoader",
                $"device.json 없음: {filePath} — Studio 에서 먼저 저장해 주세요.");
            Plcs = Array.Empty<PlcRuntimeConfig>();
            ScaleLibrary = new Dictionary<string, ScaleEntryDto>();
            ProtocolLibrary = new Dictionary<string, ProtocolEntryDto>();
            return;
        }

        DeviceConfigRoot? root;
        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            root = JsonSerializer.Deserialize<DeviceConfigRoot>(json, _opts);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Error("ConfigLoader",
                $"device.json 파싱 실패: {ex.Message}");
            Plcs = Array.Empty<PlcRuntimeConfig>();
            ScaleLibrary = new Dictionary<string, ScaleEntryDto>();
            ProtocolLibrary = new Dictionary<string, ProtocolEntryDto>();
            return;
        }

        if (root is null)
        {
            LogManager.Instance.Warn("ConfigLoader", "device.json 내용이 비어 있음");
            Plcs = Array.Empty<PlcRuntimeConfig>();
            ScaleLibrary = new Dictionary<string, ScaleEntryDto>();
            ProtocolLibrary = new Dictionary<string, ProtocolEntryDto>();
            return;
        }

        // ★ S-프로토콜01 Step B: ProtocolLibrary 인덱스 먼저 구성
        //   (_BuildPlcRuntimeConfig 가 PLC.ProtocolEntryId 조회 시 사용)
        ProtocolLibrary = root.ProtocolLibrary
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToDictionary(p => p.Id, p => p);

        var plcs = new List<PlcRuntimeConfig>();
        foreach (var node in root.Tree)
            _CollectPlcNodes(node, plcs);

        Plcs = plcs;

        // ★ C-05: ScaleLibrary 인덱스 구성 (Id 가 비어있는 항목은 제외)
        ScaleLibrary = root.ScaleLibrary
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .ToDictionary(s => s.Id, s => s);

        // ★ C-06: AlarmLibrary 인덱스 구성
        AlarmLibrary = root.AlarmLibrary
            .Where(a => !string.IsNullOrWhiteSpace(a.Id))
            .ToDictionary(a => a.Id, a => a);

        var protoBlockCount = plcs.Sum(p => p.ProtocolBlocks.Count);
        LogManager.Instance.Info("ConfigLoader",
            $"device.json 로드 완료 — {plcs.Count}개 PLC/Device, {TotalTagCount}개 Tag, " +
            $"{ScaleLibrary.Count}개 스케일, {AlarmLibrary.Count}개 알람, " +
            $"{ProtocolLibrary.Count}개 프로토콜({protoBlockCount}개 블록 연결됨)");

        _WarnUnknownDrivers(plcs);
    }

    // §5 ─ 트리 순회 — PLC/Device 노드 수집 ──────────────────

    /// <summary>
    /// 트리를 재귀 순회하며 PLC/Device 노드를 찾아 평탄화합니다.
    /// Group 노드는 건너뛰고 하위로 계속 내려감.
    /// </summary>
    private void _CollectPlcNodes(DeviceNodeDto node, List<PlcRuntimeConfig> result)
    {
        switch (node.NodeType)
        {
            case "PLC":
            case "Device":
                result.Add(_BuildPlcRuntimeConfig(node));
                break;

            case "Group":
                foreach (var child in node.Children)
                    _CollectPlcNodes(child, result);
                break;

            // Tag 노드가 트리 최상위에 단독으로 오는 구조는 없음 (PLC/Device 하위에만 존재)
            default:
                break;
        }
    }

    // §6 ─ PLC/Device 노드 → PlcRuntimeConfig 변환 ───────────

    private PlcRuntimeConfig _BuildPlcRuntimeConfig(DeviceNodeDto dto)
    {
        // ★ Studio DeviceConfigLoader 와 동일 패턴:
        //   driverId 있으면 그대로, commType 만 있으면 변환
        //   "ModbusTcp" → "modbus-tcp" / "mitsubishi-mc" → 그대로
        var driverId = CommTypeMigrator.Resolve(dto.DriverId, dto.CommType) ?? string.Empty;

        var driverParams = new Dictionary<string, string>();
        if (dto.DriverParams is not null)
            foreach (var kv in dto.DriverParams)
                driverParams[kv.Key] = kv.Value;

        var plc = new PlcRuntimeConfig
        {
            PlcId        = dto.Id,
            Name         = dto.Name,
            NodeType     = dto.NodeType,
            DriverId     = driverId,
            DriverParams = driverParams,
            PollMs       = dto.PollMs ?? 1000,
            TimeoutMs    = 3000   // C-01 단계 고정값 — C-02 이후 dto 확장 시 반영
        };

        foreach (var child in dto.Children)
            if (child.NodeType == "Tag")
                plc.Tags.Add(_BuildTagRuntimeConfig(child, plc.PlcId));

        // ★ S-프로토콜01 Step B: 연결된 프로토콜 라이브러리의 읽기 블록을
        //   ProtocolBlockSpec 목록으로 구성 + 필드별 placeholder Tag 합성
        if (!string.IsNullOrWhiteSpace(dto.ProtocolEntryId) &&
            ProtocolLibrary.TryGetValue(dto.ProtocolEntryId, out var protocolEntry))
        {
            foreach (var blockDto in protocolEntry.ReadBlocks)
            {
                var blockSpec = _BuildProtocolBlockSpec(blockDto, protocolEntry);
                plc.ProtocolBlocks.Add(blockSpec);

                foreach (var field in blockSpec.Fields)
                {
                    plc.Tags.Add(new TagRuntimeConfig
                    {
                        Id                   = ProtocolFieldTagId.Make(plc.PlcId, blockSpec.Id, field.Id),
                        Name                 = $"{blockSpec.Name}.{field.Name}",
                        Address              = string.Empty,   // 실주소 아님 — 블록 폴링 경로에서만 값 채움
                        DataType             = field.BufType,
                        Unit                 = field.Unit,
                        // ★ S-프로토콜01 Step B 후속: 필드에 연결된 스케일 라이브러리를 그대로
                        //   전달 — FlowEngine._PollProtocolBlocksAsync 가 일반 Tag 와 동일하게
                        //   ScaleEngine.Apply(tag, raw) 로 변환한다(라이브러리 참조 없으면 Raw 그대로).
                        ScaleEntryId         = field.ScaleEntryId,
                        IsEnabled            = true,
                        ParentPlcId          = plc.PlcId,
                        IsProtocolBlockField = true
                    });
                }
            }
        }

        return plc;
    }

    // §6-1 ─ ★ S-프로토콜01 Step B: ProtocolBlockDto → ProtocolBlockSpec ──

    /// <summary>
    /// device.json ProtocolBlockDto 를 Contracts.ProtocolBlockSpec 으로 변환한다.
    /// ProtocolEntry 레벨 프레이밍 설정(UseFraming/StxHex/HasLengthField/CrcType)을
    /// 블록 단위로 평탄화하여 담는다(드라이버가 매번 상위 Entry 를 조회하지 않도록).
    /// </summary>
    private static ProtocolBlockSpec _BuildProtocolBlockSpec(
        ProtocolBlockDto blockDto, ProtocolEntryDto entry)
    {
        var fields = blockDto.Fields
            .Select(f => new ProtocolFieldSpec(
                Id:           f.Id,
                Name:         f.Name,
                ByteOffset:   f.ByteOffset,
                BufType:      f.BufType,
                Unit:         f.Unit,
                ScaleEntryId: f.ScaleEntryId))
            .ToList();

        return new ProtocolBlockSpec(
            Id:             blockDto.Id,
            Name:           blockDto.Name,
            StartAddress:   blockDto.StartAddress,
            Length:         blockDto.Length,
            CmdCode:        blockDto.CmdCode,
            Fields:         fields,
            UseFraming:     entry.UseFraming,
            StxHex:         entry.StxHex,
            HasLengthField: entry.HasLengthField,
            CrcType:        entry.CrcType);
    }

    // §7 ─ Tag 노드 → TagRuntimeConfig 변환 ──────────────────

    private static TagRuntimeConfig _BuildTagRuntimeConfig(DeviceNodeDto dto, string parentPlcId)
        => new()
        {
            Id           = dto.Id,
            Name         = dto.Name,
            Address      = dto.Address  ?? string.Empty,
            DataType     = dto.DataType ?? "UInt16",
            Unit         = dto.Unit     ?? string.Empty,
            ScaleEntryId = dto.ScaleEntryId,
            AlarmEntryId = dto.AlarmEntryId,
            Memo         = dto.Memo ?? string.Empty,
            IsEnabled    = dto.IsEnabled ?? true,
            ParentPlcId  = parentPlcId,
            // ★ C-18 신규
            IsVirtual = dto.IsVirtual ?? false,
            Expression = dto.Expression,
            // ★ S-Virtual02 신규
            UseRoslynScript = dto.UseRoslynScript ?? false,
            ScriptCode = dto.ScriptCode
        };

    // §8 ─ 미등록 드라이버 경고 ───────────────────────────────

    /// <summary>
    /// device.json 이 참조하는 driverId 중 현재 로드된 플러그인에
    /// 없는 항목을 찾아 경고 로그를 남깁니다 (수집은 계속 진행, 해당 PLC만 제외 예정 — C-03).
    /// </summary>
    private void _WarnUnknownDrivers(List<PlcRuntimeConfig> plcs)
    {
        foreach (var plc in plcs)
        {
            if (string.IsNullOrWhiteSpace(plc.DriverId))
            {
                LogManager.Instance.Warn("ConfigLoader",
                    $"[{plc.Name}] DriverId 가 비어 있음 — CommType 변환 실패 가능성");
                continue;
            }

            if (!_pluginService.IsKnownDriver(plc.DriverId))
            {
                LogManager.Instance.Warn("ConfigLoader",
                    $"[{plc.Name}] 드라이버 미등록: \"{plc.DriverId}\" " +
                    "— Plugins/ 폴더에 해당 dll 이 있는지 확인하세요.");
            }
        }
    }
}
