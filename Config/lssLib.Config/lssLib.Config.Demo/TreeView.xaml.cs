// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/TreeView.xaml.cs
//  탭④: ConfigTree 장비 계층 CRUD
//        + JSON / XML 파일 선택 다이얼로그 → 트리 렌더링
//        + 파일 원본 (JSON / XML) 미리보기 패널
//        + 선택 노드 상세 패널
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using lssLib.Config.Tree;
using Microsoft.Win32;

namespace lssLib.Config.Demo.Views;

public partial class TreeView : UserControl
{
    #region §1 ─ 필드

    private readonly ConfigTree _tree = new();

    // WPF TreeViewItem ↔ ConfigNode 매핑
    private readonly Dictionary<TreeViewItem, ConfigNode> _itemMap = new();

    // 동적 프로퍼티 입력 필드 (유형별 TextBox 쌍)
    private readonly List<(TextBox Key, TextBox Val)> _propFields = new();

    // 최근 로드 파일 경로 목록 (최대 8개)
    private readonly List<string> _recentFiles = new();

    // 마지막으로 로드한 파일 경로
    private string? _strLastLoadedPath;

    // 기본 저장 디렉터리
    private static readonly string _strDefaultDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");

    #endregion

    #region §2 ─ 초기화

    public TreeView()
    {
        InitializeComponent();
        UpdatePropFields();

        // 트리 변경 이벤트
        _tree.NodeChanged += (node, action) =>
            Dispatcher.InvokeAsync(() =>
            {
                RebuildTreeView();
                UpdateStats();
                Log($"🔔 [{action}] {node.Type}: {node.Name}");
            });

        Loaded += (_, _) =>
        {
            // 앱 시작 시 기본 저장 파일이 있으면 자동 로드
            var defaultJson = Path.Combine(_strDefaultDir, "devices.json");
            if (File.Exists(defaultJson))
            {
                TbFilePath.Text = defaultJson;
                LoadFileInternal(defaultJson);
                Log($"▶ 앱 시작 자동 로드: {Path.GetFileName(defaultJson)}");
            }
            else
            {
                // 파일 없으면 샘플 트리 생성
                BtnBuildSample_Click(this, new RoutedEventArgs());
            }
        };
    }

    #endregion

    #region §3 ─ 파일 로더 ★ 신규 핵심 기능

    // ── 파일 선택 다이얼로그 ───────────────────────────────────────────
    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "장비 트리 파일 선택 (JSON 또는 XML)",
            Filter = "트리 파일|*.json;*.xml|JSON 파일|*.json|XML 파일|*.xml|모든 파일|*.*",
            InitialDirectory = Directory.Exists(_strDefaultDir)
                ? _strDefaultDir
                : AppDomain.CurrentDomain.BaseDirectory
        };

        if (dlg.ShowDialog() != true) return;

        TbFilePath.Text = dlg.FileName;
        LoadFileInternal(dlg.FileName);
    }

    // ── 경로 직접 입력 후 로드 ────────────────────────────────────────
    private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
    {
        var path = TbFilePath.Text.Trim();

        if (string.IsNullOrEmpty(path))
        {
            // 경로 없으면 다이얼로그 열기
            BtnBrowse_Click(sender, e);
            return;
        }

        if (!File.Exists(path))
        {
            Log($"❌ 파일 없음: {path}");
            TbFileInfo.Text = "❌ 파일 없음";
            TbFileInfo.Foreground = Brushes.Salmon;
            return;
        }

        LoadFileInternal(path);
    }

    // ── 마지막 파일 새로고침 ──────────────────────────────────────────
    private void BtnReloadFile_Click(object sender, RoutedEventArgs e)
    {
        if (_strLastLoadedPath is null)
        {
            Log("⚠ 로드된 파일이 없습니다. 먼저 파일을 선택하세요.");
            return;
        }
        LoadFileInternal(_strLastLoadedPath);
        Log($"🔄 새로고침: {Path.GetFileName(_strLastLoadedPath)}");
    }

    // ── 최근 파일 선택 ────────────────────────────────────────────────
    private void LbRecent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbRecent.SelectedIndex < 0) return;
        if (LbRecent.SelectedIndex >= _recentFiles.Count) return;

        var path = _recentFiles[LbRecent.SelectedIndex];
        if (!File.Exists(path))
        {
            Log($"⚠ 파일이 더 이상 존재하지 않습니다: {Path.GetFileName(path)}");
            return;
        }

        TbFilePath.Text = path;
        LoadFileInternal(path);
    }

    // ── 파일 로드 핵심 로직 ★ ────────────────────────────────────────
    /// <summary>
    /// JSON 또는 XML 파일을 읽어 ConfigTree 에 파싱하고
    /// WPF TreeView + 파일 미리보기 패널을 동시에 갱신합니다.
    /// </summary>
    private void LoadFileInternal(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToUpperInvariant();
            var fileInfo = new FileInfo(path);
            var rawText = File.ReadAllText(path, Encoding.UTF8);

            // ── Step 1. 오른쪽 미리보기 패널에 파일 원본 표시 ─────────
            TbJsonPreview.Text = rawText;
            TbFileInfo.Text =
                $"  {Path.GetFileName(path)}" +
                $"  |  {(ext.Length > 1 ? ext[1..] : ext)}" +
                $"  |  {fileInfo.Length / 1024.0:F1} KB" +
                $"  |  {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            TbFileInfo.Foreground = Brushes.LightSkyBlue;

            // ── Step 2. ConfigTree 파싱 ────────────────────────────────
            _tree.Clear();

            if (ext == ".JSON")
                _tree.FromJson(rawText);
            else if (ext == ".XML")
                _tree.FromXml(rawText);
            else
                throw new NotSupportedException($"지원하지 않는 파일 형식: {ext}");

            // ── Step 3. WPF TreeView 렌더링 ────────────────────────────
            RebuildTreeView();
            UpdateStats();

            // ── Step 4. 상태 갱신 ──────────────────────────────────────
            _strLastLoadedPath = path;
            AddRecentFile(path);

            Log($"✅ 로드 완료: {Path.GetFileName(path)}" +
                $"  ({_tree.Count}개 노드  |  {fileInfo.Length / 1024.0:F1} KB)");

            MainWindow.SetStatus(
                $"트리 로드 → {Path.GetFileName(path)}  ({_tree.Count}개 노드)");
        }
        catch (Exception ex)
        {
            Log($"❌ 로드 실패: {ex.Message}");
            TbFileInfo.Text = $"❌  {ex.Message}";
            TbFileInfo.Foreground = Brushes.Salmon;
        }
    }

    // ── 최근 파일 목록 관리 ──────────────────────────────────────────
    private void AddRecentFile(string path)
    {
        // 중복 제거 후 맨 앞에 삽입
        _recentFiles.Remove(path);
        _recentFiles.Insert(0, path);

        // 최대 8개 유지
        while (_recentFiles.Count > 8)
            _recentFiles.RemoveAt(_recentFiles.Count - 1);

        // ListBox 갱신 (파일명만 표시)
        LbRecent.ItemsSource = null;
        LbRecent.ItemsSource = _recentFiles.Select(Path.GetFileName).ToList();
    }

    #endregion

    #region §4 ─ JSON 미리보기 패널 액션

    // ── 클립보드 복사 ────────────────────────────────────────────────
    private void BtnCopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TbJsonPreview.Text)) return;
        Clipboard.SetText(TbJsonPreview.Text);
        Log("📋 파일 내용을 클립보드에 복사했습니다.");
    }

    // ── JSON 들여쓰기 정렬 ───────────────────────────────────────────
    private void BtnFormatJson_Click(object sender, RoutedEventArgs e)
    {
        var raw = TbJsonPreview.Text;
        if (string.IsNullOrEmpty(raw)) return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var opts = new JsonWriterOptions { Indented = true };
            using var ms = new MemoryStream();
            using var w = new Utf8JsonWriter(ms, opts);
            doc.WriteTo(w);
            w.Flush();
            TbJsonPreview.Text = Encoding.UTF8.GetString(ms.ToArray());
            Log("✨ JSON 들여쓰기 정렬 완료.");
        }
        catch
        {
            Log("⚠ JSON 형식이 아니어서 정렬할 수 없습니다 (XML 파일은 정렬 불필요).");
        }
    }

    // ── JSON 저장 (SaveFileDialog) ───────────────────────────────────
    private void BtnSaveJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "장비 트리 JSON 저장",
                Filter = "JSON 파일|*.json",
                FileName = "devices.json",
                InitialDirectory = _strDefaultDir
            };

            if (dlg.ShowDialog() != true) return;

            Directory.CreateDirectory(Path.GetDirectoryName(dlg.FileName)!);
            _tree.SaveJson(dlg.FileName);

            // 저장 직후 미리보기 갱신
            var saved = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            TbJsonPreview.Text = saved;
            TbFileInfo.Text =
                $"  {Path.GetFileName(dlg.FileName)}  |  JSON  |  " +
                $"{new FileInfo(dlg.FileName).Length / 1024.0:F1} KB  (방금 저장됨)";
            TbFileInfo.Foreground = Brushes.LightGreen;
            TbFilePath.Text = dlg.FileName;
            _strLastLoadedPath = dlg.FileName;
            AddRecentFile(dlg.FileName);

            Log($"💾 JSON 저장: {dlg.FileName}");
            MainWindow.SetStatus($"저장 → {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { Log($"❌ 저장 실패: {ex.Message}"); }
    }

    #endregion

    #region §5 ─ XML 저장·로드

    private void BtnSaveXml_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "장비 트리 XML 저장",
                Filter = "XML 파일|*.xml",
                FileName = "devices.xml",
                InitialDirectory = _strDefaultDir
            };
            if (dlg.ShowDialog() != true) return;

            Directory.CreateDirectory(Path.GetDirectoryName(dlg.FileName)!);
            _tree.SaveXml(dlg.FileName);

            var saved = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            TbJsonPreview.Text = saved;
            TbFileInfo.Text =
                $"  {Path.GetFileName(dlg.FileName)}  |  XML  (방금 저장됨)";
            TbFileInfo.Foreground = Brushes.LightYellow;
            TbFilePath.Text = dlg.FileName;
            AddRecentFile(dlg.FileName);

            Log($"💾 XML 저장: {dlg.FileName}");
        }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    private void BtnLoadXml_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "장비 트리 XML 로드",
            Filter = "XML 파일|*.xml",
            InitialDirectory = Directory.Exists(_strDefaultDir)
                ? _strDefaultDir : AppDomain.CurrentDomain.BaseDirectory
        };
        if (dlg.ShowDialog() != true) return;
        TbFilePath.Text = dlg.FileName;
        LoadFileInternal(dlg.FileName);
    }

    private void BtnClearTree_Click(object sender, RoutedEventArgs e)
    {
        _tree.Clear();
        TvDevices.Items.Clear();
        _itemMap.Clear();
        TbNodeDetail.Clear();
        TbJsonPreview.Clear();
        TbFileInfo.Text = "파일 미로드";
        TbFileInfo.Foreground = Brushes.SlateGray;
        TbTreeStats.Text = "총 0 노드";
        Log("🧹 초기화 완료.");
    }

    #endregion

    #region §6 ─ 노드 추가

    private void CbNodeType_Changed(object sender, SelectionChangedEventArgs e) =>
        UpdatePropFields();

    private void UpdatePropFields()
    {
        if(PnlProps == null)
        {
            return;
        }

        PnlProps.Children.Clear();
        _propFields.Clear();

        var fields = CbNodeType.SelectedIndex switch
        {
            0 => new[] { ("location", "위치 (예: Building-A)") },
            1 => new[] { ("ip", "IP 주소"), ("port", "포트"), ("protocol", "프로토콜") },
            2 => new[] { ("address", "레지스터 주소"), ("scale", "스케일"), ("unit", "단위") },
            3 => new[] { ("address", "PLC 주소"), ("dataType", "데이터 타입") },
            _ => Array.Empty<(string, string)>()
        };

        foreach (var (key, hint) in fields)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = key,
                Style = (Style)FindResource("LblField"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var tb = new TextBox
            {
                Style = (Style)FindResource("TbBase"),
                Height = 28,
                ToolTip = hint,
                FontSize = 12
            };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(tb, 1);
            row.Children.Add(lbl);
            row.Children.Add(tb);

            _propFields.Add((new TextBox { Text = key }, tb));
            PnlProps.Children.Add(row);
        }

        TbNodeName.Text = CbNodeType.SelectedIndex switch
        {
            0 => "Line-1",
            1 => "PLC-001",
            2 => "TempSensor-01",
            3 => "M0.0",
            _ => ""
        };
    }

    private void BtnAddNode_Click(object sender, RoutedEventArgs e)
    {
        var name = TbNodeName.Text.Trim();
        if (string.IsNullOrEmpty(name)) { Log("⚠ 이름을 입력하세요."); return; }

        var parent = GetSelectedNode() ?? _tree.Root;
        var props = _propFields
            .Where(p => !string.IsNullOrEmpty(p.Val.Text.Trim()))
            .ToDictionary(p => p.Key.Text, p => p.Val.Text.Trim());

        ConfigNode node = CbNodeType.SelectedIndex switch
        {
            0 => _tree.AddGroup(name, props.GetValueOrDefault("location")),
            1 => _tree.AddDevice(parent, name,
                     props.GetValueOrDefault("ip"),
                     props.GetValueOrDefault("port"),
                     props.GetValueOrDefault("protocol")),
            2 => _tree.AddSensor(parent, name,
                     props.GetValueOrDefault("address"),
                     props.GetValueOrDefault("scale"),
                     props.GetValueOrDefault("unit")),
            3 => _tree.AddTag(parent, name,
                     props.GetValueOrDefault("address"),
                     props.GetValueOrDefault("dataType")),
            _ => _tree.AddNode(parent, name, NodeType.Other, props)
        };

        if (!string.IsNullOrEmpty(TbNodeDesc.Text))
            node.Description = TbNodeDesc.Text.Trim();

        Log($"➕ 추가: {node.Path}");
    }

    #endregion

    #region §7 ─ 샘플 트리 생성

    private void BtnBuildSample_Click(object sender, RoutedEventArgs e)
    {
        _tree.Clear();

        var line1 = _tree.AddGroup("Line-1", "Building-A-1F");

        var plc1 = _tree.AddDevice(line1, "PLC-001",
                        ip: "192.168.1.10", port: "502", protocol: "Modbus TCP");
        _tree.AddSensor(plc1, "TempSensor-01", address: "40001", scale: "0.1", unit: "°C");
        _tree.AddSensor(plc1, "PressureSensor", address: "40002", scale: "0.01", unit: "bar");
        _tree.AddTag(plc1, "Run_Coil", address: "M0.0", dataType: "Bool");
        _tree.AddTag(plc1, "Speed_Register", address: "D100", dataType: "Int16");

        // 테스트 추가
        var line1_1 = _tree.AddGroup(line1, "Line-1-1", "Building-A-1F");
        var plc101 = _tree.AddDevice(line1_1, "PLC-101",
                        ip: "192.168.1.10", port: "502", protocol: "Modbus TCP");
        _tree.AddSensor(plc101, "TempSensor-01", address: "40001", scale: "0.1", unit: "°C");
        _tree.AddSensor(plc101, "PressureSensor", address: "40002", scale: "0.01", unit: "bar");
        _tree.AddTag(plc101, "Run_Coil", address: "M0.0", dataType: "Bool");
        _tree.AddTag(plc101, "Speed_Register", address: "D100", dataType: "Int16");
        //

        var hmi1 = _tree.AddDevice(line1, "HMI-001",
                       ip: "192.168.1.20", port: "80", protocol: "HTTP");
        _tree.AddSensor(hmi1, "ScreenTemp", address: "0x0010", unit: "°C");

        var line2 = _tree.AddGroup("Line-2", "Building-A-2F");
        var plc2 = _tree.AddDevice(line2, "PLC-002",
                        ip: "192.168.1.11", port: "102", protocol: "EtherNet/IP");
        _tree.AddSensor(plc2, "FlowMeter", address: "AI0", scale: "0.001", unit: "L/min");
        _tree.AddSensor(plc2, "LevelSensor", address: "AI1", scale: "0.1", unit: "%");
        _tree.AddTag(plc2, "Pump1_Start", address: "Q0.0", dataType: "Bool");
        var srv = _tree.AddDevice(line2, "DataServer",
                      ip: "192.168.1.100", port: "5432", protocol: "PostgreSQL");
        srv.SetProperty("dbName", "scada_db");
        srv.Description = "라인-2 데이터 수집 서버";
        _tree.NotifyNodeChanged(srv, "Modified");

        var util = _tree.AddGroup("Utility", "Machine-Room");
        var ups = _tree.AddDevice(util, "UPS-001",
                       ip: "192.168.2.1", port: "161", protocol: "SNMP");
        _tree.AddSensor(ups, "BatteryLevel", address: "1.3.6.1", unit: "%");
        _tree.AddSensor(ups, "LoadPct", address: "1.3.6.2", unit: "%");

        RebuildTreeView();
        UpdateStats();

        // 샘플 생성 후 JSON 미리보기 갱신 (파일 저장 없이 인메모리 직렬화)
        RefreshJsonPreviewFromTree();
        Log("✅ 샘플 트리 생성 완료. JSON 미리보기가 갱신되었습니다.");
    }

    #endregion

    #region §8 ─ 선택 노드 편집

    private void BtnSetProp_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedNode();
        if (node is null) { Log("⚠ 트리에서 노드를 선택하세요."); return; }
        var key = TbPropKey.Text.Trim();
        var val = TbPropValue.Text.Trim();
        if (string.IsNullOrEmpty(key)) { Log("⚠ 키를 입력하세요."); return; }
        node.SetProperty(key, val);
        ShowNodeDetail(node);
        Log($"🔧 [{node.Name}] {key} = \"{val}\"");
    }

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedNode();
        if (node is null || node.Parent is null) { Log("⚠ 이동할 노드를 선택하세요."); return; }
        var siblings = node.Parent.Children;
        int idx = siblings.IndexOf(node);
        if (idx <= 0) { Log("⚠ 이미 첫 번째 노드입니다."); return; }
        siblings.RemoveAt(idx);
        siblings.Insert(idx - 1, node);
        node.Order = idx - 1;
        RebuildTreeView();
        Log($"🔼 이동: {node.Name} ({idx} → {idx - 1})");
    }

    private void BtnRemoveNode_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedNode();
        if (node is null) { Log("⚠ 삭제할 노드를 선택하세요."); return; }
        if (node.IsRoot) { Log("⚠ 루트 노드는 삭제할 수 없습니다."); return; }
        var name = node.Name;
        if (_tree.Remove(node))
        {
            TbNodeDetail.Clear();
            Log($"🗑 삭제: {name}");
        }
    }

    #endregion

    #region §9 ─ WPF TreeView 렌더링

    private void RebuildTreeView()
    {
        TvDevices.Items.Clear();
        _itemMap.Clear();

        foreach (var group in _tree.Root.Children)
            TvDevices.Items.Add(BuildTreeItem(group));
    }

    private TreeViewItem BuildTreeItem(ConfigNode node)
    {
        var icon = node.Type switch
        {
            NodeType.Group => "🏭",
            NodeType.Device => "🖥",
            NodeType.Sensor => "📡",
            NodeType.Tag => "🏷",
            _ => "⚙"
        };

        // IP 또는 address 요약 표시
        var summary = node.GetProperty("ip") ?? node.GetProperty("address") ?? string.Empty;
        if (!string.IsNullOrEmpty(summary)) summary = $"  [{summary}]";

        var header = $"{icon}  {node.Name}{summary}";
        if (!node.Enabled) header += "  [비활성]";

        var item = new TreeViewItem
        {
            Header = header,
            IsExpanded = true,
            Foreground = node.Type switch
            {
                NodeType.Group => Brushes.LightSkyBlue,
                NodeType.Device => Brushes.LightGreen,
                NodeType.Sensor => Brushes.Khaki,
                NodeType.Tag => Brushes.Salmon,
                _ => Brushes.Silver
            }
        };
        _itemMap[item] = node;

        foreach (var child in node.Children.OrderBy(c => c.Order))
            item.Items.Add(BuildTreeItem(child));

        return item;
    }

    private void TvDevices_SelectedItemChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        var node = GetSelectedNode();
        if (node is null) return;
        ShowNodeDetail(node);
    }

    /// <summary>
    /// 선택 노드 상세 정보를 오른쪽 패널에 표시합니다.
    /// </summary>
    private void ShowNodeDetail(ConfigNode node)
    {
        var sb = new StringBuilder();

        sb.AppendLine("── 노드 정보 ────────────────────────────");
        sb.AppendLine($"  ID       : {node.Id}");
        sb.AppendLine($"  이름     : {node.Name}");
        sb.AppendLine($"  유형     : {node.Type}");
        sb.AppendLine($"  활성     : {node.Enabled}");
        sb.AppendLine($"  Depth    : {node.Depth}");
        sb.AppendLine($"  Path     : {node.Path}");
        sb.AppendLine($"  자식 수  : {node.Children.Count}");

        if (node.Description is not null)
            sb.AppendLine($"  설명     : {node.Description}");

        if (node.Properties.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── 프로퍼티 ─────────────────────────────");
            foreach (var kv in node.Properties)
                sb.AppendLine($"  {kv.Key,-14}: {kv.Value}");
        }

        // 하위 노드 트리 다이어그램
        var descendants = node.Flatten().Skip(1).ToList();
        if (descendants.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── 하위 노드 ────────────────────────────");
            foreach (var n in descendants)
            {
                var indent = new string(' ', (n.Depth - node.Depth - 1) * 2);
                var nodeIcon = n.Type switch
                {
                    NodeType.Device => "🖥",
                    NodeType.Sensor => "📡",
                    NodeType.Tag => "🏷",
                    _ => "⚙"
                };
                // 핵심 프로퍼티 한 줄 요약
                var prop = n.GetProperty("ip")
                           ?? n.GetProperty("address")
                           ?? string.Empty;
                var propStr = string.IsNullOrEmpty(prop) ? "" : $"  [{prop}]";
                sb.AppendLine($"  {indent}{nodeIcon} {n.Name}{propStr}");
            }
        }

        TbNodeDetail.Text = sb.ToString();
    }

    private ConfigNode? GetSelectedNode()
    {
        if (TvDevices.SelectedItem is TreeViewItem item &&
            _itemMap.TryGetValue(item, out var node))
            return node;
        return null;
    }

    #endregion

    #region §10 ─ 내부 헬퍼

    /// <summary>
    /// 현재 메모리 트리를 JSON 직렬화하여 미리보기 패널에 표시합니다.
    /// (파일 저장 없이 인메모리 직렬화만 수행)
    /// </summary>
    private void RefreshJsonPreviewFromTree()
    {
        try
        {
            var json = _tree.ToJson();
            TbJsonPreview.Text = json;
            TbFileInfo.Text =
                $"  (메모리 직렬화)  |  JSON  |  " +
                $"{Encoding.UTF8.GetByteCount(json) / 1024.0:F1} KB";
            TbFileInfo.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TbJsonPreview.Text = $"[직렬화 오류]\n{ex.Message}";
        }
    }

    private void UpdateStats()
    {
        var groups = _tree.FindAll(NodeType.Group).Count();
        var devices = _tree.FindAll(NodeType.Device).Count();
        var sensors = _tree.FindAll(NodeType.Sensor).Count();
        var tags = _tree.FindAll(NodeType.Tag).Count();
        TbTreeStats.Text =
            $"총 {_tree.Count}개  🏭{groups}  🖥{devices}  📡{sensors}  🏷{tags}";
    }

    private void Log(string msg)
    {
        TbLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}]  {msg}\n");
        TbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

    #endregion
}

// ── file-scoped string 확장 ──────────────────────────────────────────────
file static class StringEx
{
    public static string Repeat(this string s, int count) =>
        count <= 0 ? string.Empty : string.Concat(Enumerable.Repeat(s, count));
}