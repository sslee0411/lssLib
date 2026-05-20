// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Pages/MsSqlPage.xaml.cs
//  역할: MSSQL Provider 데모 — SQL 조회 / SP 호출 / BulkInsert
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.Windows;
using System.Windows.Controls;
using lssLib.DB.Core;
using lssLib.DB.MsSql;
using lssLib.DB.WpfDemo.Models;

namespace lssLib.DB.WpfDemo.Pages;

public partial class MsSqlPage : UserControl
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private MsSqlDbContext?               _ctx;
    private MsSqlRepository<SensorRow>?  _repo;
    private Action<string>?               _statusCallback;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    public MsSqlPage() => InitializeComponent();

    public void SetStatusCallback(Action<string> cb) => _statusCallback = cb;

    // §3 ─ 연결
    // ─────────────────────────────────────────────────────────────────
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ctx is not null) await _ctx.DisposeAsync();

            var cfg = new RelationalDbConfig(
                DbProviderType.MsSql,
                TxtConnStr.Text.Trim(),
                commandTimeoutSec: 30);

            _ctx  = new MsSqlDbContext(cfg);
            await _ctx.OpenAsync();

            // DbHelper 오류 핸들러 등록 → 상태바 연동
            Helpers.DbHelper.ExtraErrorHandler = (src, msg) =>
                _statusCallback?.Invoke($"[{src}] {msg}");

            _repo = new MsSqlRepository<SensorRow>(_ctx, MapRow);

            AppendLog($"[연결] {TxtConnStr.Text[..Math.Min(50, TxtConnStr.Text.Length)]}...");
            SetStatus("MSSQL 연결 성공");
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] {ex.Message}");
            SetStatus($"연결 실패: {ex.Message}");
        }
    }

    // §4 ─ SQL 조회
    // ─────────────────────────────────────────────────────────────────
    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("SQL 조회 중...");
        var r = await _repo.QueryAsync(TxtSql.Text.Trim());

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value;
            AppendLog($"[조회] {r.Value?.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"MSSQL 조회 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"조회 실패: {r.Message}");
        }
    }

    // §5 ─ SP 호출
    // ─────────────────────────────────────────────────────────────────
    private async void BtnCallSp_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("SP 호출 중...");

        // DbParam.StandardSp 사용 — IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR
        var ps = DbParam.StandardSp(
            $"SELECT '{TxtInData.Text.Trim()}' FROM DUAL");

        var r = await _repo.CallSpQueryAsync(TxtSpName.Text.Trim(), ps);

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value;
            AppendLog($"[SP] {TxtSpName.Text} → {r.Value?.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"SP 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[SP 오류] {r.Message}");
            SetStatus($"SP 실패: {r.Message}");
        }
    }

    // §6 ─ BulkInsert
    // ─────────────────────────────────────────────────────────────────
    private async void BtnBulk_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("BulkInsert 100건 준비 중...");

        // 100건 더미 데이터 생성
        var entities = Enumerable.Range(1, 100).Select(i => new SensorRow
        {
            SensorId   = i,
            PlantCd    = "A01",
            SensorName = $"Sensor_{i:D3}",
            Value      = Math.Round(Random.Shared.NextDouble() * 100, 2),
            UseYn      = "Y",
            RegDt      = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        }).ToList();

        var r = await _repo.BulkInsertAsync(
            destinationTable: "SENSOR_DATA",
            entities:  entities,
            toRow: (s, row) =>
            {
                row["SENSOR_ID"]    = s.SensorId;
                row["PLANT_CD"]     = s.PlantCd;
                row["SENSOR_NM"]    = s.SensorName;
                row["SENSOR_VALUE"] = s.Value;
                row["USE_YN"]       = s.UseYn;
                row["REG_DT"]       = s.RegDt;
            },
            columns:
            [
                ("SENSOR_ID",    typeof(int)),
                ("PLANT_CD",     typeof(string)),
                ("SENSOR_NM",    typeof(string)),
                ("SENSOR_VALUE", typeof(double)),
                ("USE_YN",       typeof(string)),
                ("REG_DT",       typeof(string)),
            ],
            batchSize: 50);

        if (r.IsOk)
        {
            AppendLog($"[Bulk] ✅ {r.Value}행 삽입 완료 ({r.ElapsedMs}ms)");
            SetStatus($"BulkInsert 완료 — {r.Value}행");
        }
        else
        {
            AppendLog($"[Bulk 오류] {r.Message}");
            SetStatus($"BulkInsert 실패: {r.Message}");
        }
    }

    // §7 ─ RowMapper
    // ─────────────────────────────────────────────────────────────────
    private static SensorRow MapRow(DataRow row) => new()
    {
        SensorId   = row["SENSOR_ID"]    is DBNull ? 0      : Convert.ToInt32(row["SENSOR_ID"]),
        PlantCd    = row["PLANT_CD"]?.ToString()   ?? string.Empty,
        SensorName = row["SENSOR_NM"]?.ToString()  ?? string.Empty,
        Value      = row["SENSOR_VALUE"] is DBNull ? 0d     : Convert.ToDouble(row["SENSOR_VALUE"]),
        UseYn      = row["USE_YN"]?.ToString()     ?? "Y",
        RegDt      = row["REG_DT"]?.ToString()     ?? string.Empty,
    };

    // §8 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────
    private void AppendLog(string msg)
        => Dispatcher.InvokeAsync(() =>
        {
            TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            TxtLog.ScrollToEnd();
        });

    private void SetStatus(string msg) => _statusCallback?.Invoke(msg);
}
