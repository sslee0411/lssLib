// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/DeployViewModel.cs
//  역할: [배포] 탭 ViewModel — 설정 배포 관리 (요구사항 4-2-7)
//        소스 파일 현황 + 대상 프로그램 선택 + 배포 실행 + 결과 표시
//  MG-06: 신규
//  MG-EX-08: 롤백 — 대상별 백업 시점 콤보 + [↩ 롤백] 버튼.
//        백업 목록은 초기화/새로고침/배포/롤백 후 자동 갱신
//  MG-EX-09: 비교 — [🔎 비교] 버튼 → 소스↔대상 diff 요약을 하단 결과창에 표시
//  MG-EX-10: "배포 후 재시작" 옵션 — 체크 시 배포 성공 + 실행 중인 대상을
//        자동 재시작 (.signal 미지원 프로그램의 새 설정 즉시 반영용)
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-10)
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

/// <summary>배포 대상 1개 (선택 체크 + 마지막 결과 + 백업 시점 목록).</summary>
public partial class DeployTargetItem : ObservableObject
{
    public Models.ManagedProcessInfo Info { get; }

    /// <summary>배포 대상으로 선택 여부</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>마지막 배포/롤백 결과 문구</summary>
    [ObservableProperty]
    private string _lastResult = "";

    /// <summary>★ MG-EX-08: 백업 시점 목록 (최신순 — Config\Backup\ 폴더명)</summary>
    public ObservableCollection<string> Backups { get; } = [];

    /// <summary>★ MG-EX-08: 선택된 백업 시점 (롤백 대상)</summary>
    [ObservableProperty]
    private string? _selectedBackup;

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
    private readonly ProcessManager        _processManager;   // ★ MG-EX-10
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

    /// <summary>★ MG-EX-09: 비교 결과 텍스트 (빈 문자열 = 결과창 숨김)</summary>
    [ObservableProperty]
    private string _compareText = "";

    /// <summary>
    /// ★ MG-EX-10: 배포 후 자동 재시작 — 배포 성공 + 실행 중인 대상만.
    /// (.signal 자동 재로드를 지원하지 않는 프로그램용. 기본 해제)
    /// </summary>
    [ObservableProperty]
    private bool _restartAfterDeploy;

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public DeployViewModel(ManagerSettingsLoader settingsLoader,
                           ConfigDeployService   deployService,
                           EventHistoryService   events,
                           ProcessManager        processManager)
    {
        _settingsLoader = settingsLoader;
        _deployService  = deployService;
        _events         = events;
        _processManager = processManager;
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
        {
            var item = new DeployTargetItem(p);
            _RefreshBackups(item);   // ★ MG-EX-08
            Targets.Add(item);
        }

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

        // ★ MG-EX-08: 백업 목록도 함께 갱신
        foreach (var t in Targets)
            _RefreshBackups(t);

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

                // ★ 대시보드 이벤트 이력 + 로그 기록 (MG-EX-02: 실패는 Warning)
                _events.Record(t.Info.Name,
                    result.Ok ? $"설정 배포 성공 — {result.Message}"
                              : $"설정 배포 실패 — {result.Message}",
                    result.Ok ? EventSeverity.Info : EventSeverity.Warning);

                if (result.Ok) ok++; else fail++;

                _RefreshBackups(t);   // ★ MG-EX-08: 배포로 생성된 백업 반영

                // ★ MG-EX-10: 배포 후 자동 재시작 (옵션 체크 + 배포 성공 + 실행 중일 때만)
                if (RestartAfterDeploy && result.Ok && _processManager.IsRunning(t.Info))
                {
                    var restart = await _processManager.RestartAsync(t.Info);

                    t.LastResult += restart.Ok ? " · 재시작됨" : $" · 재시작 실패: {restart.Error}";
                    _events.Record(t.Info.Name,
                        restart.Ok ? "배포 후 자동 재시작 완료"
                                   : $"배포 후 자동 재시작 실패 — {restart.Error}",
                        restart.Ok ? EventSeverity.Info : EventSeverity.Warning);
                }
            }

            StatusText = $"배포 완료 — 성공 {ok} / 실패 {fail} ({DateTime.Now:HH:mm:ss})";
        }
        finally { IsBusy = false; }
    }

    /// <summary>★ MG-EX-09: 🔎 소스 ↔ 대상 설정 비교 (배포 전 확인)</summary>
    [RelayCommand(CanExecute = nameof(_CanDeploy))]
    private async Task CompareAsync(DeployTargetItem item)
    {
        IsBusy = true;
        try
        {
            var deploy  = _settingsLoader.Settings.Deploy;
            var results = await _deployService.CompareAsync(item.Info, deploy.SourceConfigDir, deploy.Files);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"■ {item.Info.Name} 비교 (소스 → 대상 기준) — {DateTime.Now:HH:mm:ss}");

            foreach (var r in results)
            {
                sb.AppendLine($"  {r.FileName,-18} : {r.Status}");
                foreach (var s in r.Samples)
                    sb.AppendLine($"      {s}");
            }

            var diffCount = results.Count(r => r.Added + r.Removed > 0 || r.Status.Contains("없음"));
            sb.AppendLine(diffCount == 0
                ? "  → 모든 파일 동일 — 배포해도 변경 없음"
                : $"  → 차이 있는 파일 {diffCount}개 — 배포 시 위 내용으로 변경됨 (+ 추가 / - 제거)");

            CompareText = sb.ToString();
            StatusText  = $"비교 완료: {item.Info.Name}";
        }
        catch (Exception ex)
        {
            // ★ 규칙: 조용히 삼키지 않는다
            lssLib.Log.LogManager.Instance.Error("Deploy", $"비교 실패: {ex.Message}");
            StatusText = $"비교 실패: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>★ MG-EX-08: ↩ 선택 백업 시점으로 롤백 (복원 → .signal 발행)</summary>
    [RelayCommand(CanExecute = nameof(_CanDeploy))]
    private async Task RollbackAsync(DeployTargetItem item)
    {
        if (item.SelectedBackup is not string backup)
        {
            StatusText = "롤백할 백업 시점을 선택해 주세요.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deployService.RollbackAsync(item.Info, backup);

            item.LastResult = result.Ok
                ? $"↩ {result.Message} ({DateTime.Now:HH:mm:ss})"
                : $"❌ {result.Message}";

            // ★ 이벤트 이력 + 로그 (실패는 Warning — 트레이 알림)
            _events.Record(item.Info.Name,
                result.Ok ? $"설정 롤백 성공 — {result.Message}"
                          : $"설정 롤백 실패 — {result.Message}",
                result.Ok ? EventSeverity.Info : EventSeverity.Warning);

            _RefreshBackups(item);   // 롤백 시 생성된 "직전 상태 백업" 반영
            StatusText = $"롤백 {(result.Ok ? "완료" : "실패")} ({DateTime.Now:HH:mm:ss})";
        }
        finally { IsBusy = false; }
    }

    // §7 ─ 내부 메서드 ────────────────────────────────────────

    /// <summary>★ MG-EX-08: 대상의 백업 시점 목록 갱신 (선택값 유지 시도).</summary>
    private void _RefreshBackups(DeployTargetItem item)
    {
        var prev = item.SelectedBackup;

        item.Backups.Clear();
        foreach (var name in _deployService.GetBackupNames(item.Info))
            item.Backups.Add(name);

        item.SelectedBackup = prev is not null && item.Backups.Contains(prev)
            ? prev
            : item.Backups.FirstOrDefault();
    }
}
