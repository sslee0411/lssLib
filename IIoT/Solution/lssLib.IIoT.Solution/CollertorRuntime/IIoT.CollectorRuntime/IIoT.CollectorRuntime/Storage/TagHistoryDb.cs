// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Storage/TagHistoryDb.cs
//  역할: 수집 태그값 SQLite 저장소
//        lssLib.DB.Sqlite (SqliteDbContext / SqliteRepository) 사용
//        CommandQueue 를 통해 비동기 배치 저장
//  Phase 8: 신규
//
//  테이블 구조:
//    tag_history : tagId, timestamp, value, quality
//    tag_latest  : tagId(PK), timestamp, value, quality (최신값 UPSERT)
// ══════════════════════════════════════════════════════════

using lssLib.DB;
using lssLib.DB.Sqlite;
using lssLib.Log;
using lssLib.Messaging;

namespace IIoT.CollectorRuntime.Storage;

/// <summary>태그 히스토리 단건 레코드</summary>
public sealed record TagHistoryRecord(
    string   TagId,
    DateTime Timestamp,
    double   Value,
    string   Quality = "Good");  // "Good" / "Uncertain" / "Bad"

/// <summary>
/// 태그 히스토리 SQLite 저장소.
///
/// SDT 압축 후 저장 결정된 포인트를 CommandQueue 를 통해
/// 비동기·배치로 SQLite 에 기록합니다.
///
/// CommandQueue 사용 이유:
///   · 수집 폴링 스레드를 블로킹하지 않음
///   · 배치 INSERT 로 SQLite 쓰기 부하 최소화
///   · 우선순위: Normal (실시간 알람 저장은 High)
/// </summary>
public sealed class TagHistoryDb : IAsyncDisposable
{
    // §1 ─ 상수·필드 ──────────────────────────────────────────
    private const string LogSrc      = "TagHistoryDb";
    private const string HistoryTable = "tag_history";
    private const string LatestTable  = "tag_latest";
    private const int    BatchSize    = 100; // 배치 INSERT 묶음 크기

    private readonly SqliteDbContext _ctx;
    private readonly SqliteRepository<TagHistoryRecord> _histRepo;
    private readonly SqliteRepository<TagHistoryRecord> _latestRepo;

    private readonly List<TagHistoryRecord> _batch = [];
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private bool _disposed;

    // §2 ─ 생성자 / 초기화 ────────────────────────────────────
    public TagHistoryDb(string dbPath)
    {
        var cfg = new RelationalDbConfig(
            DbProviderType.Sqlite,
            $"Data Source={dbPath}",
            commandTimeoutSec: 30);

        _ctx = new SqliteDbContext(cfg);

        RowMapper<TagHistoryRecord> mapper = row => new TagHistoryRecord(
            TagId     : row["tag_id"].ToString()!,
            Timestamp : DateTime.Parse(row["ts"].ToString()!),
            Value     : Convert.ToDouble(row["value"]),
            Quality   : row["quality"].ToString() ?? "Good");

        _histRepo   = new SqliteRepository<TagHistoryRecord>(_ctx, mapper);
        _latestRepo = new SqliteRepository<TagHistoryRecord>(_ctx, mapper);
    }

    /// <summary>DB 초기화 (테이블 생성 + WAL 모드)</summary>
    public async Task InitializeAsync()
    {
        await _ctx.OpenAsync();

        // WAL 모드 (동시 읽기 성능 향상)
        await _ctx.SetPragmaAsync("journal_mode", "WAL");
        await _ctx.SetPragmaAsync("foreign_keys", "ON");

        // tag_history 테이블
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

        // tag_latest 테이블 (태그별 최신값 — UPSERT)
        await _ctx.EnsureTableAsync($"""
            CREATE TABLE IF NOT EXISTS {LatestTable} (
                tag_id   TEXT PRIMARY KEY,
                ts       TEXT NOT NULL,
                value    REAL NOT NULL,
                quality  TEXT NOT NULL DEFAULT 'Good',
                upd_dt   TEXT DEFAULT (datetime('now','localtime'))
            );
            """);

        LogManager.Instance.Info(LogSrc, $"TagHistoryDb 초기화 완료");
    }

    // §3 ─ 저장 메서드 ─────────────────────────────────────────

    /// <summary>
    /// 태그값을 저장 큐에 추가합니다 (CommandQueue 방식).
    ///
    /// SDT 압축 후 ShouldStore() == true 인 경우에만 호출합니다.
    /// BatchSize 도달 시 자동으로 DB에 플러시합니다.
    /// </summary>
    public void Enqueue(TagHistoryRecord record)
    {
        // CommandQueue 로 비동기 저장 (폴링 스레드 블로킹 없음)
        CommandQueue.Instance.Enqueue(LambdaCommand.Create(
            async ct => await _InsertBatchAsync([record], ct),
            CommandPriority.Normal));
    }

    /// <summary>
    /// 여러 태그값을 한 번에 저장 큐에 추가합니다 (배치 최적화).
    /// </summary>
    public void EnqueueBatch(IEnumerable<TagHistoryRecord> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        CommandQueue.Instance.Enqueue(LambdaCommand.Create(
            async ct => await _InsertBatchAsync(list, ct),
            CommandPriority.Normal));
    }

    /// <summary>
    /// 최신값 즉시 업데이트 (tag_latest UPSERT).
    /// 알람 판정 등 최신값 조회 용도.
    /// </summary>
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

    // §4 ─ 조회 메서드 ─────────────────────────────────────────

    /// <summary>최근 N개 히스토리 조회</summary>
    public async Task<List<TagHistoryRecord>> QueryRecentAsync(
        string tagId, int count = 100)
    {
        var r = await _histRepo.QueryAsync(
            $"SELECT * FROM {HistoryTable} " +
            $"WHERE tag_id = @ID ORDER BY ts DESC LIMIT @N",
            [DbParam.In("@ID", tagId), DbParam.In("@N", count)]);

        return r.IsOk ? r.Value ?? [] : [];
    }

    /// <summary>시간 범위 히스토리 조회</summary>
    public async Task<List<TagHistoryRecord>> QueryRangeAsync(
        string tagId, DateTime from, DateTime to)
    {
        var r = await _histRepo.QueryAsync(
            $"SELECT * FROM {HistoryTable} " +
            $"WHERE tag_id = @ID AND ts BETWEEN @FROM AND @TO ORDER BY ts",
            [
                DbParam.In("@ID",   tagId),
                DbParam.In("@FROM", from.ToString("o")),
                DbParam.In("@TO",   to.ToString("o")),
            ]);
        return r.IsOk ? r.Value ?? [] : [];
    }

    /// <summary>전체 저장 건수</summary>
    public async Task<long> GetTotalCountAsync()
    {
        var r = await _histRepo.QueryScalarAsync<long>(
            $"SELECT COUNT(*) FROM {HistoryTable}");
        return r.IsOk ? r.Value ?? 0 : 0;
    }

    // §5 ─ 내부 배치 INSERT ────────────────────────────────────

    private async Task _InsertBatchAsync(
        List<TagHistoryRecord> records, CancellationToken ct)
    {
        // BatchSize 단위로 분할 INSERT
        for (int i = 0; i < records.Count; i += BatchSize)
        {
            var chunk = records.Skip(i).Take(BatchSize).ToList();

            // 단건 반복 INSERT (lssLib.DB.Sqlite 배치 API)
            foreach (var rec in chunk)
            {
                await _histRepo.ExecuteAsync(
                    $"INSERT INTO {HistoryTable} (tag_id, ts, value, quality) " +
                    $"VALUES (@ID, @TS, @VAL, @Q)",
                    [
                        DbParam.In("@ID",  rec.TagId),
                        DbParam.In("@TS",  rec.Timestamp.ToString("o")),
                        DbParam.In("@VAL", rec.Value),
                        DbParam.In("@Q",   rec.Quality),
                    ]);
            }

            LogManager.Instance.Debug(LogSrc,
                $"배치 저장 완료: {chunk.Count}건");
        }
    }

    // §6 ─ IAsyncDisposable ────────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _batchLock.Dispose();
        await _ctx.DisposeAsync();
        _disposed = true;
    }
}
