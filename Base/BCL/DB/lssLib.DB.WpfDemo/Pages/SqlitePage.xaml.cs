// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.WpfDemo · Pages/SqlitePage.xaml.cs
//  역할: SQLite Provider 데모 — 파일 생성 / EnsureTable / Upsert / SQL
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using lssLib.DB.Core;
using lssLib.DB.Sqlite;
using lssLib.DB.WpfDemo.Models;
using Microsoft.Win32;

namespace lssLib.DB.WpfDemo.Pages;

public partial class SqlitePage : UserControl
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private SqliteDbContext?               _ctx;
    private SqliteRepository<SensorRow>?  _repo;
    private Action<string>?                _statusCallback;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────
    public SqlitePage()
    {
        InitializeComponent();
        // 기본 경로 설정
        TxtDbPath.Text = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "lssLib_demo.db");
    }

    public void SetStatusCallback(Action<string> cb) => _statusCallback = cb;

    // §3 ─ 파일 찾기
    // ─────────────────────────────────────────────────────────────────
    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "SQLite DB 파일 선택 또는 새 파일명 입력",
            Filter = "SQLite DB (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = false,
        };
        if (dlg.ShowDialog() == true)
            TxtDbPath.Text = dlg.FileName;
    }

    // §4 ─ 연결 / 생성
    // ─────────────────────────────────────────────────────────────────
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ctx is not null) await _ctx.DisposeAsync();

            var cfg = new RelationalDbConfig(
                DbProviderType.Sqlite,
                $"Data Source={TxtDbPath.Text.Trim()}",
                commandTimeoutSec: 30);

            _ctx  = new SqliteDbContext(cfg);
            await _ctx.OpenAsync();
            _repo = new SqliteRepository<SensorRow>(_ctx, MapRow);

            bool isNew = !_ctx.DbFileExists;
            AppendLog($"[연결] {_ctx.DbFilePath} {(isNew ? "(새 파일)" : "(기존 파일)")}");
            SetStatus($"SQLite {(isNew ? "생성" : "연결")} 성공");
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] {ex.Message}");
            SetStatus($"연결 실패: {ex.Message}");
        }
    }

    // §5 ─ 테이블 초기화 (EnsureTable)
    // ─────────────────────────────────────────────────────────────────
    private async void BtnEnsureTable_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        var r = await _ctx.EnsureTableAsync("""
            CREATE TABLE IF NOT EXISTS sensor_config (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                plant_cd   TEXT    NOT NULL,
                sensor_id  INTEGER NOT NULL,
                threshold  REAL    DEFAULT 80.0,
                use_yn     TEXT    DEFAULT 'Y',
                reg_dt     TEXT    DEFAULT (datetime('now','localtime')),
                UNIQUE(plant_cd, sensor_id)
            )
            """);

        AppendLog(r.IsOk
            ? "[테이블] sensor_config 초기화 완료"
            : $"[오류] {r.Message}");
        SetStatus(r.IsOk ? "테이블 초기화 완료" : $"실패: {r.Message}");
    }

    // §6 ─ WAL 모드 설정
    // ─────────────────────────────────────────────────────────────────
    private async void BtnWal_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        await _ctx.SetPragmaAsync("journal_mode", "WAL");
        await _ctx.SetPragmaAsync("foreign_keys", "ON");
        await _ctx.SetPragmaAsync("synchronous",  "NORMAL");

        string mode = await _ctx.GetPragmaAsync("journal_mode");
        AppendLog($"[PRAGMA] journal_mode = {mode}");
        SetStatus($"PRAGMA 설정 완료 — journal_mode={mode}");
    }

    // §7 ─ SQL 조회
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
            SetStatus($"SQLite 조회 완료 — {r.Value?.Count ?? 0}행");
        }
        else
        {
            AppendLog($"[오류] {r.Message}");
            SetStatus($"조회 실패: {r.Message}");
        }
    }

    // §8 ─ Upsert (INSERT OR REPLACE)
    // ─────────────────────────────────────────────────────────────────
    private async void BtnUpsert_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        SetStatus("Upsert 5건 실행 중...");
        int ok = 0;

        for (int i = 1; i <= 5; i++)
        {
            var r = await _repo.UpsertAsync("sensor_config",
            [
                ("plant_cd",  "A01"),
                ("sensor_id", i),
                ("threshold", Math.Round(Random.Shared.NextDouble() * 100, 1)),
                ("use_yn",    i % 2 == 0 ? "N" : "Y"),
            ]);
            if (r.IsOk) ok++;
        }

        AppendLog($"[Upsert] ✅ {ok}/5건 완료");
        SetStatus($"Upsert 완료 — {ok}건");
    }

    // §9 ─ 전체 삭제
    // ─────────────────────────────────────────────────────────────────
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null) { AppendLog("[경고] 먼저 연결하세요."); return; }

        var confirm = MessageBox.Show(
            "sensor_config 테이블 전체 데이터를 삭제합니다.",
            "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var r = await _repo.ExecuteAsync("DELETE FROM sensor_config");

        AppendLog(r.IsOk
            ? $"[삭제] {r.Value}행 삭제 완료"
            : $"[오류] {r.Message}");
        SetStatus(r.IsOk ? $"전체 삭제 완료 — {r.Value}행" : $"실패: {r.Message}");

        if (r.IsOk) DgResult.ItemsSource = null;
    }

    // §10 ─ RowMapper
    // ─────────────────────────────────────────────────────────────────
    private static SensorRow MapRow(DataRow row) => new()
    {
        SensorId   = row["sensor_id"]  is DBNull ? 0  : Convert.ToInt32(row["sensor_id"]),
        PlantCd    = row["plant_cd"]?.ToString()  ?? string.Empty,
        Value      = row["threshold"]  is DBNull ? 0d : Convert.ToDouble(row["threshold"]),
        UseYn      = row["use_yn"]?.ToString()    ?? "Y",
        RegDt      = row["reg_dt"]?.ToString()    ?? string.Empty,
    };

    // §11 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────
    private void AppendLog(string msg)
        => Dispatcher.InvokeAsync(() =>
        {
            TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            TxtLog.ScrollToEnd();
        });

    private void SetStatus(string msg) => _statusCallback?.Invoke(msg);
}
