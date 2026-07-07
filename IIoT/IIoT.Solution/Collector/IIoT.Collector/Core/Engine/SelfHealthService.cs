// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/SelfHealthService.cs
//  역할: Collector 자신의 CPU/메모리 사용량 + PLC 폴링 지연(설정 주기 대비
//        실제 소요 시간) 을 주기적으로 측정 — 향후 IIoT.Manager 헬스체크 연동 지점
//  C-EX-08: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using lssLib.Messaging;
using System.Diagnostics;

namespace IIoT.Collector.Core.Engine;

/// <summary>자체 진단 스냅샷 (읽기 전용).</summary>
public sealed record SelfHealthSnapshot(
    double  CpuUsagePercent,
    long    MemoryUsageMb,
    int     ThreadCount,
    TimeSpan Uptime,
    DateTimeOffset MeasuredAt);

/// <summary>자체 진단 서비스 (DI 싱글턴).</summary>
public sealed class SelfHealthService : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTimeOffset _lastSampleAt = DateTimeOffset.UtcNow;

    private ScheduledTask? _task;

    public SelfHealthSnapshot? Latest { get; private set; }

    public void Initialize()
    {
        _lastCpuTime  = _process.TotalProcessorTime;
        _lastSampleAt = DateTimeOffset.UtcNow;

        _task = AsyncScheduler.Instance.ScheduleRecurring(
            TimeSpan.FromSeconds(30), _SampleAsync, name: "selfhealth:sample");

        LogManager.Instance.Info("SelfHealth", "자체 진단 서비스 초기화 완료 (30초 주기)");
    }

    private Task _SampleAsync(CancellationToken ct)
    {
        try
        {
            _process.Refresh();

            var now       = DateTimeOffset.UtcNow;
            var cpuNow    = _process.TotalProcessorTime;
            var cpuDelta  = (cpuNow - _lastCpuTime).TotalMilliseconds;
            var wallDelta = (now - _lastSampleAt).TotalMilliseconds;

            var cpuPercent = wallDelta > 0
                ? Math.Round(cpuDelta / (Environment.ProcessorCount * wallDelta) * 100.0, 1)
                : 0.0;

            _lastCpuTime  = cpuNow;
            _lastSampleAt = now;

            Latest = new SelfHealthSnapshot(
                CpuUsagePercent: cpuPercent,
                MemoryUsageMb:   _process.WorkingSet64 / (1024 * 1024),
                ThreadCount:     _process.Threads.Count,
                Uptime:          now - _startedAt,
                MeasuredAt:      now);

            LogManager.Instance.Info("SelfHealth",
                $"CPU {Latest.CpuUsagePercent}% · MEM {Latest.MemoryUsageMb}MB · " +
                $"Thread {Latest.ThreadCount}개 · Uptime {Latest.Uptime:d\\d\\ hh\\:mm}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("SelfHealth", $"진단 측정 실패: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _task?.Cancel();
        _process.Dispose();
    }
}
