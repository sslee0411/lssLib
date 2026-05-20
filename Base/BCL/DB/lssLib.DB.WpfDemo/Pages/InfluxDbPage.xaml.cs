// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Pages/InfluxDbPage.xaml.cs
//  역할: InfluxDB v2.0 Provider 데모
//        Flux 쿼리 / Line Protocol 쓰기 / Health Check
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.Windows;
using System.Windows.Controls;
using lssLib.DB.Core;
using lssLib.DB.InfluxDB;

namespace lssLib.DB.WpfDemo.Pages;

public partial class InfluxDbPage : UserControl
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private InfluxDbContext?     _ctx;
    private Action<string>?      _statusCallback;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    public InfluxDbPage() => InitializeComponent();

    public void SetStatusCallback(Action<string> cb) => _statusCallback = cb;

    // §3 ─ 연결
    // ─────────────────────────────────────────────────────────────────
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _ctx?.DisposeAsync();
            var cfg = new InfluxDbConfig(
                TxtUrl.Text.Trim(),
                TxtToken.Text.Trim(),
                TxtOrg.Text.Trim(),
                TxtBucket.Text.Trim());

            _ctx = new InfluxDbContext(cfg);
            await _ctx.OpenAsync();

            AppendLog($"[연결] {TxtUrl.Text} / {TxtBucket.Text}");
            SetStatus("InfluxDB 연결 성공");
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] 연결 실패: {ex.Message}");
            SetStatus($"연결 실패: {ex.Message}");
        }
    }

    private async void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx is null) { AppendLog("[경고] 먼저 연결하세요."); return; }
        bool ok = await _ctx.PingAsync();
        AppendLog(ok ? "[Health] ✅ 서버 응답 정상" : "[Health] ❌ 서버 응답 없음");
        SetStatus(ok ? "InfluxDB Health OK" : "InfluxDB 응답 없음");
    }

    // §4 ─ Flux 쿼리
    // ─────────────────────────────────────────────────────────────────
    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("Flux 쿼리 실행 중...");
        var r = await _ctx.QueryFluxAsync(TxtFlux.Text.Trim());

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value?.DefaultView;
            AppendLog($"[조회] {r.Value?.Rows.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"InfluxDB 조회 완료 — {r.Value?.Rows.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"조회 실패: {r.Message}");
        }
    }

    // §5 ─ Line Protocol 쓰기
    // ─────────────────────────────────────────────────────────────────
    private async void BtnWrite_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        if (!double.TryParse(TxtWriteValue.Text, out double val))
        {
            AppendLog("[경고] 숫자 값을 입력하세요.");
            return;
        }

        var line = new LineProtocolBuilder("sensor_data")
            .Tag("plant", TxtWritePlant.Text.Trim())
            .Field("temperature", val)
            .Field("status",      1)
            .Timestamp(DateTime.UtcNow)
            .Build();

        var r = await _ctx.WriteLineProtocolAsync(line);

        if (r.IsOk)
        {
            AppendLog($"[Write] ✅ {line}");
            SetStatus("InfluxDB 쓰기 완료");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"쓰기 실패: {r.Message}");
        }
    }

    // §6 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────
    private void AppendLog(string msg)
        => Dispatcher.InvokeAsync(() =>
        {
            TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            TxtLog.ScrollToEnd();
        });

    private void SetStatus(string msg) => _statusCallback?.Invoke(msg);
}
