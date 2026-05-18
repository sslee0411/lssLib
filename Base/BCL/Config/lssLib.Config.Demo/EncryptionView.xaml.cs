// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/EncryptionView.xaml.cs
//  탭②: AES-256-GCM 암호화 설정값 데모
// ══════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Windows;
using System.Windows.Controls;
using lssLib.Config.Encryption;

namespace lssLib.Config.Demo.Views;

public partial class EncryptionView : UserControl
{
    #region §1 ─ 초기화

    // 암호화 탭 전용 독립 ConfigManager 인스턴스 (BasicRw와 독립)
    // 실제 앱에서는 싱글톤을 공유하지만, 데모에서는 탭별 분리
    private readonly ConfigManager _cfg = ConfigManager.CreateNew();
    private readonly string _strSavePath;
    private bool _strKeySet;

    public EncryptionView()
    {
        // ① 상태 초기화
        _strSavePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config", "encrypted.json");

        InitializeComponent();

        Log("EncryptionView 초기화 완료.");
        Log("① 패스워드를 입력하고 [키 설정]을 클릭하세요.");
        Log("② 암호화 항목을 추가한 뒤 [JSON 저장]을 실행하세요.");
        Log("③ 파일 내용에서 ENC: 접두사를 확인하세요.");
        Log("④ [JSON 로드·복호화]로 원래 평문을 복원하세요.");
    }

    #endregion

    #region §2 ─ 패스워드 설정

    private void BtnSetPassword_Click(object sender, RoutedEventArgs e)
    {
        var pass = PbPassword.Password;
        if (string.IsNullOrWhiteSpace(pass))
        {
            Log("⚠ 패스워드를 입력하세요.");
            return;
        }
        try
        {
            _cfg.SetPassword(pass);
            _strKeySet = true;
            TbKeyStatus.Text = $"✅ 키 설정 완료  (패스워드 길이: {pass.Length}자)";
            TbKeyStatus.Foreground = System.Windows.Media.Brushes.Green;
            Log($"✅ 암호화 키 설정 완료. (PBKDF2-SHA256 · 100,000 회)");
        }
        catch (Exception ex) { Log($"❌ 키 설정 실패: {ex.Message}"); }
    }

    #endregion

    #region §3 ─ 샘플 항목

    private void BtnSampleDbPass_Click(object sender, RoutedEventArgs e)
    {
        TbEncSection.Text = "Credentials";
        TbEncKey.Text = "DbPassword";
        TbEncPlain.Text = "P@ssw0rd!DB#2024";
        ChkEncrypt.IsChecked = true;
    }

    private void BtnSampleApiToken_Click(object sender, RoutedEventArgs e)
    {
        TbEncSection.Text = "Credentials";
        TbEncKey.Text = "ApiToken";
        TbEncPlain.Text = "sk-api-xK9mLqRtZ2pWvNjYhCbX";
        ChkEncrypt.IsChecked = true;
    }

    private void BtnSampleSecret_Click(object sender, RoutedEventArgs e)
    {
        TbEncSection.Text = "Security";
        TbEncKey.Text = "JwtSecret";
        TbEncPlain.Text = "jwt-secret-key-lssLib-2024";
        ChkEncrypt.IsChecked = true;
    }

    #endregion

    #region §4 ─ 액션

    private void BtnAddEncrypted_Click(object sender, RoutedEventArgs e)
    {
        if (!_strKeySet && ChkEncrypt.IsChecked == true)
        {
            Log("⚠ 먼저 [키 설정]을 실행하세요.");
            return;
        }
        var sec = TbEncSection.Text.Trim();
        var key = TbEncKey.Text.Trim();
        var plain = TbEncPlain.Text.Trim();
        var enc = ChkEncrypt.IsChecked == true;

        if (string.IsNullOrEmpty(sec) || string.IsNullOrEmpty(key))
        {
            Log("⚠ 섹션·키를 입력하세요.");
            return;
        }

        _cfg.Set(sec, key, plain, enc);
        RefreshStoreView();
        Log($"➕ 추가: [{sec}] {key} = \"{plain}\"  IsEncrypted={enc}");
    }

    private void BtnSaveEncrypted_Click(object sender, RoutedEventArgs e)
    {
        if (!_strKeySet)
        {
            Log("⚠ 먼저 [키 설정]을 실행하세요.");
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_strSavePath)!);
            _cfg.Save(_strSavePath, ConfigFormat.Json);
            TbFileView.Text = File.ReadAllText(_strSavePath);
            Log($"✅ 저장 완료: {_strSavePath}");
            Log("   ↳ ENC: 접두사 항목은 파일에서 암호화된 Base64 형태로 저장됩니다.");
            MainWindow.SetStatus($"암호화 파일 저장 완료 → {Path.GetFileName(_strSavePath)}");
        }
        catch (Exception ex) { Log($"❌ 저장 실패: {ex.Message}"); }
    }

    private void BtnLoadDecrypt_Click(object sender, RoutedEventArgs e)
    {
        if (!_strKeySet)
        {
            Log("⚠ 먼저 [키 설정]을 실행하세요.");
            return;
        }
        if (!File.Exists(_strSavePath))
        {
            Log($"⚠ 파일 없음: {_strSavePath}  (먼저 [JSON 저장]을 실행하세요)");
            return;
        }
        try
        {
            _cfg.Clear();
            _cfg.Load(_strSavePath, ConfigFormat.Json);
            RefreshStoreView();
            Log($"✅ 로드 완료: {_strSavePath}");
            Log($"   ↳ ENC: 항목이 자동 복호화되어 평문으로 메모리에 적재됩니다.");

            foreach (var entry in _cfg.Store.GetAll().Where(e => e.IsEncrypted))
                Log($"   🔓 복호화 성공: [{entry.Section}] {entry.Key} = \"{entry.Value}\"");
        }
        catch (Exception ex) { Log($"❌ 로드/복호화 실패: {ex.Message}"); }
    }

    private void BtnDecryptTest_Click(object sender, RoutedEventArgs e)
    {
        if (!_strKeySet)
        {
            Log("⚠ 먼저 [키 설정]을 실행하세요.");
            return;
        }
        var plain = TbEncPlain.Text.Trim();
        if (string.IsNullOrEmpty(plain)) { Log("⚠ 평문값을 입력하세요."); return; }

        try
        {
            Log("── 직접 암호화/복호화 테스트 ─────────────────");
            var enc1 = ConfigEncryptor.Encrypt(plain);
            var enc2 = ConfigEncryptor.Encrypt(plain);  // 두 번 암호화: 다른 결과

            Log($"  평문     : {plain}");
            Log($"  암호문①  : {enc1[..40]}…");
            Log($"  암호문②  : {enc2[..40]}…");
            Log($"  동일한가? : {enc1 == enc2}  (랜덤 salt → 항상 다름)");

            var dec1 = ConfigEncryptor.Decrypt(enc1);
            var dec2 = ConfigEncryptor.Decrypt(enc2);
            Log($"  복호화①  : {dec1}  ✓={dec1 == plain}");
            Log($"  복호화②  : {dec2}  ✓={dec2 == plain}");

            var stored = ConfigEncryptor.ToStoredValue(plain);
            Log($"  저장형식  : {stored[..30]}…");
            Log($"  FromStored: {ConfigEncryptor.FromStoredValue(stored)}");
            Log("──────────────────────────────────────────────");
        }
        catch (Exception ex) { Log($"❌ 테스트 실패: {ex.Message}"); }
    }

    private void BtnClearEnc_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Clear();
        TbFileView.Clear();
        TbStoreView.Clear();
        Log("🧹 초기화 완료.");
    }

    #endregion

    #region §5 ─ 내부 헬퍼

    private void RefreshStoreView()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var section in _cfg.Store.GetSections())
        {
            sb.AppendLine($"[{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
            {
                sb.AppendLine(entry.IsEncrypted
                    ? $"  {entry.Key} = \"{entry.Value}\"  🔐"
                    : $"  {entry.Key} = \"{entry.Value}\"");
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