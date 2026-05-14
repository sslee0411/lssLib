// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/BasicRwView.xaml.cs
//  탭①: INI / JSON / XML 설정 읽기·쓰기 데모
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace lssLib.Config.Demo.Views;

public partial class BasicRwView : UserControl
{
    #region §1 ─ 초기화

    // ConfigManager 싱글톤 참조
    private readonly ConfigManager _cfg = ConfigManager.Instance;

    public BasicRwView()
    {
        InitializeComponent();
        // 시작 시 샘플 데이터 채우기
        LoadSampleData();
        Log("BasicRwView 초기화 완료. 샘플 데이터가 로드되었습니다.");
    }

    #endregion

    #region §2 ─ 샘플 데이터

    private void LoadSampleData()
    {
        _cfg.Clear();

        // 네트워크 설정
        _cfg.Set("Network", "Host", "192.168.1.100");
        _cfg.Set("Network", "Port", "502");
        _cfg.Set("Network", "Timeout", "5000");
        _cfg.Set("Network", "Retry", "3");

        // DB 설정
        _cfg.Set("Database", "Server", "db.local");
        _cfg.Set("Database", "Name", "scada_db");
        _cfg.Set("Database", "Port", "5432");
        _cfg.Set("Database", "PoolSize", "10");

        // 앱 설정
        _cfg.Set("App", "Name", "IIoT Monitor");
        _cfg.Set("App", "Version", "2.1.0");
        _cfg.Set("App", "Debug", "false");
        _cfg.Set("App", "MaxRetry", "5");
        _cfg.Set("App", "SampleRate", "0.5");
        _cfg.Set("App", "LogLevel", "Info");

        DumpPreview();
    }

    private void BtnSampleNetwork_Click(object sender, RoutedEventArgs e)
    {
        TbSection.Text = "Network";
        TbKey.Text = "Host";
        TbValue.Text = "192.168.1.200";
        Log("네트워크 샘플 데이터를 입력란에 채웠습니다.");
    }

    private void BtnSampleDb_Click(object sender, RoutedEventArgs e)
    {
        TbSection.Text = "Database";
        TbKey.Text = "Server";
        TbValue.Text = "db-primary.local";
        Log("DB 샘플 데이터를 입력란에 채웠습니다.");
    }

    private void BtnSampleApp_Click(object sender, RoutedEventArgs e)
    {
        TbSection.Text = "App";
        TbKey.Text = "MaxRetry";
        TbValue.Text = "10";
        Log("앱 설정 샘플 데이터를 입력란에 채웠습니다.");
    }

    #endregion

    #region §3 ─ 파일 저장 / 로드

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "설정 파일 선택",
            Filter = "모든 설정 파일|*.ini;*.json;*.xml|INI|*.ini|JSON|*.json|XML|*.xml"
        };
        if (dlg.ShowDialog() == true)
        {
            TbFilePath.Text = dlg.FileName;
            Log($"파일 선택됨: {dlg.FileName}");
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = GetFilePath();
            var format = GetFormat();
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                                      ?? Directory.GetCurrentDirectory());
            _cfg.Save(path, format);
            DumpPreview();
            Log($"✅ 저장 완료: {path}  [{format}]");
            MainWindow.SetStatus($"저장 완료 → {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Log($"❌ 저장 실패: {ex.Message}"); }
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = GetFilePath();
            var format = GetFormat();
            if (!File.Exists(path))
            {
                Log($"⚠ 파일 없음: {path}  (먼저 [저장]을 실행하세요)");
                return;
            }
            _cfg.Load(path, format);
            DumpPreview();
            Log($"✅ 로드 완료: {path}  [{format}]  총 {_cfg.Store.Count}개 항목");
            MainWindow.SetStatus($"로드 완료 → {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Log($"❌ 로드 실패: {ex.Message}"); }
    }

    #endregion

    #region §4 ─ CRUD 액션

    private void BtnSet_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbSection.Text.Trim();
        var key = TbKey.Text.Trim();
        var val = TbValue.Text.Trim();
        if (string.IsNullOrEmpty(sec) || string.IsNullOrEmpty(key)) { Log("⚠ 섹션·키를 입력하세요."); return; }

        _cfg.Set(sec, key, val);
        DumpPreview();
        Log($"➕ Set [{sec}] {key} = \"{val}\"");
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbSection.Text.Trim();
        var key = TbKey.Text.Trim();
        if (_cfg.Remove(sec, key))
        {
            DumpPreview();
            Log($"🗑 Remove [{sec}] {key}");
        }
        else
            Log($"⚠ 키 없음: [{sec}] {key}");
    }

    private void BtnGet_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbSection.Text.Trim();
        var key = TbKey.Text.Trim();
        var val = _cfg.Get(sec, key);
        Log(val is not null
            ? $"🔍 Get [{sec}] {key} = \"{val}\""
            : $"⚠ 키 없음: [{sec}] {key}");
    }

    private void BtnDump_Click(object sender, RoutedEventArgs e)
    {
        Log("─── 전체 설정 목록 ───────────────────────────────");
        foreach (var section in _cfg.Store.GetSections())
        {
            Log($"  [{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
                Log($"    {entry.Key} = {entry.Value}{(entry.IsEncrypted ? "  🔐" : "")}");
        }
        Log($"  총 {_cfg.Store.Count}개 항목");
        Log("──────────────────────────────────────────────────");
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        TbFilePreview.Clear();
        Log("🧹 ConfigManager 초기화 완료.");
    }

    #endregion

    #region §5 ─ 타입 변환 조회

    private void BtnGetInt_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbQSection.Text.Trim();
        var key = TbQKey.Text.Trim();
        var v = _cfg.GetInt(sec, key, -1);
        Log($"GetInt  [{sec}] {key} = {v}  (int)");
    }

    private void BtnGetBool_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbQSection.Text.Trim();
        var key = TbQKey.Text.Trim();
        var v = _cfg.GetBool(sec, key, false);
        Log($"GetBool [{sec}] {key} = {v}  (bool)");
    }

    private void BtnGetDouble_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbQSection.Text.Trim();
        var key = TbQKey.Text.Trim();
        var v = _cfg.GetDouble(sec, key, 0.0);
        Log($"GetDouble [{sec}] {key} = {v:F4}  (double)");
    }

    #endregion

    #region §6 ─ 내부 헬퍼

    private string GetFilePath()
    {
        var raw = TbFilePath.Text.Trim();
        if (string.IsNullOrEmpty(raw)) raw = "config/app.json";
        return Path.IsPathRooted(raw)
            ? raw
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, raw);
    }

    private ConfigFormat GetFormat() => CbFormat.SelectedIndex switch
    {
        0 => ConfigFormat.Ini,
        2 => ConfigFormat.Xml,
        _ => ConfigFormat.Json
    };

    private void DumpPreview()
    {
        try
        {
            var path = GetFilePath();
            var format = GetFormat();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            // 현재 _cfg 내용을 저장 후 파일 텍스트 읽어서 미리보기
            _cfg.Save(path, format);
            TbFilePreview.Text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            TbFilePreview.Text = $"[미리보기 실패]\n{ex.Message}";
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