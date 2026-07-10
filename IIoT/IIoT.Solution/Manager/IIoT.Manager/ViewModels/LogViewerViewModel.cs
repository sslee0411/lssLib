// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/LogViewerViewModel.cs
//  역할: [로그] 탭 ViewModel — LogTailService 라인 수신 + 필터/검색/일시정지
//  MG-04: 신규
//  개선(2026-07-09, 사용자 요청): 표준 LogPanelView UI 정렬 —
//        레벨 필터(ALL/DEBUG/INFO/WARN/ERROR/FATAL) 추가,
//        LogRow 확장(시각/레벨/Source 분리)에 맞춰 필터 로직 갱신
//  생성: 2026-07-09 / 수정: 2026-07-09
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace IIoT.Manager.ViewModels;

/// <summary>
/// 통합 로그 뷰어 ViewModel.
/// <para>
/// LogTailService.LineReceived(UI 스레드)를 구독해 Rows 에 누적한다.
/// 최대 2000행 유지(초과 시 앞에서 제거 — Monitor MaxDisplayCount 와 동일 기준).
/// 프로그램/레벨 필터 + 자유 검색은 ICollectionView.Filter 로 처리한다.
/// </para>
/// </summary>
public partial class LogViewerViewModel : ObservableObject
{
    // §1 ─ 상수/필드 ─────────────────────────────────────────

    private const int _maxRows = 2000;

    /// <summary>프로그램 필터의 "전체" 항목</summary>
    public const string AllPrograms = "전체";

    // §2 ─ 컬렉션 ─────────────────────────────────────────────

    /// <summary>수신 로그 (원본 — 화면은 RowsView 를 바인딩)</summary>
    public ObservableCollection<LogRow> Rows { get; } = [];

    /// <summary>필터 적용 뷰</summary>
    public ICollectionView RowsView { get; }

    /// <summary>프로그램 필터 목록 ("전체" + 프로그램 이름 자동 추가)</summary>
    public ObservableCollection<string> Programs { get; } = [AllPrograms];

    /// <summary>레벨 필터 목록 (표준 LogPanelView 와 동일 구성)</summary>
    public ObservableCollection<string> Levels { get; } =
        ["ALL", "DEBUG", "INFO", "WARN", "ERROR", "FATAL"];

    // §3 ─ 관찰 속성 ─────────────────────────────────────────

    /// <summary>선택된 프로그램 필터</summary>
    [ObservableProperty]
    private string _selectedProgram = AllPrograms;

    /// <summary>선택된 레벨 필터 (ALL = 전체)</summary>
    [ObservableProperty]
    private string _selectedLevel = "ALL";

    /// <summary>자유 검색어 (Source·내용 부분일치)</summary>
    [ObservableProperty]
    private string _searchText = "";

    /// <summary>일시정지 — true 면 신규 라인 무시 (화면 고정 검토용)</summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>하단 카운트 문구</summary>
    [ObservableProperty]
    private string _countText = "0행";

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public LogViewerViewModel(LogTailService logTail)
    {
        RowsView        = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = _FilterRow;

        // ★ LogTailService 는 UI 스레드(DispatcherTimer)에서 발행 — 마샬링 불필요
        logTail.LineReceived += _OnLine;
    }

    // §5 ─ 커맨드 ─────────────────────────────────────────────

    /// <summary>표시 중인 로그 전체 삭제</summary>
    [RelayCommand]
    private void Clear()
    {
        Rows.Clear();
        CountText = "0행";
    }

    // §6 ─ 내부 메서드 ────────────────────────────────────────

    private void _OnLine(LogRow row)
    {
        if (IsPaused) return;

        // 프로그램 필터 목록에 신규 프로그램 자동 추가
        if (!Programs.Contains(row.Program))
            Programs.Add(row.Program);

        Rows.Add(row);

        // 최대 행 초과 시 앞에서 제거
        while (Rows.Count > _maxRows)
            Rows.RemoveAt(0);

        CountText = $"{Rows.Count}행";
    }

    private bool _FilterRow(object obj)
    {
        if (obj is not LogRow row) return false;

        if (SelectedProgram != AllPrograms && row.Program != SelectedProgram)
            return false;

        if (SelectedLevel != "ALL" && row.LevelText != SelectedLevel)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !row.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
            !row.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    // ★ 필터 조건 변경 시 뷰 갱신 (ObservableProperty partial 메서드 활용)
    partial void OnSelectedProgramChanged(string value) => RowsView.Refresh();
    partial void OnSelectedLevelChanged(string value)   => RowsView.Refresh();
    partial void OnSearchTextChanged(string value)      => RowsView.Refresh();
}
