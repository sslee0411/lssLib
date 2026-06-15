// ══════════════════════════════════════════════════════════
//  IIoT.CollectorRuntime · Collection/SwingingDoorCompressor.cs
//  역할: Swinging Door Trending (SDT) 압축 알고리즘
//        연속 수집 데이터에서 의미 있는 변화점만 저장 (95%+ 절감)
//  Phase 8: 신규
//
//  SDT 알고리즘 개요:
//    문의 상단 경사(upper slope)와 하단 경사(lower slope)를 유지.
//    새 포인트가 두 경사 사이에 있으면 "변화 없음" → 저장 생략.
//    새 포인트가 경사 범위를 벗어나면 "변화 있음" → 저장.
//
//  파라미터:
//    devBand : 편차 허용 범위 (예: 0.5 → ±0.5)
//              값이 작을수록 정밀도 높고 저장 포인트 많아짐
//    maxTime : 최대 저장 간격 (예: 300초)
//              변화 없어도 이 시간 경과 시 강제 저장 (데이터 공백 방지)
// ══════════════════════════════════════════════════════════

namespace IIoT.CollectorRuntime.Collection;

/// <summary>
/// SDT(Swinging Door Trending) 압축기.
///
/// 태그별로 독립 인스턴스를 생성하여 사용합니다.
/// (CollectionEngine 에서 태그별로 1개씩 생성)
///
/// 사용 패턴:
/// <code>
///   var compressor = new SwingingDoorCompressor(devBand: 0.5, maxTimeSec: 300);
///   if (compressor.ShouldStore(value, timestamp))
///       await _db.InsertAsync(tagId, value, timestamp);
/// </code>
/// </summary>
public sealed class SwingingDoorCompressor
{
    // §1 ─ 파라미터 ────────────────────────────────────────────
    private readonly double _devBand;      // 편차 허용 범위
    private readonly double _maxTimeSec;   // 최대 저장 간격 (초)

    // §2 ─ 알고리즘 상태 ──────────────────────────────────────
    private double?  _archiveValue;     // 가장 최근 저장된 값
    private DateTime _archiveTime;      // 가장 최근 저장 시각
    private double   _upperSlope;       // 문의 상단 경사
    private double   _lowerSlope;       // 문의 하단 경사
    private bool     _initialized;

    // §3 ─ 통계 (모니터링용) ──────────────────────────────────
    public int SkippedCount  { get; private set; }
    public int StoredCount   { get; private set; }
    public double CompressionRatio =>
        StoredCount + SkippedCount == 0 ? 0
        : 1.0 - (double)StoredCount / (StoredCount + SkippedCount);

    // §4 ─ 생성자 ─────────────────────────────────────────────

    /// <summary>
    /// SDT 압축기 생성.
    /// </summary>
    /// <param name="devBand">
    ///   편차 허용 범위.
    ///   0.5 → 마지막 저장값 ±0.5 이내 변화는 저장 생략.
    ///   스케일이 큰 태그(예: 0~10000rpm)는 더 큰 값(예: 5)을 사용.
    /// </param>
    /// <param name="maxTimeSec">
    ///   강제 저장 최대 간격(초). 기본 300초 (5분).
    ///   이 시간 경과 시 변화 없어도 저장하여 데이터 공백 방지.
    /// </param>
    public SwingingDoorCompressor(double devBand = 0.5, double maxTimeSec = 300)
    {
        _devBand    = devBand;
        _maxTimeSec = maxTimeSec;
    }

    // §5 ─ 핵심 메서드 ─────────────────────────────────────────

    /// <summary>
    /// 새 데이터 포인트를 저장해야 하는지 판단합니다.
    ///
    /// true  → 저장 필요 (의미 있는 변화 또는 최대 시간 초과)
    /// false → 저장 불필요 (이전값과 편차 내)
    /// </summary>
    /// <param name="value">새로 수집된 값</param>
    /// <param name="timestamp">수집 시각</param>
    public bool ShouldStore(double value, DateTime timestamp)
    {
        // NaN / 무한대는 항상 저장 (품질 변화)
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            _StorePoint(value, timestamp);
            return true;
        }

        // 초기화: 첫 번째 포인트는 항상 저장
        if (!_initialized)
        {
            _InitializeSlopes(value, timestamp);
            _StorePoint(value, timestamp);
            return true;
        }

        double elapsed = (timestamp - _archiveTime).TotalSeconds;

        // ① 최대 시간 초과 → 강제 저장
        if (elapsed >= _maxTimeSec)
        {
            _InitializeSlopes(value, timestamp);
            _StorePoint(value, timestamp);
            return true;
        }

        // ② SDT 경사 검사
        double slope = (value - _archiveValue!.Value) / elapsed;

        if (slope > _upperSlope || slope < _lowerSlope)
        {
            // 경사 범위 이탈 → 변화 감지, 저장
            _InitializeSlopes(value, timestamp);
            _StorePoint(value, timestamp);
            return true;
        }

        // ③ 경사 범위 축소 (문을 점점 닫기)
        double newUpper = (_archiveValue.Value + _devBand - value) / elapsed;
        double newLower = (_archiveValue.Value - _devBand - value) / elapsed;

        _upperSlope = Math.Min(_upperSlope, newUpper);
        _lowerSlope = Math.Max(_lowerSlope, newLower);

        // 생략
        SkippedCount++;
        return false;
    }

    /// <summary>
    /// 압축기 상태를 초기화합니다.
    /// 수집 재시작 시 호출합니다.
    /// </summary>
    public void Reset()
    {
        _initialized  = false;
        _archiveValue = null;
        _archiveTime  = default;
        SkippedCount  = 0;
        StoredCount   = 0;
    }

    // §6 ─ 내부 헬퍼 ──────────────────────────────────────────

    private void _InitializeSlopes(double value, DateTime timestamp)
    {
        double elapsed = _initialized
            ? Math.Max((timestamp - _archiveTime).TotalSeconds, 0.001)
            : 1.0;

        double baseSlope = _initialized
            ? (value - _archiveValue!.Value) / elapsed
            : 0.0;

        _upperSlope  = baseSlope + _devBand / elapsed;
        _lowerSlope  = baseSlope - _devBand / elapsed;
        _initialized = true;
    }

    private void _StorePoint(double value, DateTime timestamp)
    {
        _archiveValue = value;
        _archiveTime  = timestamp;
        StoredCount++;
    }
}

/// <summary>
/// 여러 태그의 SDT 압축기를 관리하는 레지스트리.
/// CollectionEngine 이 보유하며 태그별로 압축기를 자동 생성합니다.
/// </summary>
public sealed class CompressorRegistry
{
    private readonly double _defaultDevBand;
    private readonly double _defaultMaxTimeSec;
    private readonly Dictionary<string, SwingingDoorCompressor> _compressors = [];

    public CompressorRegistry(double defaultDevBand = 0.5, double defaultMaxTimeSec = 300)
    {
        _defaultDevBand    = defaultDevBand;
        _defaultMaxTimeSec = defaultMaxTimeSec;
    }

    /// <summary>태그별 압축기 취득 (없으면 자동 생성)</summary>
    public SwingingDoorCompressor GetOrCreate(string tagId,
        double? devBand = null, double? maxTimeSec = null)
    {
        if (!_compressors.TryGetValue(tagId, out var c))
        {
            c = new SwingingDoorCompressor(
                devBand    ?? _defaultDevBand,
                maxTimeSec ?? _defaultMaxTimeSec);
            _compressors[tagId] = c;
        }
        return c;
    }

    /// <summary>모든 압축기 리셋 (수집 재시작 시)</summary>
    public void ResetAll()
    {
        foreach (var c in _compressors.Values) c.Reset();
    }

    /// <summary>전체 압축률 (저장 건수 / 전체 건수)</summary>
    public double OverallCompressionRatio
    {
        get
        {
            int total  = _compressors.Values.Sum(c => c.StoredCount + c.SkippedCount);
            int stored = _compressors.Values.Sum(c => c.StoredCount);
            return total == 0 ? 0 : 1.0 - (double)stored / total;
        }
    }
}
