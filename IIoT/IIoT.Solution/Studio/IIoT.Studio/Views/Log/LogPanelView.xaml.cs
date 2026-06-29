// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Views/Log/LogPanelView.xaml.cs
//  역할: 하단 로그 패널 코드비하인드
//        LogManager.Instance.LogAdded 이벤트 구독 → ListView 표시
//  Studio-P04: 신규
//  Studio-P04 fix: CS0246 using System.Windows.Data 추가
//                  CS1503 data.Time은 string → ToString() 불필요
//                  CS1061 data.Message → data.Contents
//  생성: 2026-06-27
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;          // ★ CS0246 수정: ICollectionView

namespace IIoT.Studio.Views.Log;

/// <summary>로그 한 줄을 ListView에 바인딩하기 위한 뷰 모델</summary>
internal sealed class LogRow
{
    public string TimeText { get; init; } = string.Empty;
    public string LevelText { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public partial class LogPanelView : UserControl
{
    // §1 ─ 필드 ──────────────────────────────────────────────

    private const int MaxRows = 2000;

    private readonly ObservableCollection<LogRow> _allLogs = new();
    private readonly ICollectionView _logView;

    private string _filterLevel = "ALL";
    private string _filterSource = string.Empty;

    // §2 ─ 생성자 ─────────────────────────────────────────────

    public LogPanelView()
    {
        // ① ICollectionView 먼저 초기화 (InitializeComponent 전 — null 방지)
        _logView = CollectionViewSource.GetDefaultView(_allLogs);
        _logView.Filter = _ApplyFilter;

        InitializeComponent();

        // ② ListView 에 뷰 연결
        LogList.ItemsSource = _logView;

        // ③ LogManager 이벤트 구독
        LogManager.Instance.LogAdded += _OnLogAdded;

        // ④ Unloaded 시 구독 해제 (메모리 누수 방지)
        Unloaded += (_, _) =>
            LogManager.Instance.LogAdded -= _OnLogAdded;
    }

    // §3 ─ 로그 수신 ──────────────────────────────────────────

    private void _OnLogAdded(LogData data)
    {
        // LogAdded 는 백그라운드 스레드에서 호출 → UI 스레드로 전환
        Dispatcher.InvokeAsync(() =>
        {
            var row = new LogRow
            {
                // ★ CS1503 수정: data.Time 은 이미 "HH:mm:ss.fff" 형식 string
                TimeText = data.Time,
                LevelText = data.LevelText,
                Source = data.Source ?? string.Empty,
                // ★ CS1061 수정: Message 프로퍼티 없음 → Contents 사용
                Message = data.Contents ?? string.Empty
            };

            // 최신이 맨 위 (Insert 0)
            _allLogs.Insert(0, row);

            // 최대 건수 초과 시 가장 오래된 항목 제거
            if (_allLogs.Count > MaxRows)
                _allLogs.RemoveAt(_allLogs.Count - 1);
        });
    }

    // §4 ─ 필터 ───────────────────────────────────────────────

    private bool _ApplyFilter(object item)
    {
        if (item is not LogRow row) return false;

        // 레벨 필터
        if (_filterLevel != "ALL" &&
            !row.LevelText.Equals(_filterLevel, StringComparison.OrdinalIgnoreCase))
            return false;

        // Source 필터
        if (!string.IsNullOrWhiteSpace(_filterSource) &&
            !row.Source.Contains(_filterSource, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    // §5 ─ 이벤트 핸들러 ──────────────────────────────────────

    private void OnFilterChanged(object sender, EventArgs e)
    {
        if (LevelFilter is null || SourceFilter is null) return;
        _filterLevel = (LevelFilter.SelectedItem as ComboBoxItem)
                        ?.Content?.ToString() ?? "ALL";
        _filterSource = SourceFilter.Text ?? string.Empty;
        _logView.Refresh();
    }

    private void OnClear(object sender, RoutedEventArgs e)
        => _allLogs.Clear();
}