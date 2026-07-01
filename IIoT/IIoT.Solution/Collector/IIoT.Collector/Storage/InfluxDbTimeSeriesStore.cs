// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/InfluxDbTimeSeriesStore.cs
//  역할: InfluxDB v2 기반 시계열 저장소 구현 (HTTP Line Protocol)
//        추가 NuGet 패키지 없음 — HttpClient 직접 사용
//        settings.json 의 Storage.Provider = "InfluxDB" 일 때 활성화
//
//  ━━━ InfluxDB 환경 구성 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//
//  [설치 — Docker (권장)]
//  docker run -d \
//    --name influxdb \
//    -p 8086:8086 \
//    -v influxdb2-data:/var/lib/influxdb2 \
//    influxdb:2.7
//
//  [설치 — Windows 직접]
//  1. https://portal.influxdata.com/downloads/ → InfluxDB v2.x Windows 다운로드
//  2. influxd.exe 실행
//  3. 브라우저 → http://localhost:8086 → 초기 설정
//     - Username / Password
//     - Organization (예: my-org)
//     - Bucket (예: iiot)
//     - 완료 후 API Token 복사 (settings.json Token 에 붙여넣기)
//
//  [settings.json 설정 예시]
//  {
//    "Storage": {
//      "Provider": "InfluxDB",
//      "InfluxDB": {
//        "Url": "http://localhost:8086",
//        "Token": "복사한_API_Token",
//        "Org": "my-org",
//        "Bucket": "iiot",
//        "BatchSize": 500,
//        "FlushIntervalMs": 5000
//      }
//    }
//  }
//
//  [InfluxDB 데이터 조회]
//  브라우저 → http://localhost:8086 → Explore
//  Flux 쿼리 예시:
//    from(bucket: "iiot")
//      |> range(start: -1h)
//      |> filter(fn: (r) => r._measurement == "tag_values")
//      |> filter(fn: (r) => r.tag_id == "T001")
//
//  ━━━ Line Protocol 구조 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  measurement,tag_key=val field_key=value timestamp_ns
//
//  예시:
//    tag_values,plc_id=PLC-01,tag_id=T001,unit=bar raw=1234.5,eng=12.3 1719600000000000000
//    alarm_history,tag_id=T001,level=HH status="Active",eng=95.2 1719600000000000000
//    collector_stats,plc_id=PLC-01 poll_count=60i,error_count=0i,avg_poll_ms=12.5 1719600000000000000
//
//  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  C-07: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.Log;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using lssLib.DB.Core;

namespace IIoT.Collector.Storage;

/// <summary>
/// InfluxDB v2 시계열 저장소 (HTTP Line Protocol 직접 구현).
/// <para>
/// <b>추가 NuGet 패키지 없음</b> — HttpClient 와 Line Protocol 문자열만 사용한다.
/// Collector 는 Write 위주이므로 공식 SDK 의 복잡한 Query API 가 불필요하다.
/// </para>
/// <para>
/// <b>배치 쓰기:</b><br/>
/// BatchSize 건 또는 FlushIntervalMs 경과 시 HTTP POST 를 한 번에 전송한다.
/// 전송 실패 시 최대 3회 재시도, 이후 경고 로그만 남기고 계속 진행한다.
/// </para>
/// </summary>
public sealed class InfluxDbTimeSeriesStore : ITimeSeriesStore
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;
    private          HttpClient?             _http;
    private          string                  _writeUrl = string.Empty;

    private readonly List<string>  _batch     = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private          DateTime      _lastFlush = DateTime.UtcNow;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public InfluxDbTimeSeriesStore(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var s = _settingsLoader.Settings.Storage.InfluxDB;

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", s.Token);

        _writeUrl = $"{s.Url.TrimEnd('/')}/api/v2/write" +
                    $"?org={Uri.EscapeDataString(s.Org)}" +
                    $"&bucket={Uri.EscapeDataString(s.Bucket)}" +
                    $"&precision=ns";

        // 연결 확인 (헬스체크 — 실패해도 계속 진행)
        try
        {
            var healthUrl = $"{s.Url.TrimEnd('/')}/health";
            var resp = await _http.GetAsync(healthUrl);
            if (resp.IsSuccessStatusCode)
                LogManager.Instance.Info("InfluxStore",
                    $"InfluxDB 연결 확인 완료: {s.Url} (Org={s.Org}, Bucket={s.Bucket})");
            else
                LogManager.Instance.Warn("InfluxStore",
                    $"InfluxDB 헬스체크 실패 HTTP {(int)resp.StatusCode}: {s.Url}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("InfluxStore",
                $"InfluxDB 연결 확인 실패 (수집은 계속 진행): {ex.Message}");
        }
    }

    // §4 ─ Tag 값 이력 저장 ────────────────────────────────

    public async Task WriteTagHistoryAsync(TagHistoryRecord r, CancellationToken ct = default)
    {
        // InfluxDB Line Protocol:
        // measurement,tag_key=val field_key=value timestamp_ns
        var line = _LineTag(r);
        await _AddToBatchAsync(line, ct);
    }

    // §5 ─ 알람 이력 저장 ──────────────────────────────────

    public async Task WriteAlarmHistoryAsync(AlarmHistoryRecord r, CancellationToken ct = default)
    {
        var line = _LineAlarm(r);
        await _AddToBatchAsync(line, ct);
    }

    // §6 ─ 수집 통계 저장 ──────────────────────────────────

    public async Task WriteStatsAsync(CollectorStatsRecord r, CancellationToken ct = default)
    {
        var line = _LineStats(r);
        await _AddToBatchAsync(line, ct);
    }

    // §7 ─ Flush ───────────────────────────────────────────

    public async Task FlushAsync(CancellationToken ct = default)
    {
        await _FlushBatchAsync(ct);
    }

    // §8 ─ Line Protocol 변환 헬퍼 ─────────────────────────

    private static string _LineTag(TagHistoryRecord r)
    {
        var tsNs = r.Timestamp.ToUnixTimeMilliseconds() * 1_000_000L;
        // 태그 값: 공백·쉼표 이스케이프 (Line Protocol 규칙)
        var tagId   = _Escape(r.TagId);
        var tagName = _EscapeField(r.TagName);
        var unit    = _EscapeField(r.Unit);
        var quality = _EscapeField(r.Quality);

        return $"tag_values,plc_id={_Escape(r.PlcId)},tag_id={tagId} " +
               $"tag_name={tagName},raw={r.RawValue},eng={r.EngValue}," +
               $"unit={unit},quality={quality} {tsNs}";
    }

    private static string _LineAlarm(AlarmHistoryRecord r)
    {
        var tsNs = r.OccurredAt.ToUnixTimeMilliseconds() * 1_000_000L;
        return $"alarm_history,tag_id={_Escape(r.TagId)},level={_Escape(r.Level)} " +
               $"alarm_key={_EscapeField(r.AlarmKey)},tag_name={_EscapeField(r.TagName)}," +
               $"plc_id={_EscapeField(r.PlcId)},status={_EscapeField(r.Status)}," +
               $"message={_EscapeField(r.Message)},eng={r.EngValue} {tsNs}";
    }

    private static string _LineStats(CollectorStatsRecord r)
    {
        var tsNs = r.Timestamp.ToUnixTimeMilliseconds() * 1_000_000L;
        return $"collector_stats,plc_id={_Escape(r.PlcId)} " +
               $"poll_count={r.PollCount}i,error_count={r.ErrorCount}i," +
               $"avg_poll_ms={r.AvgPollMs},tag_count={r.TagCount}i {tsNs}";
    }

    /// <summary>태그 키/값의 공백·쉼표·= 이스케이프 (Line Protocol 규칙)</summary>
    private static string _Escape(string s)
        => s.Replace(" ", @"\ ").Replace(",", @"\,").Replace("=", @"\=");

    /// <summary>문자열 필드 값 쌍따옴표 감싸기 + 내부 따옴표 이스케이프</summary>
    private static string _EscapeField(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // §9 ─ 배치 관리 ───────────────────────────────────────

    private async Task _AddToBatchAsync(string line, CancellationToken ct)
    {
        var s = _settingsLoader.Settings.Storage.InfluxDB;

        await _batchLock.WaitAsync(ct);
        try
        {
            _batch.Add(line);

            var shouldFlush = _batch.Count >= s.BatchSize ||
                              (DateTime.UtcNow - _lastFlush).TotalMilliseconds >= s.FlushIntervalMs;

            if (shouldFlush)
                await _FlushBatchInternalAsync(ct);
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private async Task _FlushBatchAsync(CancellationToken ct)
    {
        await _batchLock.WaitAsync(ct);
        try { await _FlushBatchInternalAsync(ct); }
        finally { _batchLock.Release(); }
    }

    private async Task _FlushBatchInternalAsync(CancellationToken ct)
    {
        if (_batch.Count == 0 || _http is null) return;

        var payload = string.Join("\n", _batch);
        _batch.Clear();
        _lastFlush = DateTime.UtcNow;

        // 최대 3회 재시도
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var content = new StringContent(payload, Encoding.UTF8, "text/plain");
                var resp = await _http.PostAsync(_writeUrl, content, ct);

                if (resp.IsSuccessStatusCode)
                    return; // 성공

                var body = await resp.Content.ReadAsStringAsync(ct);
                LogManager.Instance.Warn("InfluxStore",
                    $"HTTP {(int)resp.StatusCode} (시도 {attempt}/3): {body[..Math.Min(200, body.Length)]}");
            }
            catch (Exception ex) when (attempt < 3)
            {
                LogManager.Instance.Warn("InfluxStore",
                    $"전송 실패 (시도 {attempt}/3): {ex.Message} — 1초 후 재시도");
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                LogManager.Instance.Error("InfluxStore",
                    $"전송 최종 실패 (데이터 {payload.Count(c => c == '\n') + 1}건 유실): {ex.Message}");
                return;
            }
        }
    }

    // §10 ─ 리소스 해제 ────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        // 남은 배치 전송 시도
        try { await _FlushBatchAsync(CancellationToken.None); }
        catch { /* 종료 중 오류 무시 */ }

        _http?.Dispose();
        LogManager.Instance.Info("InfluxStore", "InfluxDB 연결 해제 완료");
    }
}
