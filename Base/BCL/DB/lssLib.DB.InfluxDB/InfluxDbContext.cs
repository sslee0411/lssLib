// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.InfluxDB · InfluxDbContext.cs
//  역할: InfluxDB v2.0 DbContext 구현
//        Flux 쿼리 / Line Protocol 쓰기 / 버킷 관리
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core.Flux.Domain;
using InfluxDB.Client.Writes;
using lssLib.DB.Abstractions;
using lssLib.DB.Core;

namespace lssLib.DB.InfluxDB;

/// <summary>
/// InfluxDB v2.0 DbContext 구현.
/// Flux 쿼리 / Line Protocol 쓰기 / Health Check 지원.
/// </summary>
/// <remarks>
/// InfluxDB는 관계형 DB와 다르게 트랜잭션이 없고 SQL 대신 Flux 쿼리를 사용한다.
/// <code>
/// // 생성
/// var cfg = new InfluxDbConfig("http://localhost:8086", "my-token", "my-org", "sensor-data");
/// await using var ctx = new InfluxDbContext(cfg);
/// await ctx.OpenAsync();
///
/// // Flux 쿼리
/// DbResult&lt;DataTable&gt; r = await ctx.QueryFluxAsync("""
///     from(bucket: "sensor-data")
///       |> range(start: -1h)
///       |> filter(fn: (r) => r._measurement == "sensor_data")
///     """);
///
/// // Line Protocol 쓰기
/// string line = new LineProtocolBuilder("sensor_data")
///     .Tag("plant", "A01")
///     .Field("temperature", 72.5)
///     .Timestamp(DateTime.UtcNow)
///     .Build();
/// DbResult&lt;int&gt; wr = await ctx.WriteLineProtocolAsync(line);
/// </code>
/// </remarks>
public sealed class InfluxDbContext : DbContextBase
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private InfluxDBClient? _client;
    private InfluxDbConfig _influxCfg;

    // InfluxDB는 ADO.NET IDbConnection을 사용하지 않으므로
    // 연결 상태를 직접 관리한다.
    private ConnectionState _state = ConnectionState.Closed;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="config">InfluxDB 연결 설정.</param>
    public InfluxDbContext(InfluxDbConfig config) : base(config)
    {
        _influxCfg = config;
    }

    // §3 ─ DbContextBase 추상 멤버 구현
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override DbProviderType ProviderType => DbProviderType.InfluxDB;

    /// <inheritdoc/>
    /// <remarks>InfluxDB는 IDbConnection을 사용하지 않으므로 NotSupportedException.</remarks>
    protected override IDbConnection CreateConnection()
        => throw new NotSupportedException(
            "InfluxDB는 IDbConnection을 사용하지 않습니다. OpenAsync()를 직접 사용하세요.");

    /// <inheritdoc/>
    protected override async Task OpenConnectionAsync(IDbConnection conn, CancellationToken ct)
    {
        // InfluxDB 연결은 InfluxDBClient 생성 + Health Check 로 처리
        _client = new InfluxDBClient(_influxCfg.Url, _influxCfg.Token);
        var health = await _client.PingAsync().ConfigureAwait(false);
        if (!health)
            throw new DbException(DbErrorCode.ConnectionFailed, ProviderType,
                $"InfluxDB Health Check 실패: {_influxCfg.Url}");
        _state = ConnectionState.Open;
    }

    // §4 ─ 연결 재정의 (IDbConnection 불사용)
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public new ConnectionState State => _state;

    /// <inheritdoc/>
    public new async Task OpenAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Open) return;
        try
        {
            _client = new InfluxDBClient(_influxCfg.Url, _influxCfg.Token);
            var health = await _client.PingAsync().ConfigureAwait(false);
            if (!health)
                throw new DbException(DbErrorCode.ConnectionFailed, ProviderType,
                    $"InfluxDB Health Check 실패: {_influxCfg.Url}");
            _state = ConnectionState.Open;
            Log($"InfluxDB 연결 성공: {_influxCfg.Url} / {_influxCfg.Bucket}");
        }
        catch (DbException) { throw; }
        catch (Exception ex)
        {
            throw new DbException(DbErrorCode.ConnectionFailed, ProviderType,
                ex.Message, innerException: ex);
        }
    }

    /// <inheritdoc/>
    public new Task CloseAsync()
    {
        _client?.Dispose();
        _client = null;
        _state = ConnectionState.Closed;
        Log("InfluxDB 연결 종료");
        return Task.CompletedTask;
    }

    // §5 ─ InfluxDB 전용 — Flux 쿼리
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Flux 쿼리를 실행하고 DataTable로 반환합니다.
    /// </summary>
    /// <param name="fluxQuery">Flux 쿼리 문자열.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>DataTable을 담은 DbResult.</returns>
    /// <example><code>
    /// var r = await ctx.QueryFluxAsync("""
    ///     from(bucket: "sensor-data")
    ///       |> range(start: -1h)
    ///       |> filter(fn: (r) => r._measurement == "sensor_data")
    ///       |> filter(fn: (r) => r.plant == "A01")
    ///     """);
    /// if (r.IsOk) grid.ItemsSource = r.Value?.DefaultView;
    /// </code></example>
    public async Task<DbResult<DataTable>> QueryFluxAsync(
        string fluxQuery,
        CancellationToken ct = default)
    {
        EnsureClientReady();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var tables = await _client!.GetQueryApi()
                .QueryAsync(fluxQuery, _influxCfg.Org, ct)
                .ConfigureAwait(false);

            var dt = FluxTablesToDataTable(tables);
            sw.Stop();
            Log($"Flux Query ({sw.ElapsedMilliseconds}ms) rows={dt.Rows.Count}");
            return DbResult<DataTable>.Ok(dt, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<DataTable>.Timeout("Flux 쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"Flux Query 실패: {ex.Message}");
            return DbResult<DataTable>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Flux 쿼리를 실행하고 지정 타입의 목록으로 반환합니다.
    /// </summary>
    /// <typeparam name="T">매핑 엔티티 타입.</typeparam>
    /// <param name="fluxQuery">Flux 쿼리 문자열.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>엔티티 목록을 담은 DbResult.</returns>
    public async Task<DbResult<List<T>>> QueryFluxAsync<T>(
        string fluxQuery,
        CancellationToken ct = default) where T : class, new()
    {
        EnsureClientReady();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var list = await _client!.GetQueryApi()
                .QueryAsync<T>(fluxQuery, _influxCfg.Org, ct)
                .ConfigureAwait(false);

            sw.Stop();
            Log($"Flux Query<{typeof(T).Name}> ({sw.ElapsedMilliseconds}ms) count={list.Count}");
            return DbResult<List<T>>.Ok(list, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return DbResult<List<T>>.Timeout("Flux 쿼리 취소됨", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"Flux Query<{typeof(T).Name}> 실패: {ex.Message}");
            return DbResult<List<T>>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    // §6 ─ InfluxDB 전용 — Line Protocol 쓰기
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Line Protocol 문자열로 단건 데이터를 씁니다.
    /// </summary>
    /// <param name="lineProtocol">Line Protocol 문자열.</param>
    /// <param name="precision">타임스탬프 정밀도 (기본: Nanoseconds).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>쓰기 성공 시 행 수 1, 실패 시 0을 담은 DbResult.</returns>
    /// <example><code>
    /// string line = new LineProtocolBuilder("sensor_data")
    ///     .Tag("plant", "A01")
    ///     .Field("temperature", 72.5)
    ///     .Timestamp(DateTime.UtcNow)
    ///     .Build();
    /// await ctx.WriteLineProtocolAsync(line);
    /// </code></example>
    public async Task<DbResult<int>> WriteLineProtocolAsync(
        string lineProtocol,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
    {
        EnsureClientReady();
        if (string.IsNullOrWhiteSpace(lineProtocol))
            return DbResult<int>.Fail("Line Protocol 문자열이 비어 있습니다.");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _client!.GetWriteApiAsync()
                .WriteRecordAsync(lineProtocol, precision,
                    _influxCfg.Bucket, _influxCfg.Org, ct)
                .ConfigureAwait(false);

            sw.Stop();
            Log($"Write ({sw.ElapsedMilliseconds}ms): 1 point → {_influxCfg.Bucket}");
            return DbResult<int>.Ok(1, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"Write 실패: {ex.Message}");
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Line Protocol 문자열 목록으로 배치 데이터를 씁니다.
    /// </summary>
    /// <param name="lineProtocols">Line Protocol 문자열 목록.</param>
    /// <param name="precision">타임스탬프 정밀도 (기본: Nanoseconds).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>쓴 행 수를 담은 DbResult.</returns>
    public async Task<DbResult<int>> WriteBatchAsync(
        IEnumerable<string> lineProtocols,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
    {
        EnsureClientReady();
        var lines = lineProtocols
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return DbResult<int>.Ok(0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _client!.GetWriteApiAsync()
                .WriteRecordsAsync(lines, precision,
                    _influxCfg.Bucket, _influxCfg.Org, ct)
                .ConfigureAwait(false);

            sw.Stop();
            Log($"WriteBatch ({sw.ElapsedMilliseconds}ms): {lines.Count} points → {_influxCfg.Bucket}");
            return DbResult<int>.Ok(lines.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError($"WriteBatch 실패: {ex.Message}");
            return DbResult<int>.Error(ex, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// LineProtocolBuilder 인스턴스로 단건 데이터를 씁니다.
    /// </summary>
    public Task<DbResult<int>> WriteAsync(
        LineProtocolBuilder builder,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
        => WriteLineProtocolAsync(builder.Build(), precision, ct);

    // §7 ─ InfluxDB 전용 — 버킷 / Health
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// InfluxDB 서버 Health Check.
    /// </summary>
    public async Task<bool> PingAsync()
    {
        if (_client is null) return false;
        try { return await _client.PingAsync().ConfigureAwait(false); }
        catch { return false; }
    }

    /// <summary>
    /// 버킷 존재 여부 확인.
    /// </summary>
    public async Task<bool> BucketExistsAsync(
        string? bucketName = null,
        CancellationToken ct = default)
    {
        EnsureClientReady();
        var name = bucketName ?? _influxCfg.Bucket;
        try
        {
            var bucket = await _client!.GetBucketsApi()
                .FindBucketByNameAsync(name, ct)
                .ConfigureAwait(false);
            return bucket is not null;
        }
        catch { return false; }
    }

    /// <summary>
    /// 버킷 생성.
    /// </summary>
    /// <param name="bucketName">생성할 버킷 이름.</param>
    /// <param name="retentionHours">데이터 보존 기간 (시간, 0 = 영구).</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task<DbResult<int>> CreateBucketAsync(
        string bucketName,
        int retentionHours = 0,
        CancellationToken ct = default)
    {
        EnsureClientReady();
        try
        {
            var org = await _client!.GetOrganizationsApi()
                .FindOrganizationsAsync(org: _influxCfg.Org, cancellationToken: ct)
                .ConfigureAwait(false);

            if (org is null || org.Count == 0)
                return DbResult<int>.Fail($"Organization '{_influxCfg.Org}'을 찾을 수 없습니다.");

            var retention = retentionHours > 0
                ? new BucketRetentionRules(BucketRetentionRules.TypeEnum.Expire,
                    retentionHours * 3600L)
                : new BucketRetentionRules(BucketRetentionRules.TypeEnum.Expire, 0L);

            await _client.GetBucketsApi()
                .CreateBucketAsync(bucketName, retention, org[0].Id, ct)
                .ConfigureAwait(false);

            Log($"버킷 생성 완료: {bucketName}");
            return DbResult<int>.Ok(1);
        }
        catch (Exception ex)
        {
            LogError($"버킷 생성 실패: {ex.Message}");
            return DbResult<int>.Error(ex);
        }
    }

    // §8 ─ DbContextBase 추상 메서드 — InfluxDB 미지원 명시
    // ─────────────────────────────────────────────────────────────────
    // InfluxDB는 ADO.NET SQL 실행 모델을 지원하지 않으므로
    // Execute / QueryTable / CallSp 는 NotSupported 처리한다.
    // InfluxDB 전용 메서드(QueryFluxAsync, WriteLineProtocolAsync)를 사용할 것.

    /// <inheritdoc/>
    protected override Task<DbResult<int>> ExecuteCoreAsync(
        string sql, CommandType commandType, DbParam[]? parameters, CancellationToken ct)
        => Task.FromResult(DbResult<int>.Fail(
            "InfluxDB는 SQL ExecuteNonQuery를 지원하지 않습니다. WriteLineProtocolAsync()를 사용하세요."));

    /// <inheritdoc/>
    protected override Task<DbResult<DataTable>> QueryTableCoreAsync(
        string sql, CommandType commandType, DbParam[]? parameters, CancellationToken ct)
        => Task.FromResult(DbResult<DataTable>.Fail(
            "InfluxDB는 SQL QueryTable을 지원하지 않습니다. QueryFluxAsync()를 사용하세요."));

    /// <inheritdoc/>
    protected override Task<DbResult<SpResult>> CallSpCoreAsync(
        string spName, DbParam[]? parameters, CancellationToken ct)
        => Task.FromResult(DbResult<SpResult>.Fail(
            "InfluxDB는 Stored Procedure를 지원하지 않습니다."));

    // §9 ─ 내부 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Flux Tables → DataTable 변환.</summary>
    private static DataTable FluxTablesToDataTable(List<FluxTable> tables)
    {
        var dt = new DataTable();
        bool columnsBuilt = false;

        foreach (var table in tables)
        {
            // 첫 번째 테이블 기준으로 컬럼 생성
            if (!columnsBuilt)
            {
                foreach (var col in table.Columns)
                {
                    var colName = col.Label;
                    // 중복 컬럼명 처리
                    if (!dt.Columns.Contains(colName))
                        dt.Columns.Add(colName, typeof(string));
                }
                columnsBuilt = true;
            }

            foreach (var record in table.Records)
            {
                var row = dt.NewRow();
                foreach (var col in table.Columns)
                {
                    var val = record.GetValueByKey(col.Label);
                    row[col.Label] = val?.ToString() ?? string.Empty;
                }
                dt.Rows.Add(row);
            }
        }

        return dt;
    }

    private void EnsureClientReady()
    {
        if (_client is null || _state != ConnectionState.Open)
            throw new InvalidOperationException(
                "InfluxDB 연결이 열려 있지 않습니다. OpenAsync()를 먼저 호출하세요.");
    }

    // §10 ─ Dispose
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public new async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
    // public override async ValueTask DisposeAsync() ->
    // public new async ValueTask DisposeAsync() 로 수정함

}