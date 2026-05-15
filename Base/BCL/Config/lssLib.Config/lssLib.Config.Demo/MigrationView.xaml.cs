// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/MigrationView.xaml.cs
//  탭⑦: ConfigMigration 버전 이전 + ConfigProfileManager 환경별 프로파일
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using lssLib.Config.Migration;
using lssLib.Config.Profile;

namespace lssLib.Config.Demo.Views;

public partial class MigrationView : UserControl
{
    #region §1 ─ 필드

    private readonly ConfigManager _cfg = ConfigManager.CreateNew();
    private readonly ConfigProfileManager _profiles = new();
    private static readonly string _strCfgDir;

    #endregion

    #region §2 ─ 초기화

    static MigrationView()
    {
        _strCfgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
    }

    public MigrationView()
    {
        InitializeComponent();

        RegisterMigrations();
        RegisterProfiles();
        CreateSampleFiles();

        Log("MigrationView 초기화 완료.");
        Log("① 버전별 샘플 설정 로드 → ② 마이그레이션 실행 → 결과 확인");
        Log("③ 프로파일 버튼으로 환경별 설정 자동 병합 확인");
    }

    #endregion

    #region §3 ─ 마이그레이션 규칙 등록

    private void RegisterMigrations()
    {
        // 탭 재진입 시 정적 레지스트리 중복 등록 방지
        ConfigMigration.ClearAll();

        // ── v1.0 → v2.0 ─────────────────────────────────────────
        ConfigMigration.Register("1.0", "2.0", rules =>
        {
            // 키 이름 변경
            rules.Rename("Network", "ServerIP", "Host");
            rules.Rename("Network", "Timeout_ms", "Timeout");

            // 섹션 이동 + 암호화 플래그
            rules.Move("DB", "Password", "Credentials", "DbPassword", isEncrypted: true);
            rules.Move("DB", "User", "Credentials", "DbUser");

            // 구버전 키 삭제
            rules.Delete("Legacy", "OldFlag");
            rules.Delete("Legacy", "CompatMode");

            // 신규 키 추가 (없으면만)
            rules.Add("App", "LogLevel", "Info");
            rules.Add("App", "Version", "2.0.0");
        });

        // ── v2.0 → v3.0 ─────────────────────────────────────────
        ConfigMigration.Register("2.0", "3.0", rules =>
        {
            // 포트를 문자열 → 정수 확인용 Transform
            rules.Transform("Network", "Port", v =>
                int.TryParse(v, out var p) ? p.ToString() : "502");

            // 모니터링 섹션 추가
            rules.Add("Monitor", "Enabled", "true");
            rules.Add("Monitor", "Interval", "5000");
            rules.Add("Monitor", "AlertMail", "ops@company.com");

            // 버전 갱신
            rules.Rename("App", "Version", "Version");  // 실제로는 Transform
            rules.Transform("App", "Version", _ => "3.0.0");
        });
    }

    #endregion

    #region §4 ─ 프로파일 등록 + 샘플 파일 생성

    private void RegisterProfiles()
    {
        _profiles.Define("development",
            baseFile: Path.Combine(_strCfgDir, "base.json"),
            envFile: Path.Combine(_strCfgDir, "development.json"),
            localFile: Path.Combine(_strCfgDir, "local.json"),
            description: "개발 환경 — 상세 로그, 로컬 DB");

        _profiles.Define("production",
            baseFile: Path.Combine(_strCfgDir, "base.json"),
            envFile: Path.Combine(_strCfgDir, "production.json"),
            description: "운영 환경 — 최소 로그, 운영 DB");

        _profiles.Define("staging",
            baseFile: Path.Combine(_strCfgDir, "base.json"),
            envFile: Path.Combine(_strCfgDir, "staging.json"),
            description: "스테이징 환경 — 운영 미러");

        _profiles.ProfileSwitched += (name, store) =>
            Dispatcher.InvokeAsync(() =>
            {
                TbActiveProfile.Text = name;
                _cfg.Store.Merge(store, overwrite: true);
                RefreshStoreView();
                Log($"🔄 프로파일 전환: [{name}]  항목={store.Count}개");
                MainWindow.SetStatus($"프로파일: {name}");
            });
    }

    private void CreateSampleFiles()
    {
        Directory.CreateDirectory(_strCfgDir);

        // base.json
        WriteIfNotExists("base.json", """
{
  "Network": { "Host": "192.168.1.100", "Port": "502", "Timeout": "5000" },
  "App":     { "Name": "IIoT Monitor",  "Version": "3.0.0", "LogLevel": "Info" },
  "Monitor": { "Enabled": "true",       "Interval": "5000" }
}
""");

        // development.json
        WriteIfNotExists("development.json", """
{
  "Network": { "Host": "127.0.0.1" },
  "App":     { "LogLevel": "Debug", "Debug": "true" },
  "Database":{ "Server": "localhost", "Port": "5432", "Name": "scada_dev" }
}
""");

        // production.json
        WriteIfNotExists("production.json", """
{
  "Network": { "Host": "10.0.0.50", "Timeout": "3000" },
  "App":     { "LogLevel": "Warn",  "Debug": "false" },
  "Database":{ "Server": "db-prod.internal", "Port": "5432", "Name": "scada_prod" }
}
""");

        // staging.json
        WriteIfNotExists("staging.json", """
{
  "Network": { "Host": "10.0.1.50" },
  "App":     { "LogLevel": "Info",  "Debug": "false" },
  "Database":{ "Server": "db-staging.internal", "Port": "5432", "Name": "scada_staging" }
}
""");
    }

    private static void WriteIfNotExists(string fileName, string content)
    {
        var path = Path.Combine(_strCfgDir, fileName);
        if (!File.Exists(path))
            File.WriteAllText(path, content.Trim(), System.Text.Encoding.UTF8);
    }

    #endregion

    #region §5 ─ 마이그레이션 액션

    private void BtnLoadV1_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        // v1.0 형식 설정값 직접 주입
        _cfg.Set("Network", "ServerIP", "192.168.1.100");   // 구버전 키
        _cfg.Set("Network", "Port", "502");
        _cfg.Set("Network", "Timeout_ms", "5000");             // 구버전 키
        _cfg.Set("DB", "User", "admin");            // 이동 대상
        _cfg.Set("DB", "Password", "secret");           // 이동+암호화 대상
        _cfg.Set("Legacy", "OldFlag", "1");                // 삭제 대상
        _cfg.Set("Legacy", "CompatMode", "true");             // 삭제 대상
        _cfg.Set("Meta", "Version", "1.0");

        RefreshStoreView();
        Log("📂 v1.0 설정 로드 완료. 구버전 키(ServerIP, Timeout_ms, DB.Password 등) 포함.");
    }

    private void BtnLoadV2_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        _cfg.Set("Network", "Host", "192.168.1.100");
        _cfg.Set("Network", "Port", "502");
        _cfg.Set("Network", "Timeout", "5000");
        _cfg.Set("Credentials", "DbUser", "admin");
        _cfg.Set("Credentials", "DbPassword", "secret");
        _cfg.Set("App", "LogLevel", "Info");
        _cfg.Set("App", "Version", "2.0.0");
        _cfg.Set("Meta", "Version", "2.0");

        RefreshStoreView();
        Log("📂 v2.0 설정 로드 완료.");
    }

    private void BtnMigrate_Click(object sender, RoutedEventArgs e)
    {
        var from = (CbFromVersion.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.0";
        var to = (CbToVersion.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "2.0";

        try
        {
            var report = ConfigMigration.Migrate(_cfg.Store, from, to);
            RefreshStoreView();
            Log($"🔀 {report}");
            foreach (var step in report.Steps)
            {
                Log($"   단계: {step.FromVersion} → {step.ToVersion}  ({step.AppliedRules.Count}개 규칙)");
                foreach (var rule in step.AppliedRules)
                    Log($"     · {rule}");
            }
            MainWindow.SetStatus($"마이그레이션 완료: {from} → {to}");
        }
        catch (Exception ex)
        {
            Log($"❌ 마이그레이션 실패: {ex.Message}");
        }
    }

    private void BtnMigrateAll_Click(object sender, RoutedEventArgs e)
    {
        // v1.0 데이터 먼저 로드
        BtnLoadV1_Click(this, new RoutedEventArgs());
        Log("─── 전체 마이그레이션 1.0 → 3.0 ───────────────────────");

        try
        {
            var report = ConfigMigration.Migrate(_cfg.Store, "1.0", "3.0");
            RefreshStoreView();
            Log($"✅ {report}");
        }
        catch (Exception ex)
        {
            Log($"❌ {ex.Message}");
        }
    }

    private void BtnShowRules_Click(object sender, RoutedEventArgs e)
    {
        Log("─── 등록된 마이그레이션 규칙 ──────────────────────────");
        foreach (var path in ConfigMigration.RegisteredPaths)
        {
            Log($"   {path.From} → {path.To}");
        }
    }

    #endregion

    #region §6 ─ 프로파일 액션

    private void BtnProfileDev_Click(object sender, RoutedEventArgs e)
    {
        try { _profiles.Activate("development"); }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    private void BtnProfileProd_Click(object sender, RoutedEventArgs e)
    {
        try { _profiles.Activate("production"); }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    private void BtnProfileStaging_Click(object sender, RoutedEventArgs e)
    {
        try { _profiles.Activate("staging"); }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    #endregion

    #region §7 ─ 내부 헬퍼

    private void BtnClearStore_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        TbStoreView.Clear();
        TbActiveProfile.Text = "없음";
        Log("🧹 저장소 초기화.");
    }

    private void BtnDumpStore_Click(object sender, RoutedEventArgs e) => RefreshStoreView();

    private void RefreshStoreView()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  총 {_cfg.Store.Count}개 항목");
        sb.AppendLine();
        foreach (var section in _cfg.Store.GetSections())
        {
            sb.AppendLine($"  [{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
            {
                var flag = entry.IsEncrypted ? "  🔐" : "";
                sb.AppendLine($"    {entry.Key,-18} = {entry.Value}{flag}");
            }
        }
        TbStoreView.Text = sb.ToString();
    }

    private void Log(string msg)
    {
        TbLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}]  {msg}\n");
        TbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

    #endregion
}