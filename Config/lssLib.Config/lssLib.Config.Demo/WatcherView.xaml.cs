// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/WatcherView.xaml.cs
//  탭③: FileSystemWatcher 런타임 변경 감지 데모
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace lssLib.Config.Demo.Views;

public partial class WatcherView : UserControl
{
    #region §1 ─ 초기화

    private readonly ConfigManager _cfg = ConfigManager.CreateNew();
    private int _detectCount = 0;
    private bool _watching = false;
    private Timer? _autoTimer;

    public WatcherView()
    {
        InitializeComponent();

        // 초기 파일 준비
        Loaded += (_, _) => InitFile();

        Log("WatcherView 초기화 완료.");
        Log("① [감시 시작] → ② 파일 편집·저장 또는 [항목 추가 후 저장]");
        Log("   → 변경 감지 이벤트가 자동으로 발생합니다.");
    }

    private void InitFile()
    {
        try
        {
            var path = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (!File.Exists(path))
            {
                // 초기 파일 생성
                _cfg.Set("Runtime", "Counter", "0");
                _cfg.Set("Runtime", "StartedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _cfg.Set("Monitor", "Interval", "1000");
                _cfg.Set("Monitor", "Enabled", "true");
                _cfg.Save(path, ConfigFormat.Json);
                Log($"초기 파일 생성: {path}");
            }
            else
            {
                _cfg.Load(path, ConfigFormat.Json);
                Log($"기존 파일 로드: {path}");
            }

            RefreshConfigView();
        }
        catch (Exception ex) { Log($"❌ 초기화 실패: {ex.Message}"); }
    }

    #endregion

    #region §2 ─ 감시 제어

    private void BtnStartWatch_Click(object sender, RoutedEventArgs e)
    {
        if (_watching) return;
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                Log($"⚠ 파일 없음: {path}");
                return;
            }

            _cfg.ConfigChanged += OnConfigChanged;
            _cfg.StartWatch(path);
            _watching = true;

            BtnStartWatch.IsEnabled = false;
            BtnStopWatch.IsEnabled = true;
            Log($"▶ 감시 시작: {Path.GetFileName(path)}");
            Log($"   디바운스: {TbDebounce.Text} ms");
            MainWindow.SetStatus($"감시 중 → {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Log($"❌ 감시 시작 실패: {ex.Message}"); }
    }

    private void BtnStopWatch_Click(object sender, RoutedEventArgs e)
    {
        _cfg.StopWatch();
        _cfg.ConfigChanged -= OnConfigChanged;
        _watching = false;

        _autoTimer?.Dispose();
        _autoTimer = null;

        BtnStartWatch.IsEnabled = true;
        BtnStopWatch.IsEnabled = false;
        Log("⏹ 감시 중단.");
        MainWindow.SetStatus("감시 중단됨");
    }

    private void BtnReloadFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) { Log("⚠ 파일 없음"); return; }
            _cfg.Load(path, ConfigFormat.Json);
            RefreshConfigView();
            Log($"🔄 수동 새로고침: {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    #endregion

    #region §3 ─ 파일 수정 (변경 유발)

    private void BtnAddAndSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sec = TbWSection.Text.Trim();
            var key = TbWKey.Text.Trim();
            var val = TbWValue.Text.Trim();

            _cfg.Set(sec, key, val);
            _cfg.Save(GetFilePath(), ConfigFormat.Json);
            RefreshConfigView();
            Log($"💾 저장: [{sec}] {key} = \"{val}\"");
        }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    private void BtnAutoIncrement_Click(object sender, RoutedEventArgs e)
    {
        if (_autoTimer is not null)
        {
            _autoTimer.Dispose();
            _autoTimer = null;
            Log("⏹ 자동 증가 중단");
            return;
        }

        int counter = int.TryParse(_cfg.Get("Runtime", "Counter"), out var c) ? c : 0;
        Log("🔁 자동 증가 시작 (1초 간격, 10회)");

        int count = 0;
        _autoTimer = new Timer(_ =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                count++;
                counter++;
                _cfg.Set("Runtime", "Counter", counter.ToString());
                _cfg.Set("Runtime", "LastUpdated", DateTime.Now.ToString("HH:mm:ss.fff"));
                _cfg.Save(GetFilePath(), ConfigFormat.Json);
                TbWValue.Text = counter.ToString();

                if (count >= 10)
                {
                    _autoTimer?.Dispose();
                    _autoTimer = null;
                    Log("✅ 자동 증가 완료 (10회)");
                }
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    #endregion

    #region §4 ─ 변경 감지 콜백

    private void OnConfigChanged(string path, ConfigStore store)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _detectCount++;
            TbDetectCount.Text = _detectCount.ToString();
            TbLastDetect.Text = $"{DateTime.Now:HH:mm:ss.fff}\n{Path.GetFileName(path)}";

            RefreshConfigView();
            Log($"🔔 변경 감지 #{_detectCount:D3}  파일: {Path.GetFileName(path)}");
            Log($"   Counter = {store.Get("Runtime", "Counter") ?? "-"}");
            MainWindow.SetStatus($"변경 감지 #{_detectCount}  [{Path.GetFileName(path)}]");
        });
    }

    #endregion

    #region §5 ─ 내부 헬퍼

    private string GetFilePath()
    {
        var raw = TbWatchPath.Text.Trim();
        if (string.IsNullOrEmpty(raw)) raw = "config/watch_demo.json";
        return Path.IsPathRooted(raw)
            ? raw
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, raw);
    }

    private void RefreshConfigView()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
                TbCurrentConfig.Text = File.ReadAllText(path);
        }
        catch { /* 파일 잠금 등 무시 */ }
    }

    private void BtnBrowseWatch_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "감시 파일 선택",
            Filter = "JSON|*.json|INI|*.ini|XML|*.xml"
        };
        if (dlg.ShowDialog() == true)
        {
            TbWatchPath.Text = dlg.FileName;
            Log($"감시 파일 변경: {dlg.FileName}");
        }
    }

    private void Log(string msg)
    {
        TbLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}]  {msg}\n");
        TbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

    #endregion
}