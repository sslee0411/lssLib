// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Storage/TagHistoryDb.cs
//  역할: 수집 태그값 SQLite 저장소
//  수정: Phase 8R
//    ① using lssLib.DB.Contracts 추가 (RowMapper<T> 위치)
//    ② using lssLib.DB.Core 추가 (RelationalDbConfig, DbProviderType, DbParam)
//    ③ QueryScalarAsync<long> → long (non-nullable) → ?? 제거
// ══════════════════════════════════════════════════════════

using lssLib.DB;                 // DbResult 등 공통
using lssLib.DB.Contracts;       // RowMapper<T>
using lssLib.DB.Core;            // RelationalDbConfig · DbProviderType · DbParam
using lssLib.DB.Sqlite;          // SqliteDbContext · SqliteRepository<T>
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.CollectorRuntime.Storage;

/// <summary>태그 히스토리 단건 레코드</summary>
public sealed record TagHistoryRecord(
    string   TagId,
    DateTime Timestamp,
    double   Value,
    string   Quality = "Good");

/// <summary>
/// 태그 히스토리 SQLite 저장소.
/// CommandQueue 를 통해 비동기·배치로 기록합니다.
/// </summary>
public sealed class TagHistoryDb : IAsyncDisposable
{
    private const string LogSrc       = "TagHistoryDb";
    private const string HistoryTable = "tag_history";
    private const string LatestTable  = "tag_latest";
    private const int    BatchSize    = 100;

    private readonly SqliteDbContext _ctx;
    private readonly SqliteRepository<TagHistoryRecord> _histRepo;
    private readonly SqliteRepository<TagHistoryRecord> _latestRepo;

    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private bool _disposed;

    public TagHistoryDb(string dbPath)
    {
        var cfg = new RelationalDbConfig(
            DbProviderType.Sqlite,
            $"Data Source={dbPath}",
            commandTimeoutSec: 30);

        _ctx = new SqliteDbContext(cfg);

        // ★ RowMapper<T> — lssLib.DB.Contracts 네임스페이스
        RowMapper<TagHistoryRecord> mapper = row => new TagHistoryRecord(
            TagId     : row["tag_id"].ToString()!,
            Timestamp : DateTime.Parse(row["ts"].ToString()!),
            Value     : Convert.ToDouble(row["value"]),
            Quality   : row["quality"].ToString() ?? "Good");

        _histRepo   = new SqliteRepository<TagHistoryRecord>(_ctx, mapper);
        _latestRepo = new SqliteRepository<TagHistoryRecord>(_ctx, mapper);
    }

    public async Task InitializeAsync()
    {
        await _ctx.OpenAsync();
        await _ctx.SetPragmaAsync("journal_mode", "WAL");
        await _ctx.SetPragmaAsync("foreign_keys", "ON");

        await _ctx.EnsureTableAsync($"""
            CREATE TABLE IF NOT EXISTS {HistoryTable} (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                tag_id   TEXT    NOT NULL,
                ts       TEXT    NOT NULL,
                value    REAL    NOT NULL,
                quality  TEXT    NOT NULL DEFAULT 'Good',
                reg_dt   TEXT    DEFAULT (datetime('now','localtime'))
            );
            CREATE INDEX IF NOT EXISTS idx_hist_tag_ts
                ON {HistoryTable} (tag_id, ts);
            """);

        await _ctx.EnsureTableAsync($"""
            CREATE TABLE IF NOT EXISTS {LatestTable} (
                tag_id   TEXT PRIMARY KEY,
                ts       TEXT NOT NULL,
                value    REAL NOT NULL,
                quality  TEXT NOT NULL DEFAULT 'Good',
                upd_dt   TEXT DEFAULT (datetime('now','localtime'))
            );
            """);

        LogManager.Instance.Info(LogSrc, "TagHistoryDb 초기화 완료");
    }

    public void Enqueue(TagHistoryRecord record)
    {
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(
            async ct => await _InsertBatchAsync([record], ct),
            CommandPriority.Normal));
    }

    public void EnqueueBatch(IEnumerable<TagHistoryRecord> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(
            async ct => await _InsertBatchAsync(list, ct),
            CommandPriority.Normal));
    }

    public void UpsertLatest(TagHistoryRecord record)
    {
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(
            async ct =>
            {
                await _latestRepo.UpsertAsync(LatestTable,
                [
                    ("tag_id",  record.TagId),
                    ("ts",      record.Timestamp.ToString("o")),
                    ("value",   record.Value),
                    ("quality", record.Quality),
                ]);
            },
            CommandPriority.Normal));
    }

    public async Task<List<TagHistoryRecord>> QueryRecentAsync(string tagId, int count = 100)
    {
        var r = await _histRepo.QueryAsync(
            $"SELECT * FROM {HistoryTable} WHERE tag_id = @ID ORDER BY ts DESC LIMIT @N",
            [DbParam.In("@ID", tagId), DbParam.In("@N", count)]);
        return r.IsOk ? r.Value ?? [] : [];
    }

    public async Task<List<TagHistoryRecord>> QueryRangeAsync(
        string tagId, DateTime from, DateTime to)
    {
        var r = await _histRepo.QueryAsync(
            $"SELECT * FROM {HistoryTable} WHERE tag_id = @ID AND ts BETWEEN @FROM AND @TO ORDER BY ts",
            [
                DbParam.In("@ID",   tagId),
                DbParam.In("@FROM", from.ToString("o")),
                DbParam.In("@TO",   to.ToString("o")),
            ]);
        return r.IsOk ? r.Value ?? [] : [];
    }

    public async Task<long> GetTotalCountAsync()
    {
        // ★ QueryScalarAsync<long> → DbResult<long> (non-nullable)
        var r = await _histRepo.QueryScalarAsync<long>(
            $"SELECT COUNT(*) FROM {HistoryTable}");
        return r.IsOk ? r.Value : 0L;
    }

    private async Task _InsertBatchAsync(List<TagHistoryRecord> records, CancellationToken ct)
    {
        for (int i = 0; i < records.Count; i += BatchSize)
        {
            var chunk = records.Skip(i).Take(BatchSize).ToList();
            foreach (var rec in chunk)
            {
                await _histRepo.ExecuteAsync(
                    $"INSERT INTO {HistoryTable} (tag_id, ts, value, quality) VALUES (@ID, @TS, @VAL, @Q)",
                    [
                        DbParam.In("@ID",  rec.TagId),
                        DbParam.In("@TS",  rec.Timestamp.ToString("o")),
                        DbParam.In("@VAL", rec.Value),
                        DbParam.In("@Q",   rec.Quality),
                    ]);
            }
            LogManager.Instance.Debug(LogSrc, $"배치 저장 완료: {chunk.Count}건");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _batchLock.Dispose();
        await _ctx.DisposeAsync();
        _disposed = true;
    }
}
