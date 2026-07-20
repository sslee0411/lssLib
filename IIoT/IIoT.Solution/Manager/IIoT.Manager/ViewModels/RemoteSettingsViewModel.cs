// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/RemoteSettingsViewModel.cs
//  역할: [원격 설정] 탭 ViewModel — Studio/Collector/Monitor/HMI 의
//        settings.json 원문을 NamedPipe 헬스체크 채널(HM-22 확장)로 원격
//        조회·저장한다. Manager 자신의 설정은 대상이 아니다(이미 [환경설정]
//        탭에서 로컬로 편집 가능).
//  HM-22: 신규 — "기존 NamedPipe 헬스체크 채널 확장" 방식으로 착수(사용자 확인,
//        2026-07-20). 대상 프로그램이 실행 중이어야 조회·저장이 동작한다
//        (연결 자체가 프로세스 생존의 1차 확인 수단이기도 하다).
//        ★ 조회한 JSON 은 대상 프로그램의 실제 settings.json 원문 그대로이므로,
//          그 프로그램의 로컬 [환경설정] 탭이 다루는 필드 전부(그리고 UI 가 아직
//          없는 필드까지) 자유롭게 수정할 수 있다 — 대신 스키마 검증은 "문법이
//          올바른 JSON인가"까지만 하고, 필드별 유효성 검사는 하지 않는다
//          (그 책임은 각 프로그램 로컬 탭의 SaveCommand 가 이미 담당).
//        ★ 저장은 파일만 갱신 — 대상 프로그램은 재시작해야 반영된다(다른
//          프로그램의 로컬 환경설정 탭과 동일한 "재시작 필요" 원칙 유지).
//  생성: 2026-07-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace IIoT.Manager.ViewModels;

/// <summary>원격 설정 조회/저장 대상 프로그램 1개 항목(콤보박스 표시용).</summary>
public sealed record RemoteTargetOption(string ProcessName, string DisplayName);

/// <summary>[원격 설정] 화면 ViewModel (DI 싱글턴).</summary>
public partial class RemoteSettingsViewModel : ObservableObject
{
    private readonly HealthCheckService _healthCheck;

    /// <summary>원격 설정 대상 — 로컬 [환경설정] 탭이 이미 있는 4개 프로그램 고정 목록.
    /// Manager 자신은 제외(로컬 [환경설정] 탭에서 편집).</summary>
    public ObservableCollection<RemoteTargetOption> Targets { get; } =
    [
        new("IIoT.Studio",    "🖥 Studio (설정 편집기)"),
        new("IIoT.Collector", "📥 Collector (수집·감지·저장)"),
        new("IIoT.Monitor",   "📊 Monitor (실시간 모니터링)"),
        new("IIoT.HMI",       "🗂 HMI (생산현황판)"),
    ];

    /// <summary>★ 규칙(S-17A): CanExecute 트리거 프로퍼티에는 반드시
    /// [NotifyCanExecuteChangedFor] 를 붙여야 버튼 활성 상태가 갱신된다.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private RemoteTargetOption? _selectedTarget;

    /// <summary>편집 중인 JSON 원문(다중 줄 TextBox 바인딩).</summary>
    [ObservableProperty]
    private string _jsonText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "대상 프로그램을 선택한 뒤 [↻ 조회]를 누르세요.";

    [ObservableProperty]
    private bool _hasError;

    /// <summary>조회/저장 진행 중 — 버튼 중복 클릭 방지용</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    /// <summary>대상이 선택돼 있고 진행 중이 아닐 때만 조회/저장 가능.</summary>
    private bool _CanLoadOrSave() => SelectedTarget is not null && !IsBusy;

    public RemoteSettingsViewModel(HealthCheckService healthCheck)
    {
        _healthCheck = healthCheck;
    }

    /// <summary>대상 프로그램 변경 시 이전 내용을 비워 혼동을 방지(자동 조회는 하지 않음 —
    /// 실행 중이 아닌 프로그램을 선택만 해도 불필요한 연결 시도가 발생하지 않도록).</summary>
    partial void OnSelectedTargetChanged(RemoteTargetOption? value)
    {
        JsonText      = string.Empty;
        HasError      = false;
        StatusMessage = value is null
            ? "대상 프로그램을 선택한 뒤 [↻ 조회]를 누르세요."
            : $"{value.DisplayName} 선택됨 — [↻ 조회]를 눌러 현재 설정을 불러오세요.";
    }

    [RelayCommand(CanExecute = nameof(_CanLoadOrSave))]
    private async Task LoadAsync()
    {
        if (SelectedTarget is null || IsBusy) return;

        IsBusy        = true;
        StatusMessage = $"{SelectedTarget.DisplayName} 조회 중...";
        HasError      = false;
        try
        {
            var result = await _healthCheck.GetSettingsAsync(SelectedTarget.ProcessName);
            if (!result.Ok)
            {
                HasError      = true;
                StatusMessage = $"조회 실패 — {result.Error}";
                return;
            }

            // ★ 사람이 읽기 좋도록 재포맷(원본이 압축 저장돼 있어도 들여쓰기로 표시)
            JsonText = _PrettyPrint(result.Json);
            HasError = false;
            StatusMessage = $"조회 완료 ({DateTime.Now:HH:mm:ss}) — {SelectedTarget.DisplayName}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(_CanLoadOrSave))]
    private async Task SaveAsync()
    {
        if (SelectedTarget is null || IsBusy) return;

        // ★ 전송 전 클라이언트 측 1차 검증 — 문법이 깨진 JSON 을 굳이 파이프로
        //   보내지 않고 여기서 바로 알려준다(서버 측도 동일 검증을 한 번 더 함).
        try
        {
            using var _ = JsonDocument.Parse(JsonText);
        }
        catch (JsonException ex)
        {
            HasError      = true;
            StatusMessage = $"저장 실패 — JSON 문법 오류: {ex.Message}";
            return;
        }

        IsBusy        = true;
        StatusMessage = $"{SelectedTarget.DisplayName} 저장 중...";
        HasError      = false;
        try
        {
            var result = await _healthCheck.SaveSettingsAsync(SelectedTarget.ProcessName, JsonText);
            if (!result.Ok)
            {
                HasError      = true;
                StatusMessage = $"저장 실패 — {result.Error}";
                return;
            }

            HasError      = false;
            StatusMessage = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — {SelectedTarget.DisplayName} 을(를) " +
                             "재시작해야 변경사항이 적용됩니다.";
        }
        finally { IsBusy = false; }
    }

    /// <summary>받은 JSON 을 들여쓰기 형태로 재포맷. 파싱 실패 시(있을 수 없지만 방어적으로)
    /// 원문을 그대로 반환한다.</summary>
    private static string _PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
