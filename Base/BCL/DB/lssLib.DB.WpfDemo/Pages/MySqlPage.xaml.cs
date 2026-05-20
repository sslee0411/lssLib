// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Pages/MySqlPage.xaml.cs
//  역할: MySQL Provider 데모 — SQL 조회 / SP 호출 / 배치 INSERT
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.Windows;
using System.Windows.Controls;
using lssLib.DB.Core;
using lssLib.DB.MySql;
using lssLib.DB.WpfDemo.Models;

namespace lssLib.DB.WpfDemo.Pages;

public partial class MySqlPage : UserControl
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private MySqlDbContext?               _ctx;
    private MySqlRepository<SensorRow>?  _repo;
    private Action<string>?               _statusCallback;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    public MySqlPage() => InitializeComponent();

    public void SetStatusCallback(Action<string> cb) => _statusCallback = cb;

    // §3 ─ 연결
    // ─────────────────────────────────────────────────────────────────
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ctx is not null) await _ctx.DisposeAsync();

            var cfg = new RelationalDbConfig(
                DbProviderType.MySql,
                TxtConnStr.Text.Trim(),
                commandTimeoutSec: 30);

            _ctx  = new MySqlDbContext(cfg);
            await _ctx.OpenAsync();
            _repo = new MySqlRepository<SensorRow>(_ctx, MapRow);

            AppendLog("[연결] MySQL DB 연결 성공");
            SetStatus("MySQL 연결 성공");
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
            SetStatus($"MySQL 조회 완료 — {r.Value?.Count ?? 0}행");
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
        var ps = DbParam.StandardSp($"SELECT '{TxtInData.Text.Trim()}'");
        var r  = await _repo.CallSpQueryAsync(TxtSpName.Text.Trim(), ps);

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

    // §6 ─ 배치 INSERT (ExecuteBatchAsync 데모)
    // ─────────────────────────────────────────────────────────────────
    private async void BtnBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("배치 INSERT 10건 실행 중...");

        // 10건 일괄 INSERT — 트랜잭션 자동 관리
        var commands = Enumerable.Range(1, 10).Select(i =>
        (
            Sql: "INSERT INTO sensor_data (plant_cd, sensor_id, sensor_value, reg_dt) " +
                 "VALUES (@P1, @P2, @P3, NOW())",
            Ps: (DbParam[]?)new[]
            {
                DbParam.In("@P1", "A01"),
                DbParam.In("@P2", i),
                DbParam.In("@P3", Math.Round(Random.Shared.NextDouble() * 100, 2)),
            }
        )).ToList();

        var r = await _repo.ExecuteBatchAsync(commands);

        AppendLog(r.IsOk
            ? $"[Batch] ✅ {r.Value}행 INSERT 완료 ({r.ElapsedMs}ms)"
            : $"[Batch 오류] {r.Message}");
        SetStatus(r.IsOk ? $"배치 INSERT 완료 — {r.Value}행" : $"실패: {r.Message}");
    }

    // §7 ─ RowMapper
    // ─────────────────────────────────────────────────────────────────
    private static SensorRow MapRow(DataRow row) => new()
    {
        SensorId   = row["sensor_id"]    is DBNull ? 0  : Convert.ToInt32(row["sensor_id"]),
        PlantCd    = row["plant_cd"]?.ToString()   ?? string.Empty,
        SensorName = row["sensor_nm"]?.ToString()  ?? string.Empty,
        Value      = row["sensor_value"] is DBNull ? 0d : Convert.ToDouble(row["sensor_value"]),
        UseYn      = row["use_yn"]?.ToString()     ?? "Y",
        RegDt      = row["reg_dt"]?.ToString()     ?? string.Empty,
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
