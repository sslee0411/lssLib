// ══════════════════════════════════════════════════════════
//  IIoT.Driver.Virtual · VirtualDriver.cs
//  역할: 가상 드라이버 실제 동작 구현 (IProtocolDriver)
//        ConnectAsync: 즉시 성공 (실 통신 없음)
//        ReadTagsAsync: SimMode 에 따라 Sine/Fixed 값 생성
//
//  HANDOFF C-02 설계 반영:
//    Sine 모드:  sin(elapsed / period × 2π) × (Max-Min)/2 + (Max+Min)/2
//    Fixed 모드: DriverParams["FixedValue"] 파싱
//    Tag별 위상 오프셋: TagId.GetHashCode() % 360 → 각 Tag마다 다른 값
//
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Contracts;
using System.Diagnostics;

namespace IIoT.Driver.Virtual;

/// <summary>
/// 가상 드라이버.
/// ConnectAsync → ReadTagsAsync (폴링) → DisposeAsync 순서로 사용.
/// 실제 네트워크 통신 없이 즉시 동작하므로 PLC 미보유 환경에서도 파이프라인 검증 가능.
/// </summary>
public sealed class VirtualDriver : IProtocolDriver
{
    // §1 ─ 상태 ────────────────────────────────────────────

    public string DriverName  => "virtual";
    public bool   IsConnected { get; private set; }

    public event Action<string>?         OnConnected;
    public event Action<string, string>? OnError;

    // §2 ─ 시뮬레이션 파라미터 ─────────────────────────────

    private string _simMode    = "Sine";
    private double _fixedValue = 0.0;
    private double _min        = 0.0;
    private double _max        = 100.0;
    private double _periodSec  = 10.0;

    /// <summary>
    /// 경과 시간 측정용 스톱워치.
    /// ConnectAsync 시점부터 시작 — Sine 위상 계산의 기준 시각.
    /// </summary>
    private readonly Stopwatch _elapsed = new();

    // §3 ─ 연결 / 해제 ─────────────────────────────────────

    /// <summary>
    /// 가상 연결 — 실제 네트워크 작업 없이 파라미터만 읽고 즉시 성공 처리.
    /// </summary>
    public Task<bool> ConnectAsync(DriverConfig config, CancellationToken ct = default)
    {
        _ReadParams(config);

        IsConnected = true;
        _elapsed.Restart();

        OnConnected?.Invoke(DriverName);
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        _elapsed.Reset();
        return Task.CompletedTask;
    }

    // §4 ─ 읽기 ────────────────────────────────────────────

    /// <summary>
    /// 태그별로 시뮬레이션 값을 생성합니다.
    /// Sine 모드: Tag 마다 TagId.GetHashCode() 기반 위상 오프셋을 적용하여
    ///            동시에 폴링되는 여러 Tag 가 서로 다른 파형을 갖도록 함.
    /// </summary>
    public Task<DriverReadResult> ReadTagsAsync(
        IReadOnlyList<TagReadRequest> tags,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            return Task.FromResult(
                DriverReadResult.Fail("미연결", TagQuality.Disconnected));

        var now    = DateTimeOffset.UtcNow;
        var values = new List<TagValue>(tags.Count);

        foreach (var tag in tags)
        {
            double raw = _simMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase)
                ? _fixedValue
                : _ComputeSineValue(tag.TagId);

            values.Add(new TagValue(
                TagId:     tag.TagId,
                RawValue:  raw,
                Quality:   TagQuality.Good,
                Timestamp: now
            ));
        }

        return Task.FromResult(DriverReadResult.Ok(values));
    }

    // §5 ─ 쓰기 ────────────────────────────────────────────

    /// <summary>가상 드라이버는 쓰기 미지원 (VirtualPlugin.GetCapabilities() 와 일치).</summary>
    public Task<DriverWriteResult> WriteTagAsync(
        TagWriteRequest tag, CancellationToken ct = default)
        => Task.FromResult(DriverWriteResult.Fail("가상 드라이버는 쓰기를 지원하지 않습니다."));

    // §6 ─ Sine 값 계산 ────────────────────────────────────

    /// <summary>
    /// sin(elapsed/period × 2π + phaseOffset) × (Max-Min)/2 + (Max+Min)/2
    /// <para>
    /// ★ HANDOFF 설계: Tag별 위상 오프셋 = TagId.GetHashCode() % 360 (도 단위 → 라디안 변환)
    ///    동일 폴링 시각이라도 Tag마다 다른 파형 위치를 가지도록 분산.
    /// </para>
    /// </summary>
    private double _ComputeSineValue(string tagId)
    {
        var period = _periodSec <= 0 ? 10.0 : _periodSec;

        // Tag별 위상 오프셋 (0~359도 → 라디안)
        var phaseDeg = Math.Abs(tagId.GetHashCode() % 360);
        var phaseRad = phaseDeg * Math.PI / 180.0;

        var t = _elapsed.Elapsed.TotalSeconds;
        var angle = (t / period) * 2.0 * Math.PI + phaseRad;

        var amplitude = (_max - _min) / 2.0;
        var offset    = (_max + _min) / 2.0;

        return Math.Sin(angle) * amplitude + offset;
    }

    // §7 ─ 파라미터 읽기 ───────────────────────────────────

    /// <summary>
    /// DriverConfig.Params 에서 VirtualPlugin.GetParameterSchema() 의 Key 와
    /// 동일한 이름으로 값을 읽습니다. TryParse 실패 시 기본값 사용.
    /// </summary>
    private void _ReadParams(DriverConfig config)
    {
        var p = config.Params ?? new();

        _simMode = p.GetValueOrDefault("SimMode", "Sine");

        _fixedValue = double.TryParse(
            p.GetValueOrDefault("FixedValue", "0"),
            out var fv) ? fv : 0.0;

        _min = double.TryParse(
            p.GetValueOrDefault("Min", "0"),
            out var min) ? min : 0.0;

        _max = double.TryParse(
            p.GetValueOrDefault("Max", "100"),
            out var max) ? max : 100.0;

        _periodSec = double.TryParse(
            p.GetValueOrDefault("PeriodSec", "10"),
            out var period) ? period : 10.0;
    }

    // §8 ─ 리소스 해제 ─────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        DisconnectAsync();
        return ValueTask.CompletedTask;
    }
}
