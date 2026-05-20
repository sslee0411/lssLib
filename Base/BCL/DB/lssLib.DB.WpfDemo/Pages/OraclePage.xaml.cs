// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Pages/OraclePage.xaml.cs
//  역할: Oracle Provider 데모 — CallProc / CallProc1 / SpMutiSave / SQL
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.Windows;
using System.Windows.Controls;
using lssLib.DB.Core;
using lssLib.DB.Oracle;
using lssLib.DB.WpfDemo.Models;

namespace lssLib.DB.WpfDemo.Pages;

public partial class OraclePage : UserControl
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private OracleDbContext?               _ctx;
    private OracleRepository<SensorRow>?  _repo;
    private Action<string>?                _statusCallback;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    public OraclePage() => InitializeComponent();

    public void SetStatusCallback(Action<string> cb) => _statusCallback = cb;

    // §3 ─ 연결
    // ─────────────────────────────────────────────────────────────────
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ctx is not null) await _ctx.DisposeAsync();

            var cfg = new RelationalDbConfig(
                DbProviderType.Oracle,
                TxtConnStr.Text.Trim(),
                commandTimeoutSec: 180);

            _ctx  = new OracleDbContext(cfg);
            await _ctx.OpenAsync();
            _repo = new OracleRepository<SensorRow>(_ctx, MapRow);

            AppendLog($"[연결] Oracle DB 연결 성공");
            SetStatus("Oracle 연결 성공");
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] {ex.Message}");
            SetStatus($"연결 실패: {ex.Message}");
        }
    }

    // §4 ─ CallProc (표준 SP 호출)
    // ─────────────────────────────────────────────────────────────────
    private async void BtnCallSp_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("SP 호출 중...");
        // StandardSp → IN_DATA / OUT_RETURNCODE / OUT_RETURNMSG / OUT_CURSOR
        var parts  = TxtInData.Text.Split(',');
        var inData = string.Join(",", parts.Select(p => $"'{p.Trim()}'"));
        var ps     = DbParam.StandardSp($"SELECT {inData} FROM DUAL");

        var r = await _repo.CallSpQueryAsync(TxtSpName.Text.Trim(), ps);

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value;
            AppendLog($"[CallProc] {TxtSpName.Text} → {r.Value?.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"SP 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[SP 오류] {r.Message}");
            SetStatus($"SP 실패: {r.Message}");
        }
    }

    // §5 ─ CallProc1 (가변인수 SP 호출)
    // ─────────────────────────────────────────────────────────────────
    private async void BtnCallSpArgs_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("CallProc1 호출 중...");
        var args = TxtInData.Text.Split(',').Select(s => (object?)s.Trim()).ToArray();

        // OracleDB.CallProc1 동일 패턴 — 가변인수 → SELECT 'v1','v2' FROM DUAL 자동 조립
        var r = await _repo.CallSpArgsQueryAsync(TxtSpName.Text.Trim(), default, args);

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value;
            AppendLog($"[CallProc1] {TxtSpName.Text} → {r.Value?.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"CallProc1 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"CallProc1 실패: {r.Message}");
        }
    }

    // §6 ─ SpMutiSave 데모
    // ─────────────────────────────────────────────────────────────────
    private async void BtnMutiSave_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        // 더미 DataTable 생성
        var dt = new DataTable();
        dt.Columns.Add("SENSOR_ID",    typeof(int));
        dt.Columns.Add("SENSOR_VALUE", typeof(double));
        dt.Columns.Add("USE_YN",       typeof(string));

        for (int i = 1; i <= 10; i++)
            dt.Rows.Add(i, Math.Round(Random.Shared.NextDouble() * 100, 2), i % 3 == 0 ? "N" : "Y");

        SetStatus("SpMutiSave 실행 중...");
        // USE_YN = 'Y' 행만 SP 호출 (OracleDB.Sp_MutiSave 동일 패턴)
        var r = await _repo.SpMutiSaveAsync(
            dt:              dt,
            spName:          "SP_SENSOR_SAVE",
            whereConditions: [("USE_YN", "Y")],
            paramColumns:    ["SENSOR_ID", "SENSOR_VALUE"]);

        AppendLog(r.IsOk
            ? $"[MutiSave] ✅ {r.Value}행 처리 ({r.ElapsedMs}ms)"
            : $"[MutiSave 오류] {r.Message}");
        SetStatus(r.IsOk ? $"SpMutiSave 완료 — {r.Value}행" : $"실패: {r.Message}");
    }

    // §7 ─ SQL 직접 조회
    // ─────────────────────────────────────────────────────────────────
    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("SQL 조회 중...");
        var r = await _repo.QueryAsync(TxtSql.Text.Trim());

        if (r.IsOk)
        {
            DgResult.ItemsSource = r.Value;
            AppendLog($"[SQL] {r.Value?.Count ?? 0}행 ({r.ElapsedMs}ms)");
            SetStatus($"Oracle 조회 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"조회 실패: {r.Message}");
        }
    }

    // §8 ─ RowMapper
    // ─────────────────────────────────────────────────────────────────
    private static SensorRow MapRow(DataRow row) => new()
    {
        SensorId   = row["SENSOR_ID"]    is DBNull ? 0  : Convert.ToInt32(row["SENSOR_ID"]),
        PlantCd    = row["PLANT_CD"]?.ToString()   ?? string.Empty,
        SensorName = row["SENSOR_NM"]?.ToString()  ?? string.Empty,
        Value      = row["SENSOR_VALUE"] is DBNull ? 0d : Convert.ToDouble(row["SENSOR_VALUE"]),
        UseYn      = row["USE_YN"]?.ToString()     ?? "Y",
        RegDt      = row["REG_DT"]?.ToString()     ?? string.Empty,
    };

    // §9 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────
    private void AppendLog(string msg)
        => Dispatcher.InvokeAsync(() =>
        {
            TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            TxtLog.ScrollToEnd();
        });

    private void SetStatus(string msg) => _statusCallback?.Invoke(msg);
}
