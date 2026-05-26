// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/MainViewModel.cs
//  역할: 메인 윈도우 ViewModel — 트리 선택 → 편집기 자동 교체
//  Phase 3: 편집기 패널 연결
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.ViewModels.DeviceTree;
using IIoT.DeviceManager.ViewModels.Editors;

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
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // §1 ─ 의존성 ─────────────────────────────────────────────
    private readonly JsonConfigLoader _configLoader;

    // §2 ─ 서브 ViewModel ─────────────────────────────────────
    public DeviceTreeViewModel DeviceTree { get; }

    // §3 ─ 편집기 ─────────────────────────────────────────────
    private readonly DeviceEditorViewModel _deviceEditor = new();
    private readonly PlcEditorViewModel _plcEditor = new();
    private readonly TagEditorViewModel _tagEditor = new();
    private readonly SensorEditorViewModel _sensorEditor = new();

    /// <summary>
    /// 현재 표시할 편집기 ViewModel.
    /// ContentControl 이 DataTemplate 으로 자동 매핑.
    /// null 이면 플레이스홀더 표시.
    /// </summary>
    [ObservableProperty] private ObservableObject? _selectedEditor;

    // §4 ─ 생성자 ─────────────────────────────────────────────
    public MainViewModel(DeviceTreeViewModel deviceTree, JsonConfigLoader configLoader)
    {
        DeviceTree = deviceTree;
        _configLoader = configLoader;

        // 트리 선택 변경 구독
        DeviceTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceTreeViewModel.SelectedNode))
                _OnSelectedNodeChanged(DeviceTree.SelectedNode);
        };
    }

    // §5 ─ 선택 변경 처리 ─────────────────────────────────────

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
                // 전체 Tag 목록 수집 (OwnerDevice 탐색용)
                var allNodes = DeviceTree.RootNodes
                    .SelectMany(r => r.Flatten())
                    .ToList();
                _tagEditor.Load(tag, allNodes);
                SelectedEditor = _tagEditor;
                break;

            case SensorNodeViewModel sensor:
                // 같은 Device 하위의 Tag 목록 수집 (TagRef 연결용)
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
}