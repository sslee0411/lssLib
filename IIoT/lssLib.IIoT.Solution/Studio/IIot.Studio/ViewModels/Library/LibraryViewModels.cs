// ══════════════════════════════════════════════════════════
//  IIoT.Studio · ViewModels/Library/LibraryViewModels.cs
//  역할: 스케일·알람·통신 라이브러리 ViewModel 3종
//        StudioMainViewModel / App.xaml.cs / AlarmLibraryView / CommLibraryView 에서 참조
//  생성: 2026-06-14
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using System.Collections.ObjectModel;

namespace IIoT.Studio.ViewModels.Library;

// ══════════════════════════════════════════════════════════
//  §1 — 스케일 라이브러리 ViewModel
// ══════════════════════════════════════════════════════════

/// <summary>스케일 규칙 단일 항목</summary>
public sealed partial class ScaleRuleViewModel : ObservableObject
{
    [ObservableProperty] private string _id          = Guid.NewGuid().ToString("N")[..8];
    [ObservableProperty] private string _name        = string.Empty;
    [ObservableProperty] private double _rawMin      = 0;
    [ObservableProperty] private double _rawMax      = 4095;
    [ObservableProperty] private double _engMin      = 0;
    [ObservableProperty] private double _engMax      = 100;
    [ObservableProperty] private string _unit        = string.Empty;
    [ObservableProperty] private int    _decimalPlaces = 2;

    public string RangeText => $"{RawMin}~{RawMax} → {EngMin}~{EngMax}";
}

/// <summary>스케일 라이브러리 관리 ViewModel</summary>
public sealed partial class ScaleLibraryViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly JsonWriteService _writer;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────
    public ObservableCollection<ScaleRuleViewModel> Scales { get; } = [];

    // §3 ─ 선택 상태 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(IsScaleSelected))]
    private ScaleRuleViewModel? _selectedScale;

    public bool IsNoneSelected  => SelectedScale is null;
    public bool IsScaleSelected => SelectedScale is not null;

    // §4 ─ 드롭다운 옵션 ──────────────────────────────────────
    public IReadOnlyList<int> DecimalPlaceOptions { get; } = [0, 1, 2, 3, 4];

    // §5 ─ 생성자 ─────────────────────────────────────────────
    public ScaleLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
    }

    // §6 ─ 로드 (SwitchTab에서 호출) ──────────────────────────
    public void Load(object? data)
    {
        // device.json / scale-library.json 로딩 시 여기서 파싱
        // 현재는 빈 구현 (추후 JsonConfigLoader 연동)
    }

    // §7 ─ 커맨드 ─────────────────────────────────────────────
    [RelayCommand]
    private void AddScale()
    {
        var rule = new ScaleRuleViewModel { Name = $"스케일 {Scales.Count + 1}" };
        Scales.Add(rule);
        SelectedScale = rule;
    }

    [RelayCommand]
    private void DeleteScale(ScaleRuleViewModel? rule)
    {
        if (rule is null) return;
        Scales.Remove(rule);
        if (SelectedScale == rule) SelectedScale = null;
    }

    [RelayCommand]
    private void SaveScale()
    {
        if (SelectedScale is null) return;
        // _writer.SaveScaleLibrary(Scales) — 추후 구현
    }
}

// ══════════════════════════════════════════════════════════
//  §2 — 알람 라이브러리 ViewModel
// ══════════════════════════════════════════════════════════

/// <summary>알람 규칙 단일 항목 (HH/H/L/LL 4단계)</summary>
public sealed partial class AlarmRuleViewModel : ObservableObject
{
    [ObservableProperty] private string  _id      = Guid.NewGuid().ToString("N")[..8];
    [ObservableProperty] private string  _name    = string.Empty;

    // HH 상상한
    [ObservableProperty] private double? _hiHi;
    [ObservableProperty] private string  _hiHiMsg = string.Empty;
    // H 상한
    [ObservableProperty] private double? _hi;
    [ObservableProperty] private string  _hiMsg   = string.Empty;
    // L 하한
    [ObservableProperty] private double? _lo;
    [ObservableProperty] private string  _loMsg   = string.Empty;
    // LL 하하한
    [ObservableProperty] private double? _loLo;
    [ObservableProperty] private string  _loLoMsg = string.Empty;

    // 목록 표시용 뱃지 텍스트
    public string HiHiText => HiHi.HasValue ? $"HH:{HiHi:F1}" : "HH:—";
    public string HiText   => Hi.HasValue   ? $"H:{Hi:F1}"     : "H:—";
    public string LoText   => Lo.HasValue   ? $"L:{Lo:F1}"     : "L:—";
    public string LoLoText => LoLo.HasValue ? $"LL:{LoLo:F1}"  : "LL:—";
}

/// <summary>알람 라이브러리 관리 ViewModel</summary>
public sealed partial class AlarmLibraryViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly JsonWriteService _writer;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────
    public ObservableCollection<AlarmRuleViewModel> AlarmRules { get; } = [];

    // §3 ─ 선택 상태 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(IsRuleSelected))]
    private AlarmRuleViewModel? _selectedRule;

    public bool IsNoneSelected => SelectedRule is null;
    public bool IsRuleSelected => SelectedRule is not null;

    // §4 ─ 생성자 ─────────────────────────────────────────────
    public AlarmLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
    }

    // §5 ─ 로드 ───────────────────────────────────────────────
    public void Load(object? data) { /* 추후 JsonConfigLoader 연동 */ }

    // §6 ─ 커맨드 ─────────────────────────────────────────────
    [RelayCommand]
    private void AddAlarm()
    {
        var rule = new AlarmRuleViewModel { Name = $"알람 규칙 {AlarmRules.Count + 1}" };
        AlarmRules.Add(rule);
        SelectedRule = rule;
    }

    [RelayCommand]
    private void DeleteAlarm(AlarmRuleViewModel? rule)
    {
        if (rule is null) return;
        AlarmRules.Remove(rule);
        if (SelectedRule == rule) SelectedRule = null;
    }

    [RelayCommand]
    private void SaveAlarm()
    {
        if (SelectedRule is null) return;
        // _writer.SaveAlarmLibrary(AlarmRules) — 추후 구현
    }
}

// ══════════════════════════════════════════════════════════
//  §3 — 통신 라이브러리 ViewModel
// ══════════════════════════════════════════════════════════

/// <summary>통신 설정 단일 항목</summary>
public sealed partial class CommConfigViewModel : ObservableObject
{
    [ObservableProperty] private string _id              = Guid.NewGuid().ToString("N")[..8];
    [ObservableProperty] private string _name            = string.Empty;
    [ObservableProperty] private string _protocol        = "ModbusTcp";

    // Modbus TCP / MQTT 공통 (Host/Port)
    [ObservableProperty] private string _host            = string.Empty;
    [ObservableProperty] private int    _port            = 502;

    // Modbus 전용
    [ObservableProperty] private int    _unitId          = 1;
    [ObservableProperty] private int    _timeoutMs       = 3000;

    // Serial 전용
    [ObservableProperty] private string _portName        = "COM1";
    [ObservableProperty] private int    _baudRate        = 9600;

    // MQTT 전용
    [ObservableProperty] private string _clientId        = "iiot-collector";

    // 재시도 (공통)
    [ObservableProperty] private int    _retryCount      = 3;
    [ObservableProperty] private int    _retryIntervalMs = 1000;

    // 목록 표시용
    public string HostPortText => Protocol switch
    {
        "Serial" => $"{PortName} @ {BaudRate}bps",
        _        => $"{Host}:{Port}",
    };
}

/// <summary>통신 라이브러리 관리 ViewModel</summary>
public sealed partial class CommLibraryViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────
    private readonly JsonWriteService _writer;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────
    public ObservableCollection<CommConfigViewModel> CommConfigs { get; } = [];

    // §3 ─ 선택 상태 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected))]
    [NotifyPropertyChangedFor(nameof(IsConfigSelected))]
    [NotifyPropertyChangedFor(nameof(IsModbusTcp))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsMqtt))]
    private CommConfigViewModel? _selectedConfig;

    public bool IsNoneSelected   => SelectedConfig is null;
    public bool IsConfigSelected => SelectedConfig is not null;
    public bool IsModbusTcp      => SelectedConfig?.Protocol == "ModbusTcp";
    public bool IsSerial         => SelectedConfig?.Protocol == "Serial";
    public bool IsMqtt           => SelectedConfig?.Protocol == "Mqtt";

    // §4 ─ 드롭다운 옵션 ──────────────────────────────────────
    public IReadOnlyList<string> ProtocolOptions { get; } =
        ["ModbusTcp", "ModbusRtu", "Serial", "OpcUa", "Mqtt"];

    public IReadOnlyList<int> BaudRateOptions { get; } =
        [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    // §5 ─ 생성자 ─────────────────────────────────────────────
    public CommLibraryViewModel(JsonWriteService writer)
    {
        _writer = writer;
    }

    // §6 ─ 로드 ───────────────────────────────────────────────
    public void Load(object? data) { /* 추후 JsonConfigLoader 연동 */ }

    // §7 ─ 커맨드 ─────────────────────────────────────────────
    [RelayCommand]
    private void AddComm()
    {
        var cfg = new CommConfigViewModel
        {
            Name = $"통신 설정 {CommConfigs.Count + 1}",
            Protocol = "ModbusTcp",
        };
        CommConfigs.Add(cfg);
        SelectedConfig = cfg;
    }

    [RelayCommand]
    private void DeleteComm(CommConfigViewModel? cfg)
    {
        if (cfg is null) return;
        CommConfigs.Remove(cfg);
        if (SelectedConfig == cfg) SelectedConfig = null;
    }

    [RelayCommand]
    private void SaveComm()
    {
        if (SelectedConfig is null) return;
        // _writer.SaveCommLibrary(CommConfigs) — 추후 구현
    }
}
