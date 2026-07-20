// ══════════════════════════════════════════════════════════
//  IIoT.Collector · ViewModels/SettingsViewModel.cs
//  역할: 환경설정(Settings) 탭 ViewModel — settings.json 12개 섹션 편집
//        (Storage/SignalR/Retry/Notification/ForceWrite/Filter/
//         VirtualTag/Security/Retention/Backup/CollectorId)
//  C-SET-01: 신규
//        ★ 대부분의 필드는 CollectorSettings 하위 모델(Settings.Storage.X 등)에
//          직접 TwoWay 바인딩한다 — 편집 중 실시간 반영을 요구하는 항목이 아니라
//          [저장] 시점에만 값을 읽으면 되므로 모델에 INotifyPropertyChanged 가
//          없어도 문제없다 (프로젝트 규칙: 판단 후 최소 구현).
//        ★ 예외적으로 화면 표시 형태 변환이 필요한 필드만 래퍼 프로퍼티로 노출:
//          - Storage.Provider(string) → IsInfluxDbProvider(bool) : 섹션 표시 전환용
//          - Retry.IntervalsSec(int[]) → RetryIntervalsSecText(string, "5,15,30,60")
//          - SignalR.AllowedOrigins(string[]) → AllowedOriginsText(string, 줄바꿈 구분)
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Collector.Core.Config;
using lssLib.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IIoT.Collector.ViewModels;

/// <summary>
/// 환경설정 화면 ViewModel (DI 싱글턴).
/// App.xaml.cs win.Loaded 에서 CollectorSettingsLoader.LoadAsync() 완료 후
/// Initialize() 를 호출해야 로드된 값이 화면에 반영된다.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly CollectorSettingsLoader _loader;

    // §2 ─ 좌측 섹션 네비게이션 ───────────────────────────────

    /// <summary>
    /// 현재 선택된 섹션 인덱스.
    /// 0=일반 1=저장소 2=SignalR 3=재연결 4=알림 5=강제쓰기
    /// 6=이상값필터 7=가상Tag 8=보안 9=데이터보존 10=DB백업
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralSection))]
    [NotifyPropertyChangedFor(nameof(IsStorageSection))]
    [NotifyPropertyChangedFor(nameof(IsSignalRSection))]
    [NotifyPropertyChangedFor(nameof(IsRetrySection))]
    [NotifyPropertyChangedFor(nameof(IsNotificationSection))]
    [NotifyPropertyChangedFor(nameof(IsForceWriteSection))]
    [NotifyPropertyChangedFor(nameof(IsFilterSection))]
    [NotifyPropertyChangedFor(nameof(IsVirtualTagSection))]
    [NotifyPropertyChangedFor(nameof(IsSecuritySection))]
    [NotifyPropertyChangedFor(nameof(IsRetentionSection))]
    [NotifyPropertyChangedFor(nameof(IsBackupSection))]
    private int _activeSectionIndex;

    public bool IsGeneralSection      => ActiveSectionIndex == 0;
    public bool IsStorageSection      => ActiveSectionIndex == 1;
    public bool IsSignalRSection      => ActiveSectionIndex == 2;
    public bool IsRetrySection        => ActiveSectionIndex == 3;
    public bool IsNotificationSection => ActiveSectionIndex == 4;
    public bool IsForceWriteSection   => ActiveSectionIndex == 5;
    public bool IsFilterSection       => ActiveSectionIndex == 6;
    public bool IsVirtualTagSection   => ActiveSectionIndex == 7;
    public bool IsSecuritySection     => ActiveSectionIndex == 8;
    public bool IsRetentionSection    => ActiveSectionIndex == 9;
    public bool IsBackupSection       => ActiveSectionIndex == 10;

    [RelayCommand]
    private void SwitchSection(string idx)
    {
        if (int.TryParse(idx, out var i)) ActiveSectionIndex = i;
    }

    // §3 ─ 설정 모델 ──────────────────────────────────────────

    /// <summary>
    /// 편집 대상 설정 객체 (CollectorSettingsLoader.Settings 와 동일 참조).
    /// 하위 필드(Settings.Storage.Provider 등)에 화면이 직접 TwoWay 바인딩된다.
    /// </summary>
    [ObservableProperty]
    private CollectorSettings _settings = new();

    /// <summary>Storage.Provider == "InfluxDB" 여부 (섹션 표시 전환용 래퍼)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSqliteProvider))]
    private bool _isInfluxDbProvider;

    public bool IsSqliteProvider => !IsInfluxDbProvider;

    partial void OnIsInfluxDbProviderChanged(bool value)
        => Settings.Storage.Provider = value ? "InfluxDB" : "SQLite";

    /// <summary>Retry.IntervalsSec 콤마 구분 텍스트 래퍼 (예: "5,15,30,60")</summary>
    public string RetryIntervalsSecText
    {
        get => string.Join(",", Settings.Retry.IntervalsSec);
        set
        {
            var parsed = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                               .Where(n => n.HasValue && n.Value > 0)
                               .Select(n => n!.Value)
                               .ToArray();
            Settings.Retry.IntervalsSec = parsed.Length > 0 ? parsed : [5, 15, 30, 60];
            OnPropertyChanged();
        }
    }

    /// <summary>SignalR.AllowedOrigins 줄바꿈 구분 텍스트 래퍼</summary>
    public string AllowedOriginsText
    {
        get => string.Join(Environment.NewLine, Settings.SignalR.AllowedOrigins);
        set
        {
            Settings.SignalR.AllowedOrigins =
                value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            OnPropertyChanged();
        }
    }

    // §4 ─ 저장 상태 ──────────────────────────────────────────

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    // §5 ─ 초기화 ─────────────────────────────────────────────

    public SettingsViewModel(CollectorSettingsLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// App.xaml.cs 에서 CollectorSettingsLoader.LoadAsync() 완료 직후 호출.
    /// 로드된 Settings 참조를 화면에 반영한다.
    /// </summary>
    public void Initialize()
    {
        Settings = _loader.Settings;
        IsInfluxDbProvider = Settings.Storage.Provider.Equals("InfluxDB", StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(RetryIntervalsSecText));
        OnPropertyChanged(nameof(AllowedOriginsText));
        StatusMessage = string.Empty;
        HasError = false;
    }

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>
    /// 저장 전 유효성 검사 후 settings.json 에 저장한다.
    /// 오류가 있으면 저장을 중단하고 StatusMessage 에 오류 목록을 표시한다.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var errors = _ValidateAll();
        if (errors.Count > 0)
        {
            HasError = true;
            StatusMessage = "저장 실패 — " + string.Join(" / ", errors);
            return;
        }

        await _loader.SaveAsync();
        HasError = false;
        StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — 대부분의 설정은 프로그램을 재시작해야 적용됩니다.";
    }

    /// <summary>디스크의 settings.json 을 다시 읽어 편집 중인 내용을 되돌린다.</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        await _loader.LoadAsync();
        Initialize();
        StatusMessage = "settings.json 을 다시 불러왔습니다 (편집 중이던 내용은 취소됨).";
    }

    /// <summary>새 CollectorId 를 발급해 화면에 반영한다 (저장은 [저장] 버튼으로 별도 수행).</summary>
    [RelayCommand]
    private void RegenerateCollectorId()
    {
        Settings.CollectorId = CollectorSettingsLoader.GenerateNewCollectorId();
        OnPropertyChanged(nameof(Settings));
        StatusMessage = $"새 CollectorId 발급됨: {Settings.CollectorId} (아직 저장 전 — [저장] 눌러야 반영됨)";
    }

    // §7 ─ 유효성 검사 ────────────────────────────────────────

    private static readonly Regex _TimeRegex = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

    private List<string> _ValidateAll()
    {
        var errors = new List<string>();
        var s = Settings;

        // Storage
        if (s.Storage.StatIntervalSec <= 0)
            errors.Add("수집 통계 저장 주기는 1초 이상이어야 합니다");
        if (s.Storage.SdtExcDevPercent < 0)
            errors.Add("SDT 허용 오차 비율은 0 이상이어야 합니다");
        if (IsSqliteProvider && string.IsNullOrWhiteSpace(s.Storage.SQLite.DbPath))
            errors.Add("SQLite DB 경로를 입력하세요");
        if (IsInfluxDbProvider)
        {
            if (string.IsNullOrWhiteSpace(s.Storage.InfluxDB.Url)) errors.Add("InfluxDB URL을 입력하세요");
            if (string.IsNullOrWhiteSpace(s.Storage.InfluxDB.Org)) errors.Add("InfluxDB Org를 입력하세요");
            if (string.IsNullOrWhiteSpace(s.Storage.InfluxDB.Bucket)) errors.Add("InfluxDB Bucket을 입력하세요");
            if (s.Storage.InfluxDB.BatchSize <= 0) errors.Add("InfluxDB BatchSize는 1 이상이어야 합니다");
            if (s.Storage.InfluxDB.FlushIntervalMs <= 0) errors.Add("InfluxDB FlushIntervalMs는 1 이상이어야 합니다");
        }
        if (s.Storage.Mqtt.Enabled && (s.Storage.Mqtt.BrokerPort is < 1 or > 65535))
            errors.Add("MQTT 브로커 포트는 1~65535 범위여야 합니다");

        // SignalR
        if (s.SignalR.Port is < 1 or > 65535)
            errors.Add("SignalR 포트는 1~65535 범위여야 합니다");

        // Retry
        if (s.Retry.IntervalsSec.Length == 0 || s.Retry.IntervalsSec.Any(v => v <= 0))
            errors.Add("재연결 간격은 1개 이상, 모두 1초 이상이어야 합니다");
        if (s.Retry.MaxRetries < 0)
            errors.Add("최대 재시도 횟수는 0 이상이어야 합니다 (0=무제한)");

        // Notification
        if (s.Notification.Enabled)
        {
            if (string.IsNullOrWhiteSpace(s.Notification.Smtp.Host)) errors.Add("SMTP 호스트를 입력하세요");
            if (s.Notification.Smtp.Port is < 1 or > 65535) errors.Add("SMTP 포트는 1~65535 범위여야 합니다");
            if (string.IsNullOrWhiteSpace(s.Notification.Smtp.FromAddress)) errors.Add("SMTP 발신 주소를 입력하세요");
            if (s.Notification.Webhook.Enabled && string.IsNullOrWhiteSpace(s.Notification.Webhook.Url))
                errors.Add("Webhook URL을 입력하세요");
        }

        // Filter
        if (s.Filter.SpikeFilterEnabled && s.Filter.SpikeMaxDeltaPercent < 0)
            errors.Add("스파이크 임계값은 0 이상이어야 합니다");
        if (s.Filter.DeadbandEnabled && s.Filter.DeadbandPercent < 0)
            errors.Add("데드밴드 임계값은 0 이상이어야 합니다");

        // VirtualTag
        if (s.VirtualTag.Enabled && s.VirtualTag.IntervalMs <= 0)
            errors.Add("가상 Tag 계산 주기는 1ms 이상이어야 합니다");

        // Retention
        if (s.Retention.Enabled)
        {
            if (s.Retention.RetentionDays <= 0) errors.Add("보존 일수는 1일 이상이어야 합니다");
            if (!_TimeRegex.IsMatch(s.Retention.RunAtTime)) errors.Add("데이터 보존 실행 시각 형식은 HH:mm 입니다");
        }

        // Backup
        if (s.Backup.Enabled)
        {
            if (s.Backup.MaxBackupCount < 0) errors.Add("최대 백업 개수는 0 이상이어야 합니다");
            if (!_TimeRegex.IsMatch(s.Backup.RunAtTime)) errors.Add("DB 백업 실행 시각 형식은 HH:mm 입니다");
            if (string.IsNullOrWhiteSpace(s.Backup.BackupDir)) errors.Add("백업 폴더 경로를 입력하세요");
        }

        return errors;
    }
}
