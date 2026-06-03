// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/MainViewModel.cs
//  역할: 메인 윈도우 ViewModel — 트리 선택 → 편집기 자동 교체
//  Phase 3: 편집기 패널 연결
//  Phase 5: DataContext 교체 + SaveDeviceTreeCommand 실연동
//           + LoadDeviceTreeCommand (앱 시작 시 device.json 로드)
//
//  ★ Phase 5 Fix:
//    - namespace: ViewModels.Editors → ViewModels (위치 변경)
//    - [RelayCommand] 비동기 메서드명: SaveDeviceTreeAsync → SaveDeviceTree
//      CommunityToolkit.Mvvm 은 Async 접미사를 자동 제거하지 않음
//      → 생성 커맨드명: SaveDeviceTreeCommand (기대) 확인
//    - LoadDeviceTreeAsync → LoadDeviceTree
//      → 생성 커맨드명: LoadDeviceTreeCommand
//    - MainWindow.xaml.cs 에서 참조: SaveDeviceTreeCommand / LoadDeviceTreeCommand
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.DeviceManager.ViewModels.Editors;
using lssLib.Log;

namespace IIoT.DeviceManager.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel.
///
/// SelectedNode 변경 → SelectedEditor 자동 갱신.
/// ContentControl 이 DataTemplate 으로 EditorView 를 자동 선택.
///
/// DataTemplate 매핑 (MainWindow.xaml Resources):
///   DeviceEditorViewModel  → DeviceEditorView
///   PlcEditorViewModel     → PlcEditorView
///   TagEditorViewModel     → TagEditorView
///   SensorEditorViewModel  → SensorEditorView
///
/// Phase 5 변경:
///   · SaveDeviceTreeCommand — DeviceTreeSerializer → JsonWriteService 저장
///   · LoadDeviceTreeCommand — device.json → DeviceTreeSerializer 역직렬화
///   · SaveStatus 프로퍼티   — MainWindow 상태바 바인딩용
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // §1 ─ 의존성 ─────────────────────────────────────────────
    private const string LogSrc = "MainViewModel";

    private readonly JsonConfigLoader _configLoader;
    private readonly JsonWriteService _writeService;

    // §2 ─ 서브 ViewModel ─────────────────────────────────────
    public DeviceTreeViewModel DeviceTree { get; }

    // §3 ─ 편집기 ─────────────────────────────────────────────
    private readonly DeviceEditorViewModel  _deviceEditor = new();
    private readonly PlcEditorViewModel     _plcEditor    = new();
    private readonly TagEditorViewModel     _tagEditor    = new();
    private readonly SensorEditorViewModel  _sensorEditor = new();

    /// <summary>
    /// 현재 표시할 편집기 ViewModel.
    /// ContentControl 이 DataTemplate 으로 자동 매핑.
    /// null 이면 플레이스홀더 표시.
    /// </summary>
    [ObservableProperty]
    private ObservableObject? _selectedEditor;

    // §4 ─ 상태 프로퍼티 (Phase 5 신규) ──────────────────────

    /// <summary>저장/로드 작업 결과 메시지 (상태바 바인딩용)</summary>
    [ObservableProperty]
    private string _saveStatus = "준비";

    /// <summary>저장 중 여부 (버튼 비활성화용)</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>미저장 변경사항 존재 여부</summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // §5 ─ 생성자 ─────────────────────────────────────────────
    public MainViewModel(
        DeviceTreeViewModel deviceTree,
        JsonConfigLoader    configLoader,
        JsonWriteService    writeService)
    {
        DeviceTree    = deviceTree;
        _configLoader = configLoader;
        _writeService = writeService;

        // 트리 선택 변경 구독
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _OnSelectedNodeChanged(DeviceTree.SelectedNode);

            // 노드 변경 → 미저장 플래그
            if (e.PropertyName == nameof(DeviceTreeViewModel.TotalNodeCount))
                HasUnsavedChanges = true;
        };
    }

    // §6 ─ 선택 변경 처리 ─────────────────────────────────────

    private void _OnSelectedNodeChanged(DeviceNodeViewModel? node)
    {
        if (node is null)
        {
            SelectedEditor = null;
            return;
        }

        var bundle = _configLoader.LoadAll();

        switch (node)
        {
            case DeviceItemViewModel device:
                _deviceEditor.Load(device, bundle.CommConfigs, bundle.Locations);
                SelectedEditor = _deviceEditor;
                break;

            case PlcNodeViewModel plc:
                _plcEditor.Load(plc);
                SelectedEditor = _plcEditor;
                break;

            case TagNodeViewModel tag:
                var allNodes = DeviceTree.RootNodes
                    .SelectMany(r => r.Flatten())
                    .ToList();
                _tagEditor.Load(tag, allNodes);
                SelectedEditor = _tagEditor;
                break;

            case SensorNodeViewModel sensor:
                var siblingTags = node.Parent?.Children
                    .OfType<TagNodeViewModel>()
                    .ToList() ?? [];
                _sensorEditor.Load(sensor, siblingTags);
                SelectedEditor = _sensorEditor;
                break;

            default:
                SelectedEditor = null;
                break;
        }
    }

    // §7 ─ 저장 커맨드 (Phase 5 신규) ─────────────────────────

    /// <summary>
    /// 장비 트리를 device.json 에 저장합니다.
    ///
    /// ★ CommunityToolkit.Mvvm [RelayCommand] 비동기 메서드 네이밍 규칙:
    ///    메서드명 SaveDeviceTree → 생성 커맨드명 SaveDeviceTreeCommand
    ///    (Async 접미사를 붙이면 SaveDeviceTreeAsyncCommand 가 되어 이름 불일치)
    ///
    /// 흐름:
    ///   1. 현재 편집기 Apply (변경사항 즉시 반영)
    ///   2. DeviceTreeSerializer.Serialize() → ConfigTree
    ///   3. JsonWriteService.SaveDeviceTree() → .tmp → rename → .bak → SHA-256
    /// </summary>
    [RelayCommand(CanExecute = nameof(_CanExecute))]
    private async Task SaveDeviceTree()
    {
        if (IsBusy) return;

        IsBusy     = true;
        SaveStatus = "저장 중...";

        try
        {
            // ① 현재 편집기 Apply (미적용 변경사항 반영)
            _ApplyCurrentEditor();

            // ② ViewModel → ConfigTree 직렬화 (백그라운드)
            await Task.Run(() =>
            {
                var tree = DeviceTreeSerializer.Serialize(DeviceTree.RootNodes);
                _writeService.SaveDeviceTree(tree);
            });

            HasUnsavedChanges = false;
            SaveStatus = $"저장 완료 ({DateTime.Now:HH:mm:ss}) — " +
                         $"{DeviceTree.TotalNodeCount}개 노드";

            LogManager.Instance.Info(LogSrc,
                $"device.json 저장 완료: {DeviceTree.TotalNodeCount}개 노드");
        }
        catch (Exception ex)
        {
            SaveStatus = $"저장 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"device.json 저장 오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // §8 ─ 로드 커맨드 (Phase 5 신규) ─────────────────────────

    /// <summary>
    /// device.json 을 읽어 DeviceTree 에 로드합니다.
    ///
    /// ★ 메서드명 LoadDeviceTree → 생성 커맨드명 LoadDeviceTreeCommand
    /// </summary>
    [RelayCommand(CanExecute = nameof(_CanExecute))]
    private async Task LoadDeviceTree()
    {
        if (IsBusy) return;

        IsBusy     = true;
        SaveStatus = "로드 중...";

        try
        {
            await Task.Run(() =>
            {
                var (tree, integrityOk) = _configLoader.LoadDeviceTree();

                // UI 스레드에서 트리 업데이트
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    DeviceTree.RootNodes.Clear();
                    DeviceTree.SelectedNode = null;

                    var nodes = DeviceTreeSerializer.Deserialize(tree);
                    foreach (var vm in nodes)
                        DeviceTree.RootNodes.Add(vm);

                    SaveStatus = integrityOk
                        ? $"로드 완료 — {DeviceTree.TotalNodeCount}개 노드"
                        : $"⚠ 무결성 경고 — device.json 외부 변경 감지 " +
                          $"({DeviceTree.TotalNodeCount}개 노드 로드됨)";
                });
            });

            HasUnsavedChanges = false;
            LogManager.Instance.Info(LogSrc,
                $"device.json 로드 완료: {DeviceTree.TotalNodeCount}개 노드");
        }
        catch (Exception ex)
        {
            SaveStatus = $"로드 실패: {ex.Message}";
            LogManager.Instance.Error(LogSrc, $"device.json 로드 오류: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // §9 ─ 내부 헬퍼 ──────────────────────────────────────────

    private bool _CanExecute() => !IsBusy;

    /// <summary>현재 활성 편집기의 변경사항을 대상 노드에 Apply 합니다.</summary>
    private void _ApplyCurrentEditor()
    {
        switch (SelectedEditor)
        {
            case DeviceEditorViewModel  d when d.HasChanges: d.ApplyCommand.Execute(null); break;
            case PlcEditorViewModel     p when p.HasChanges: p.ApplyCommand.Execute(null); break;
            case TagEditorViewModel     t when t.HasChanges: t.ApplyCommand.Execute(null); break;
            case SensorEditorViewModel  s when s.HasChanges: s.ApplyCommand.Execute(null); break;
        }
    }
}
