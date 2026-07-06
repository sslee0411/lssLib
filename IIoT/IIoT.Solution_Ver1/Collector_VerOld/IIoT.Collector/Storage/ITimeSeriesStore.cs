// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/ITimeSeriesStore.cs
//  역할: 시계열 저장소 추상화 인터페이스
//        SQLite/InfluxDB 를 동일한 API 로 사용할 수 있도록 추상화
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

namespace IIoT.Collector.Storage;

// ── 저장할 레코드 타입 ───────────────────────────────────

/// <summary>Tag 값 이력 레코드 (SDT 필터 통과한 것만 저장)</summary>
public sealed record TagHistoryRecord(
    string         TagId,
    string         TagName,
    string         PlcId,
    double         RawValue,
    double         EngValue,
    string         Unit,
    string         Quality,
    DateTimeOffset Timestamp
);

/// <summary>알람 이력 레코드 (모든 상태 변경 저장)</summary>
public sealed record AlarmHistoryRecord(
    string         AlarmKey,
    string         TagId,
    string         TagName,
    string         PlcId,
    string         Level,
    string         Status,
    string         Message,
    double         EngValue,
    DateTimeOffset OccurredAt
);

/// <summary>수집 통계 레코드 (1분 주기 집계)</summary>
public sealed record CollectorStatsRecord(
    string         PlcId,
    int            PollCount,
    int            ErrorCount,
    double         AvgPollMs,
    int            TagCount,
    DateTimeOffset Timestamp
);

// ── 저장소 인터페이스 ─────────────────────────────────────

/// <summary>
/// 시계열 저장소 인터페이스.
/// <see cref="SqliteTimeSeriesStore"/> 와 <see cref="InfluxDbTimeSeriesStore"/>
/// 가 이 인터페이스를 구현한다.
/// App.xaml.cs 에서 settings.json 의 Provider 값에 따라
/// 적절한 구현체를 DI 에 등록한다.
/// </summary>
public interface ITimeSeriesStore : IAsyncDisposable
{
    /// <summary>
    /// 저장소를 초기화합니다 (테이블 생성, 연결 확인 등).
    /// App 시작 시 1회 호출.
    /// </summary>
    Task InitializeAsync();

    /// <summary>Tag 값 이력을 저장합니다 (SDT 필터 통과 후 호출).</summary>
    Task WriteTagHistoryAsync(TagHistoryRecord record, CancellationToken ct = default);

    /// <summary>알람 이력을 저장합니다 (모든 상태 변경 시 호출).</summary>
    Task WriteAlarmHistoryAsync(AlarmHistoryRecord record, CancellationToken ct = default);

    /// <summary>수집 통계를 저장합니다 (1분 주기로 집계하여 호출).</summary>
    Task WriteStatsAsync(CollectorStatsRecord record, CancellationToken ct = default);

    /// <summary>
    /// 대기 중인 쓰기를 즉시 처리합니다.
    /// InfluxDB 배치 쓰기 시 Flush 용도.
    /// SQLite 는 즉시 쓰기이므로 no-op.
    /// </summary>
    Task FlushAsync(CancellationToken ct = default);
}
