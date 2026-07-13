// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/DeployViewModel.cs
//  역할: [배포] 탭 ViewModel — 설정 배포 관리 (요구사항 4-2-7)
//        소스 파일 현황 + 대상 프로그램 선택 + 배포 실행 + 결과 표시
//  MG-06: 신규
//  생성: 2026-07-09
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Core.Config;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)

namespace IIoT.Manager.ViewModels;

/// <summary>소스 파일 1개의 현황 (이름 + 수정시각/없음).</summary>
public sealed record SourceFileInfo(string Name, string StatusText, bool Exists);

/// <summary>배포 대상 1개 (선택 체크 + 마지막 결과).</summary>
public partial class DeployTargetItem : ObservableObject
{
    public Models.ManagedProcessInfo Info { get; }

    /// <summary>배포 대상으로 선택 여부</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>마지막 배포 결과 문구</summary>
    [ObservableProperty]
    private string _lastResult = "";

    public DeployTargetItem(Models.ManagedProcessInfo info) => Info = info;
}

/// <summary>
/// 설정 배포 ViewModel.
/// <para>
/// 소스 = manager.json Deploy.SourceConfigDir (기본: Studio Config 폴더).
/// 대상 = Processes[] 중 Studio(소스 프로그램) 제외.
/// 배포 실행·결과는 EventHistoryService 에도 기록된다.
/// </para>
/// </summary>
public partial class DeployViewModel : ObservableObject
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private readonly ManagerSettingsLoader _settingsLoader;
    private readonly ConfigDeployService   _deployService;
    private readonly EventHistoryService   _events;
    private bool                           _initialized;

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>소스 파일 현황 목록</summary>
    public ObservableCollection<SourceFileInfo> SourceFiles { get; } = [];

    /// <summary>배포 대상 목록 (체크 선택)</summary>
    public ObservableCollection<DeployTargetItem> Targets { get; } = [];

    // §3 ─ 관찰 속성 ─────────────────────────────────────────

    /// <summary>소스 폴더 (해석된 절대 경로 표시)</summary>
    [ObservableProperty]
    private string _sourceDirText = "";

    /// <summary>배포 진행 중 여부 (버튼 비활성)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeployCommand))]
    private bool _isBusy;

    /// <summary>상태 문구</summary>
    [ObservableProperty]
    private string _statusText = "";

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public DeployViewModel(ManagerSettingsLoader settingsLoader,
                           ConfigDeployService   deployService,
                           EventHistoryService   events)
    {
        _settingsLoader = settingsLoader;
        _deployService  = deployService;
        _events         = events;
    }

    // §5 ─ 공개 메서드 ────────────────────────────────────────

    /// <summary>
    /// manager.json 로드 후 소스·대상을 구성한다.
    /// ManagerMainViewModel.InitializeAsync 에서 1회 호출 (재호출 무시).
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // 대상: Studio(소스 프로그램) 제외 — 자기 자신에게 재배포 방지
        Targets.Clear();
        foreach (var p in _settingsLoader.Settings.Processes.Where(p => p.Id != "studio"))
            Targets.Add(new DeployTargetItem(p));

        RefreshSource();
    }

    // §6 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>소스 파일 현황 새로고침</summary>
    [RelayCommand]
    private void RefreshSource()
    {
        var deploy = _settingsLoader.Settings.Deploy;
        var srcDir = ConfigDeployService.ResolvePath(deploy.SourceConfigDir);
        SourceDirText = srcDir;

        SourceFiles.Clear();
        foreach (var f in deploy.Files)
        {
            var path = Path.Combine(srcDir, f);
            SourceFiles.Add(File.Exists(path)
                ? new SourceFileInfo(f, $"수정: {File.GetLastWriteTime(path):MM-dd HH:mm:ss}", true)
                : new SourceFileInfo(f, "파일 없음", false));
        }

        StatusText = $"소스 확인: {DateTime.Now:HH:mm:ss}";
    }

    private bool _CanDeploy() => !IsBusy;

    /// <summary>🚀 선택 대상에 배포 (백업 → 복사 → .signal 발행)</summary>
    [RelayCommand(CanExecute = nameof(_CanDeploy))]
    private async Task DeployAsync()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "배포 대상을 선택해 주세요.";
            return;
        }

        IsBusy = true;
        try
        {
            var deploy = _settingsLoader.Settings.Deploy;
            int ok = 0, fail = 0;

            foreach (var t in selected)
            {
                var result = await _deployService.DeployAsync(t.Info, deploy.SourceConfigDir, deploy.Files);

                t.LastResult = result.Ok
                    ? $"✅ {result.Message} ({DateTime.Now:HH:mm:ss})"
                    : $"❌ {result.Message}";

                // ★ 대시보드 이벤트 이력 + 로그 기록
                _events.Record(t.Info.Name,
                    result.Ok ? $"설정 배포 성공 — {result.Message}"
                              : $"설정 배포 실패 — {result.Message}");

                if (result.Ok) ok++; else fail++;
            }

            StatusText = $"배포 완료 — 성공 {ok} / 실패 {fail} ({DateTime.Now:HH:mm:ss})";
        }
        finally { IsBusy = false; }
    }
}
