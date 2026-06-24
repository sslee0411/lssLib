// ══════════════════════════════════════════════════════════
//  IIoT.Studio · MainViewModel.cs
//  역할: Studio 메인 ViewModel
//  S-08: CommLibraryViewModel 주입 추가
//  S-10: DeviceConfigService 주입 + SaveCommand 추가
//  S-11: CanvasViewModel + CollectConfigService 주입
//  S-12B: SwitchTab → 탭1 진입 시 Canvas.RefreshDevicePalette() 호출
//  S-15B: HasUnsavedChanges + CollectionChanged 구독 + 저장 시 리셋
//  S-19A: Ctrl+S → SaveCommand (XAML InputBinding)
//  S-19B: StatusBarPath, TotalTagCount, TotalPlcCount, LastSavedAt 추가
//  S-16: 저장 전 ValidationService 호출 추가
//  S-20B: ImportTagsCsvCommand 추가
//  S-18: OpenCommand / SaveAsCommand / ExportTagsCsvCommand 추가
//  생성: 2026-06-15 / 수정: 2026-06-20
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Studio.Core.Config;
using IIoT.Studio.Models;
using IIoT.Studio.ViewModels;
using IIoT.Studio.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace IIoT.Studio;

public partial class MainViewModel : ObservableObject
{
    // §1 ─ 서브 ViewModel ─────────────────────────────────────

    public DeviceTreeViewModel   DeviceTree   { get; }
    public ScaleLibraryViewModel ScaleLibrary { get; }
    public AlarmLibraryViewModel AlarmLibrary { get; }
    public CommLibraryViewModel  CommLibrary  { get; }
    public CanvasViewModel       Canvas       { get; }

    // §1-1 ─ 서비스 ───────────────────────────────────────────

    private readonly DeviceConfigService  _deviceSvc;
    private readonly CollectConfigService _collectSvc;

    // ★ S-16: 유효성 검사 서비스
    private readonly ValidationService _validationSvc;

    // ★ S-20B: CSV 가져오기 서비스
    private readonly TagCsvImporter _csvImporter = new();

    // ★ S-18: 열기·저장As·CSV내보내기 서비스
    private readonly ConfigImportExportService _importExportSvc;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public MainViewModel(
        DeviceTreeViewModel   deviceTree,
        ScaleLibraryViewModel scaleLibrary,
        AlarmLibraryViewModel alarmLibrary,
        CommLibraryViewModel  commLibrary,
        CanvasViewModel       canvas,
        DeviceConfigService   deviceSvc,
        CollectConfigService  collectSvc,
        DeviceConfigLoader    deviceLoader)
    {
        DeviceTree   = deviceTree;
        ScaleLibrary = scaleLibrary;
        AlarmLibrary = alarmLibrary;
        CommLibrary  = commLibrary;
        Canvas       = canvas;
        _deviceSvc   = deviceSvc;
        _collectSvc  = collectSvc;

        // ★ S-16
        _validationSvc = new ValidationService(DeviceTree, ScaleLibrary);

        // ★ S-18
        _importExportSvc = new ConfigImportExportService(
            deviceLoader, deviceSvc,
            deviceTree, scaleLibrary, alarmLibrary, commLibrary);

        // ★ S-15B: 변경 감지 구독
        DeviceTree.RootNodes.CollectionChanged += (_, _) =>
        {
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(TotalTagCount));
            OnPropertyChanged(nameof(TotalPlcCount));
        };
        Canvas.Nodes.CollectionChanged         += (_, _) => HasUnsavedChanges = true;
        Canvas.Connections.CollectionChanged   += (_, _) => HasUnsavedChanges = true;
        ScaleLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        AlarmLibrary.Entries.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        CommLibrary.Entries.CollectionChanged  += (_, _) => HasUnsavedChanges = true;

        // ★ S-19B: 트리 선택 변경 → StatusBarPath 갱신
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
            {
                OnPropertyChanged(nameof(StatusBarPath));
                OnPropertyChanged(nameof(TotalTagCount));
                OnPropertyChanged(nameof(TotalPlcCount));
            }
        };
    }

    // §3 ─ 저장 상태 ──────────────────────────────────────────

    [ObservableProperty]
    private string _saveStatus = "준비됨";

    // ★ S-15B: 미저장 여부
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // ★ S-19B: 마지막 저장 시각
    [ObservableProperty]
    private string _lastSavedAt = string.Empty;

    // §4 ─ 탭 전환 ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceTab))]
    [NotifyPropertyChangedFor(nameof(IsCanvasTab))]
    [NotifyPropertyChangedFor(nameof(IsScaleTab))]
    [NotifyPropertyChangedFor(nameof(IsAlarmTab))]
    [NotifyPropertyChangedFor(nameof(IsCommTab))]
    private int _activeTabIndex;

    // §5 ─ 탭 가시성 ─────────────────────────────────────────

    public bool IsDeviceTab => ActiveTabIndex == 0;
    public bool IsCanvasTab => ActiveTabIndex == 1;
    public bool IsScaleTab  => ActiveTabIndex == 2;
    public bool IsAlarmTab  => ActiveTabIndex == 3;
    public bool IsCommTab   => ActiveTabIndex == 4;

    // §6 ─ S-19B 상태바 프로퍼티 ────────────────────────────

    /// <summary>
    /// 선택된 노드의 계층 경로.
    /// 예: "공장1 > PLC-01 > 온도Tag"
    /// </summary>
    public string StatusBarPath
    {
        get
        {
            var node = DeviceTree.SelectedNode;
            if (node is null) return "노드 미선택";
            return _BuildPath(node, DeviceTree.RootNodes);
        }
    }

    /// <summary>전체 Tag 수 (재귀 카운트)</summary>
    public int TotalTagCount => _CountAll<TagTreeNode>(DeviceTree.RootNodes);

    /// <summary>전체 PLC 수 (재귀 카운트)</summary>
    public int TotalPlcCount => _CountAll<PlcTreeNode>(DeviceTree.RootNodes);

    // §7 ─ 커맨드 ─────────────────────────────────────────────

    [RelayCommand]
    private void SwitchTab(string tabParam)
    {
        if (!int.TryParse(tabParam, out var idx)) return;
        ActiveTabIndex = idx;

        // ★ S-12B: 수집 흐름 탭 진입 시 장비 팔레트 강제 갱신
        if (idx == 1)
            Canvas.RefreshDevicePalette();
    }

    // §9 ─ ★ S-20B: CSV 가져오기 커맨드 ─────────────────────

    [RelayCommand]
    private async Task ImportTagsCsvAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title       = "Tag CSV 파일 선택",
            Filter      = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog() != true) return;

        SaveStatus = "CSV 가져오는 중…";

        var result = await Task.Run(() =>
            _csvImporter.Import(dlg.FileName, DeviceTree.RootNodes));

        if (result.IsSuccess)
        {
            HasUnsavedChanges = true;
            SaveStatus        = $"✔ {result.Summary}";

            if (result.Errors.Count > 0)
            {
                var errorText = string.Join("\n", result.Errors.Take(10));
                MessageBox.Show(
                    $"일부 행을 건너뛰었습니다:\n\n{errorText}",
                    "CSV 가져오기 — 경고",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        else
        {
            SaveStatus = "✖ CSV 가져오기 실패";
            var errorText = string.Join("\n", result.Errors.Take(5));
            MessageBox.Show(
                $"가져오기에 실패했습니다:\n\n{errorText}",
                "CSV 가져오기 — 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }

    // §10 ─ 저장 커맨드 ───────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveEnabled))]
    private bool _isSaving;

    public bool IsSaveEnabled => !_isSaving;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_isSaving) return;

        // ★ S-16: 저장 전 유효성 검사
        var issues = _validationSvc.Validate();
        if (issues.Count > 0)
        {
            // 오류 또는 경고가 있으면 다이얼로그 표시
            var dlg = new ValidationErrorDialog(
                issues,
                DeviceTree,
                Application.Current.MainWindow);

            if (dlg.ShowDialog() != true || !dlg.ShouldSave)
                return; // 취소 → 저장 중단
        }

        IsSaving   = true;
        SaveStatus = "저장 중…";

        var deviceResult  = await _deviceSvc.SaveAsync();
        var collectResult = await _collectSvc.SaveAsync();

        if (deviceResult.IsSuccess && collectResult.IsSuccess)
        {
            HasUnsavedChanges = false;
            LastSavedAt = DateTime.Now.ToString("HH:mm:ss");
            SaveStatus  = $"✔ 저장 완료  ({LastSavedAt})";
        }
        else
        {
            var failed = !deviceResult.IsSuccess
                ? deviceResult.Message
                : collectResult.Message;
            SaveStatus = $"✖ {failed}";
        }

        IsSaving = false;

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }

    // §9 ─ 내부 헬퍼 ──────────────────────────────────────────

    /// <summary>노드 계층 경로 재귀 빌드</summary>
    private static string _BuildPath(
        AbstractTreeNode target,
        IEnumerable<AbstractTreeNode> nodes,
        string prefix = "")
    {
        foreach (var node in nodes)
        {
            var current = string.IsNullOrEmpty(prefix)
                ? node.Name
                : $"{prefix} > {node.Name}";

            if (ReferenceEquals(node, target)) return current;

            var found = _BuildPath(target, node.Children, current);
            if (!string.IsNullOrEmpty(found)) return found;
        }
        return string.Empty;
    }

    /// <summary>타입 T 노드 재귀 카운트</summary>
    private static int _CountAll<T>(IEnumerable<AbstractTreeNode> nodes)
        where T : AbstractTreeNode
    {
        int count = 0;
        foreach (var node in nodes)
        {
            if (node is T) count++;
            count += _CountAll<T>(node.Children);
        }
        return count;
    }

    // §11 ─ ★ S-18: 열기·저장As·CSV 내보내기 ─────────────────

    /// <summary>📂 열기 — device.json 파일 선택 후 전체 설정 교체 로드</summary>
    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "설정 파일 열기",
            Filter = "IIoT 설정 파일 (device.json)|device.json|JSON 파일 (*.json)|*.json",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;

        if (HasUnsavedChanges)
        {
            var r = MessageBox.Show(
                "저장하지 않은 변경사항이 있습니다.\n계속하면 현재 설정이 사라집니다. 계속하시겠습니까?",
                "설정 열기 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        SaveStatus = "설정 로드 중…";
        var result = await _importExportSvc.OpenAsync(dlg.FileName);

        if (result.IsSuccess)
        {
            HasUnsavedChanges = false;
            SaveStatus = $"✔ 열기 완료: {Path.GetFileName(dlg.FileName)}";
        }
        else
        {
            SaveStatus = $"✖ {result.Message}";
            MessageBox.Show(result.Message, "열기 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }

    /// <summary>💾 다른 이름으로 저장</summary>
    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "다른 이름으로 저장",
            Filter     = "IIoT 설정 파일 (*.json)|*.json",
            FileName   = "device.json",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog() != true) return;

        SaveStatus = "저장 중…";
        var result = await _importExportSvc.SaveAsAsync(dlg.FileName);
        SaveStatus = result.IsSuccess
            ? $"✔ 저장 완료: {Path.GetFileName(dlg.FileName)}"
            : $"✖ {result.Message}";

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }

    /// <summary>📤 Tag CSV 내보내기</summary>
    [RelayCommand]
    private async Task ExportTagsCsvAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Tag 목록 CSV 내보내기",
            Filter     = "CSV 파일 (*.csv)|*.csv",
            FileName   = $"Tags_{DateTime.Now:yyyyMMdd}.csv",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog() != true) return;

        SaveStatus = "CSV 내보내는 중…";
        var result = await _importExportSvc.ExportTagsCsvAsync(dlg.FileName);
        SaveStatus = result.IsSuccess ? $"✔ {result.Message}" : $"✖ {result.Message}";

        await Task.Delay(3000);
        if (!_isSaving) SaveStatus = "준비됨";
    }
}
