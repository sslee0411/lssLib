// ══════════════════════════════════════════════════════════════════════
//  lssLib.DB.InfluxDB · InfluxRepository.cs
//  역할: InfluxDB Repository 구현
//        Flux 쿼리 + Line Protocol 쓰기 + RowMapper 통합
//
//  Ver History:
//  v1.0.0  2025-05-19  최초 작성
// ══════════════════════════════════════════════════════════════════════

using System.Data;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using lssLib.DB.Abstractions;
using lssLib.DB.Contracts;
using lssLib.DB.Core;

namespace lssLib.DB.InfluxDB;

/// <summary>
/// InfluxDB Repository 구현.
/// Flux 쿼리 결과를 RowMapper로 엔티티 목록으로 변환하고,
/// LineProtocolBuilder로 데이터를 쓴다.
/// </summary>
/// <typeparam name="T">엔티티 타입.</typeparam>
/// <example><code>
/// // ① RowMapper 정의
/// RowMapper<SensorRow> mapper = row => new SensorRow
/// {
///     Time        = DateTime.Parse(row["_time"].ToString()!),
///     Measurement = row["_measurement"].ToString()!,
///     Plant       = row["plant"].ToString()!,
///     Value       = double.Parse(row["_value"].ToString()!),
/// };
///
/// // ② Repository 생성
/// var repo = new InfluxRepository<SensorRow>(ctx, mapper);
///
/// // ③ 조회
/// DbResult<List<SensorRow>> r = await repo.QueryFluxAsync("""
///     from(bucket: "sensor-data")
///       |> range(start: -1h)
///       |> filter(fn: (r) => r._measurement == "sensor_data")
///     """);
///
/// // ④ 쓰기 (간편 API)
/// await repo.WriteAsync("sensor_data",
///     tags:   [("plant", "A01"), ("line", "L1")],
///     fields: [("temperature", (object)72.5), ("pressure", 1.013)],
///     time:   DateTime.UtcNow);
/// </code></example>
public sealed class InfluxRepository<T> : RepositoryBase<T> where T : class
{
    // §1 ─ 필드
    // ─────────────────────────────────────────────────────────────────
    private readonly InfluxDbContext _influxCtx;
    private readonly RowMapper<T> _rowMapper;

    // §2 ─ 생성자
    // ─────────────────────────────────────────────────────────────────

    /// <param name="context">InfluxDB 컨텍스트.</param>
    /// <param name="mapper">DataRow → T 변환 함수.</param>
    public InfluxRepository(InfluxDbContext context, RowMapper<T> mapper)
        : base(context, mapper)
    {
        _influxCtx = context;
        _rowMapper = mapper;
    }

    // §3 ─ Flux 쿼리 → 엔티티 목록
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Flux 쿼리를 실행하고 RowMapper로 엔티티 목록을 반환합니다.
    /// </summary>
    /// <param name="fluxQuery">Flux 쿼리 문자열.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>엔티티 목록을 담은 DbResult.</returns>
    public async Task<DbResult<List<T>>> QueryFluxAsync(
        string fluxQuery,
        CancellationToken ct = default)
    {
        var tableResult = await _influxCtx.QueryFluxAsync(fluxQuery, ct)
            .ConfigureAwait(false);

        if (!tableResult.IsOk)
            return DbResult<List<T>>.Fail(tableResult.Message, tableResult.ElapsedMs);

        try
        {
            var list = new List<T>();
            foreach (DataRow row in tableResult.Value!.Rows)
                list.Add(_rowMapper(row));

            return DbResult<List<T>>.Ok(list, tableResult.ElapsedMs);
        }
        catch (Exception ex)
        {
            return DbResult<List<T>>.Error(ex, tableResult.ElapsedMs);
        }
    }

    // §4 ─ Line Protocol 쓰기 (간편 API)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tags / Fields / Timestamp 를 받아 Line Protocol로 변환 후 씁니다.
    /// </summary>
    /// <param name="measurement">Measurement 이름.</param>
    /// <param name="tags">Tag 목록 (키-값 튜플 배열).</param>
    /// <param name="fields">Field 목록 (키-값 튜플 배열).</param>
    /// <param name="time">타임스탬프 (기본: DateTime.UtcNow).</param>
    /// <param name="precision">타임스탬프 정밀도 (기본: Nanoseconds).</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>성공 시 1을 담은 DbResult.</returns>
    public Task<DbResult<int>> WriteAsync(
        string measurement,
        (string Key, string Value)[] tags,
        (string Key, object? Value)[] fields,
        DateTime? time = null,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
    {
        var builder = new LineProtocolBuilder(measurement);

        foreach (var (k, v) in tags)
            builder.Tag(k, v);

        foreach (var (k, v) in fields)
            builder.Field(k, v);

        builder.Timestamp(time ?? DateTime.UtcNow);

        return _influxCtx.WriteAsync(builder, precision, ct);
    }

    /// <summary>
    /// LineProtocolBuilder 인스턴스로 단건 씁니다.
    /// </summary>
    public Task<DbResult<int>> WriteAsync(
        LineProtocolBuilder builder,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
        => _influxCtx.WriteAsync(builder, precision, ct);

    /// <summary>
    /// 여러 LineProtocolBuilder 인스턴스를 배치로 씁니다.
    /// </summary>
    public Task<DbResult<int>> WriteBatchAsync(
        IEnumerable<LineProtocolBuilder> builders,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
        => _influxCtx.WriteBatchAsync(
            builders.Select(b => b.Build()), precision, ct);

    /// <summary>
    /// Line Protocol 문자열 목록을 배치로 씁니다.
    /// </summary>
    public Task<DbResult<int>> WriteBatchAsync(
        IEnumerable<string> lineProtocols,
        WritePrecision precision = WritePrecision.Ns,
        CancellationToken ct = default)
        => _influxCtx.WriteBatchAsync(lineProtocols, precision, ct);
}