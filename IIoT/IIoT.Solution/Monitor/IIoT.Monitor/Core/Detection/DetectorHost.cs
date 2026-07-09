// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Detection/DetectorHost.cs
//  역할: 등록된 AbstractDetector 들을 LiveTagAggregator 의 실시간 Tag 값
//        갱신에 연결한다 (DI 싱글턴). 새 감지기/응답기는 RegisterDetector/
//        RegisterResponder 로 자유롭게 추가한다.
//  MN-04: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Aggregation;
using IIoT.Monitor.Models;
using System.Collections.Specialized;
using System.ComponentModel;

namespace IIoT.Monitor.Core.Detection;

/// <summary>
/// 등록된 <see cref="AbstractDetector"/> 들을 실시간 Tag 값 갱신에 연결하는 호스트 (DI 싱글턴).
/// <para>
/// LiveTagAggregator.Rows 에 새 Tag 행이 추가되면 그 행의 PropertyChanged(EngValue 갱신)를
/// 구독하여, 값이 바뀔 때마다 등록된 모든 Detector 에게 판정을 요청한다.
/// </para>
/// </summary>
public sealed class DetectorHost
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly List<AbstractDetector>     _detectors  = new();
    private readonly List<IDetectionResponder>  _responders = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public DetectorHost(LiveTagAggregator tagAggregator)
    {
        // 이미 존재하는 행에도 구독 연결
        foreach (var row in tagAggregator.Rows)
            _AttachRow(row);

        // 이후 추가되는 행에도 자동 연결
        tagAggregator.Rows.CollectionChanged += _OnRowsChanged;
    }

    // §3 ─ 등록 API ────────────────────────────────────────

    /// <summary>커스텀 감지기를 등록한다. 순서와 상관없이 모두 동일하게 평가된다.</summary>
    public void RegisterDetector(AbstractDetector detector) => _detectors.Add(detector);

    /// <summary>트리거/해제 시 실행할 대응 동작을 등록한다.</summary>
    public void RegisterResponder(IDetectionResponder responder) => _responders.Add(responder);

    // §4 ─ 내부 배선 ───────────────────────────────────────

    private void _OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (LiveTagRow row in e.NewItems)
            _AttachRow(row);
    }

    private void _AttachRow(LiveTagRow row)
    {
        row.PropertyChanged += _OnRowPropertyChanged;
    }

    private void _OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LiveTagRow.EngValue))
            return;

        if (sender is not LiveTagRow row)
            return;

        foreach (var detector in _detectors)
            detector.Process(row, _responders);
    }
}
