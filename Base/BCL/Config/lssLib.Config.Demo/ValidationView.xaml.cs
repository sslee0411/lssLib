// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/ValidationView.xaml.cs
//  탭⑤: ConfigSchema + ConfigValidator 데모
// ══════════════════════════════════════════════════════════════════════════
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using lssLib.Config.Validation;
using ValidationResult = lssLib.Config.Validation.ValidationResult;

namespace lssLib.Config.Demo.Views;

public partial class ValidationView : UserControl
{
    #region §1 ─ 필드

    private readonly ConfigManager _cfg = ConfigManager.CreateNew();
    private ConfigSchema? _schema;

    #endregion

    #region §2 ─ 초기화

    public ValidationView()
    {
        InitializeComponent();
        Log("ValidationView 초기화 완료.");
        Log("① 스키마 프리셋 선택 → ② 값 추가/수정 → ③ [검증 실행]");
        BtnPresetNetwork_Click(this, new RoutedEventArgs());
    }

    #endregion

    #region §3 ─ 프리셋

    private void BtnPresetNetwork_Click(object sender, RoutedEventArgs e)
    {
        _schema = new ConfigSchema()
            .Require("Network", "Host", ConfigValueType.IpAddress, description: "서버 IP 주소")
            .Require("Network", "Port", ConfigValueType.Port, description: "통신 포트")
            .Require("Network", "Timeout", ConfigValueType.Int,
                range: (100, 30_000), description: "타임아웃(ms)")
            .Require("Network", "Protocol", ConfigValueType.Enum,
                allowedValues: new[] { "Modbus", "EtherNet/IP", "PROFINET", "OPC-UA" })
            .Optional("Network", "Retry", ConfigValueType.Int,
                defaultValue: "3", range: (0, 10), description: "재시도 횟수");

        _cfg.Clear();
        _cfg.Set("Network", "Host", "192.168.1.100");
        _cfg.Set("Network", "Port", "502");
        _cfg.Set("Network", "Timeout", "5000");
        _cfg.Set("Network", "Protocol", "Modbus");

        RefreshSchemaView();
        RefreshResultView();
        Log("🌐 네트워크 스키마 프리셋 로드 완료. [Retry]는 선택 필드(기본값 3).");
    }

    private void BtnPresetDb_Click(object sender, RoutedEventArgs e)
    {
        _schema = new ConfigSchema()
            .Require("Database", "Server", ConfigValueType.NonEmptyString, description: "DB 서버 주소")
            .Require("Database", "Port", ConfigValueType.Port)
            .Require("Database", "Name", ConfigValueType.NonEmptyString, description: "데이터베이스 이름")
            .Require("Database", "User", ConfigValueType.NonEmptyString)
            .Require("Database", "Password", ConfigValueType.NonEmptyString, description: "암호화 권장")
            .Optional("Database", "PoolSize", ConfigValueType.Int,
                defaultValue: "10", range: (1, 200))
            .Optional("Database", "SslMode", ConfigValueType.Enum,
                defaultValue: "Prefer",
                allowedValues: new[] { "Disable", "Prefer", "Require" });

        _cfg.Clear();
        _cfg.Set("Database", "Server", "db.local");
        _cfg.Set("Database", "Port", "5432");
        _cfg.Set("Database", "Name", "scada_db");
        _cfg.Set("Database", "User", "admin");
        _cfg.Set("Database", "Password", "secret");

        RefreshSchemaView();
        RefreshResultView();
        Log("🗄 DB 스키마 프리셋 로드 완료. [PoolSize], [SslMode] 선택 필드.");
    }

    private void BtnPresetApp_Click(object sender, RoutedEventArgs e)
    {
        _schema = new ConfigSchema()
            .Require("App", "Name", ConfigValueType.NonEmptyString)
            .Require("App", "Version", ConfigValueType.SemVer, description: "예: 1.2.3")
            .Require("App", "DataDir", ConfigValueType.DirectoryPath)
            .Optional("App", "Debug", ConfigValueType.Bool, defaultValue: "false")
            .Optional("App", "LogLevel", ConfigValueType.Enum,
                defaultValue: "Info",
                allowedValues: new[] { "Debug", "Info", "Warn", "Error", "Fatal" })
            .Optional("App", "MaxRetry", ConfigValueType.Int,
                defaultValue: "3", range: (0, 20))
            .Custom("App", "ApiKey",
                v => v.Length >= 16 ? null : "ApiKey 는 최소 16자 이상이어야 합니다.",
                required: false, defaultValue: "");

        _cfg.Clear();
        _cfg.Set("App", "Name", "IIoT Monitor");
        _cfg.Set("App", "Version", "2.1.0");
        _cfg.Set("App", "DataDir", @"C:\Data");
        _cfg.Set("App", "Debug", "false");
        _cfg.Set("App", "ApiKey", "short");      // 의도적 짧은 값

        RefreshSchemaView();
        RefreshResultView();
        Log("⚙ 앱 설정 스키마 프리셋 로드 완료. [ApiKey] 길이 검증 포함.");
    }

    #endregion

    #region §4 ─ 값 입력

    private void BtnVSet_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbVSection.Text.Trim();
        var key = TbVKey.Text.Trim();
        var val = TbVValue.Text.Trim();
        if (string.IsNullOrEmpty(sec) || string.IsNullOrEmpty(key)) return;
        _cfg.Set(sec, key, val);
        RefreshResultView();
        Log($"➕ [{sec}] {key} = \"{val}\"");
    }

    private void BtnVSetBad_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbVSection.Text.Trim();
        var key = TbVKey.Text.Trim();
        if (string.IsNullOrEmpty(key)) return;

        // 유형별 의도적 오류값
        var badVal = key.ToUpperInvariant() switch
        {
            "PORT" => "99999",
            "HOST" => "not-an-ip",
            "TIMEOUT" => "-500",
            "VERSION" => "bad-version",
            "DEBUG" => "maybe",
            "LOGLEVEL" => "VERBOSE",
            "APIKEY" => "short",
            _ => ""     // 빈 문자열 (NonEmptyString 위반)
        };

        _cfg.Set(sec, key, badVal);
        RefreshResultView();
        Log($"⚠ 의도적 오류값 삽입: [{sec}] {key} = \"{badVal}\"");
    }

    private void BtnVDelete_Click(object sender, RoutedEventArgs e)
    {
        var sec = TbVSection.Text.Trim();
        var key = TbVKey.Text.Trim();
        _cfg.Remove(sec, key);
        RefreshResultView();
        Log($"🗑 키 삭제: [{sec}] {key}");
    }

    #endregion

    #region §5 ─ 검증 실행

    private void BtnValidate_Click(object sender, RoutedEventArgs e)
    {
        if (_schema is null) { Log("⚠ 스키마를 먼저 선택하세요."); return; }

        var result = _cfg.Validate(_schema, applyDefaults: false);
        if (result != null)
        {
            ShowResult(result);
            Log($"검증 완료 → {(result.IsValid ? "✅ 성공" : $"❌ 실패 ({result.Errors.Count}개 오류)")}");
            MainWindow.SetStatus(result.IsValid ? "검증 성공" : $"검증 실패 — {result.Errors.Count}개 오류");
        }
    }

    private void BtnThrow_Click(object sender, RoutedEventArgs e)
    {
        if (_schema is null) { Log("⚠ 스키마를 먼저 선택하세요."); return; }
        try
        {
            _cfg.ValidateOrThrow(_schema);
            Log("✅ ValidateOrThrow — 예외 없음 (모든 규칙 통과)");
        }
        catch (ConfigValidationException ex)
        {
            Log($"🔥 ConfigValidationException 발생:");
            foreach (var err in ex.Errors)
                Log($"   {err}");
        }
    }

    private void BtnApplyDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (_schema is null) { Log("⚠ 스키마를 먼저 선택하세요."); return; }

        var result = _cfg.Validate(_schema, applyDefaults: true);
        ShowResult(result);

        if (result.AppliedDefaults.Count > 0)
        {
            Log($"⚙ 기본값 자동 적용 ({result.AppliedDefaults.Count}개):");
            foreach (var (sec, key, def) in result.AppliedDefaults)
                Log($"   [{sec}] {key} = \"{def}\"  (기본값)");
        }
        else
            Log("ℹ 적용할 기본값 없음 (모든 선택 키가 이미 존재).");
    }

    private void BtnVClear_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        TbValidationResult.Clear();
        ResetResultBadge();
        Log("🧹 초기화.");
    }

    #endregion

    #region §6 ─ 내부 헬퍼

    private void ShowResult(ValidationResult result)
    {
        if(result == null)
        {
            Log("⚠ 검증 결과가 없습니다.");
            return;
        }

        var sb = new StringBuilder();

        // 결과 배지
        if (result.IsValid)
        {
            BdResult.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));
            TbResultBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
            TbResultBadge.Text = $"✅  검증 성공  (규칙 {result.RuleCount}개 통과)";
        }
        else
        {
            BdResult.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            TbResultBadge.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            TbResultBadge.Text = $"❌  검증 실패  ({result.Errors.Count}개 오류)";
        }

        // 상세 결과
        sb.AppendLine($"── 검증 결과  (규칙={result.RuleCount}) ──────────────────────");
        sb.AppendLine();

        if (result.Errors.Count > 0)
        {
            sb.AppendLine("  오류 목록:");
            foreach (var err in result.Errors)
                sb.AppendLine($"  ✗  {err}");
            sb.AppendLine();
        }

        if (result.AppliedDefaults.Count > 0)
        {
            sb.AppendLine("  적용된 기본값:");
            foreach (var (sec, key, def) in result.AppliedDefaults)
                sb.AppendLine($"  ℹ  [{sec}] {key} = \"{def}\"");
            sb.AppendLine();
        }

        sb.AppendLine("── 현재 설정 값 ─────────────────────────────────────");
        foreach (var section in _cfg.Store.GetSections())
        {
            sb.AppendLine($"  [{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
                sb.AppendLine($"    {entry.Key,-16} = {entry.Value}");
        }

        TbValidationResult.Text = sb.ToString();
    }

    private void RefreshSchemaView()
    {
        if (_schema is null) return;
        var sb = new StringBuilder();
        sb.AppendLine($"  총 {_schema.Count}개 규칙");
        sb.AppendLine();
        foreach (var rule in _schema.Rules)
        {
            var req = rule.Required ? "필수" : "선택";
            sb.Append($"  {(rule.Required ? "●" : "○")}  [{rule.Section}] {rule.Key}");
            sb.Append($"  type={rule.ValueType}  ({req})");
            if (rule.Min.HasValue || rule.Max.HasValue)
                sb.Append($"  range=({rule.Min}~{rule.Max})");
            if (rule.DefaultValue is not null)
                sb.Append($"  default=\"{rule.DefaultValue}\"");
            if (rule.AllowedValues is not null)
                sb.Append($"  [{string.Join("|", rule.AllowedValues)}]");
            sb.AppendLine();
        }
        TbSchemaRules.Text = sb.ToString();
    }

    private void RefreshResultView()
    {
        var sb = new StringBuilder();
        sb.AppendLine("── 현재 설정 ─────────────────────────────────────────");
        foreach (var section in _cfg.Store.GetSections())
        {
            sb.AppendLine($"  [{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
                sb.AppendLine($"    {entry.Key,-16} = {entry.Value}");
        }
        TbValidationResult.Text = sb.ToString();
    }

    private void ResetResultBadge()
    {
        BdResult.Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        TbResultBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        TbResultBadge.Text = "검증 전";
    }

    private void Log(string msg)
    {
        TbLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}]  {msg}\n");
        TbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

    #endregion
}