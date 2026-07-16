// ══════════════════════════════════════════════════════════
//  IIoT.Manager · ViewModels/LogViewerViewModel.cs
//  역할: [로그] 탭 ViewModel — LogTailService 라인 수신 + 필터/검색/일시정지
//  MG-04: 신규
//  개선(2026-07-09, 사용자 요청): 표준 LogPanelView UI 정렬 —
//        레벨 필터(ALL/DEBUG/INFO/WARN/ERROR/FATAL) 추가,
//        LogRow 확장(시각/레벨/Source 분리)에 맞춰 필터 로직 갱신
//  MG-EX-07: ① 과거 일자 조회 — DatePicker 선택 날짜의 All*.txt 전체 로드
//               (실시간↔과거 모드 전환, 최대 5000행 — 초과 시 뒷부분 유지)
//            ② CSV 내보내기 — 현재 필터 적용된 목록을 UTF-8(BOM) CSV 저장
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-07)
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IIoT.Manager.Core;
using IIoT.Manager.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;   // ★ 규칙: ImplicitUsings 미의존 (CS0246 재발 방지)
using System.Text;
using System.Windows.Data;

namespace IIoT.Manager.ViewModels;

/// <summary>
/// 통합 로그 뷰어 ViewModel.
/// <para>
/// [실시간 모드] LogTailService.LineReceived(UI 스레드)를 구독해 Rows 에 누적 (최대 2000행).
/// [과거 모드] 선택 날짜의 All*.txt 를 로드해 표시 (최대 5000행 — 뒷부분 유지).
/// 프로그램/레벨 필터 + 자유 검색은 ICollectionView.Filter 로 처리한다.
/// </para>
/// </summary>
public partial class LogViewerViewModel : ObservableObject
{
    // §1 ─ 상수/필드 ─────────────────────────────────────────

    private const int _maxLiveRows    = 2000;
    private const int _maxHistoryRows = 5000;

    /// <summary>프로그램 필터의 "전체" 항목</summary>
    public const string AllPrograms = "전체";

    private readonly LogTailService _logTail;

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

    /// <summary>일시정지 — true 면 신규 라인 무시 (실시간 모드 전용)</summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>하단 카운트 문구</summary>
    [ObservableProperty]
    private string _countText = "0행";

    /// <summary>★ MG-EX-07: 실시간 모드 여부 (false = 과거 조회 모드)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    private bool _isLiveMode = true;

    /// <summary>★ MG-EX-07: 과거 조회 대상 날짜</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    private DateTime? _selectedDate = DateTime.Today;

    /// <summary>모드 표시 문구</summary>
    public string ModeText => IsLiveMode
        ? "● 실시간"
        : $"과거: {SelectedDate:yyyy-MM-dd}";

    // §4 ─ 생성자 ─────────────────────────────────────────────

    public LogViewerViewModel(LogTailService logTail)
    {
        _logTail = logTail;

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

    /// <summary>★ MG-EX-07: 과거 일자 로그 로드 (실시간 → 과거 모드 전환)</summary>
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (SelectedDate is not DateTime date)
        {
            CountText = "날짜를 선택해 주세요.";
            return;
        }

        IsLiveMode = false;
        Rows.Clear();

        try
        {
            // 파일 IO 는 백그라운드 — UI 비블로킹
            var loaded = await Task.Run(() => _ReadHistory(date));

            foreach (var row in loaded)
            {
                if (!Programs.Contains(row.Program))
                    Programs.Add(row.Program);
                Rows.Add(row);
            }

            CountText = loaded.Count == 0
                ? $"{date:yyyy-MM-dd} 로그 없음"
                : $"{Rows.Count}행 (과거 조회{(loaded.Count >= _maxHistoryRows ? " — 최근 부분만" : "")})";
        }
        catch (Exception ex)
        {
            // ★ 규칙: 조용히 삼키지 않는다 — 로그 + 화면 노출
            lssLib.Log.LogManager.Instance.Error("LogViewer", $"과거 로그 로드 실패: {ex.Message}");
            CountText = $"로드 실패: {ex.Message}";
        }
    }

    /// <summary>★ MG-EX-07: 실시간 모드 복귀</summary>
    [RelayCommand]
    private void BackToLive()
    {
        IsLiveMode = true;
        Rows.Clear();
        CountText = "0행";
    }

    /// <summary>★ MG-EX-07: 현재 필터 적용된 목록을 CSV 로 내보내기 (UTF-8 BOM — Excel 한글 호환)</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title    = "로그 CSV 내보내기",
                Filter   = "CSV 파일 (*.csv)|*.csv",
                FileName = $"ManagerLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("시각,레벨,프로그램,Source,내용");

            foreach (var obj in RowsView)   // 필터 적용된 뷰 기준
            {
                if (obj is not LogRow r) continue;
                sb.AppendLine(string.Join(",",
                    _Csv(r.TimeText), _Csv(r.LevelText), _Csv(r.Program),
                    _Csv(r.Source), _Csv(r.Message)));
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            CountText = $"CSV 저장 완료: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            lssLib.Log.LogManager.Instance.Error("LogViewer", $"CSV 내보내기 실패: {ex.Message}");
            CountText = $"CSV 실패: {ex.Message}";
        }
    }

    // §6 ─ 내부 메서드 ────────────────────────────────────────

    private void _OnLine(LogRow row)
    {
        // ★ MG-EX-07: 과거 조회 모드에서는 실시간 수신 중단 (복귀 시 재개)
        if (!IsLiveMode || IsPaused) return;

        // 프로그램 필터 목록에 신규 프로그램 자동 추가
        if (!Programs.Contains(row.Program))
            Programs.Add(row.Program);

        Rows.Add(row);

        // 최대 행 초과 시 앞에서 제거
        while (Rows.Count > _maxLiveRows)
            Rows.RemoveAt(0);

        CountText = $"{Rows.Count}행";
    }

    /// <summary>
    /// ★ MG-EX-07: 지정 날짜의 모든 대상 All*.txt 를 읽어 시각순 병합한다.
    /// 롤링 파일은 All.txt → All_2.txt … 순(기록 순서)으로 읽는다.
    /// </summary>
    private List<LogRow> _ReadHistory(DateTime date)
    {
        var result = new List<LogRow>();
        var subDir = Path.Combine(date.ToString("yyyy_MM"), date.ToString("dd"));

        foreach (var (source, logRoot) in _logTail.Targets)
        {
            var dayDir = Path.Combine(logRoot, subDir);
            if (!Directory.Exists(dayDir)) continue;

            // All.txt(=1) → All_2 → All_3 … 기록 순서대로
            var files = Directory.EnumerateFiles(dayDir, "All*.txt")
                .OrderBy(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);   // All / All_2 …
                    var us   = name.IndexOf('_');
                    return us < 0 ? 1 : int.TryParse(name[(us + 1)..], out var n) ? n : 1;
                });

            foreach (var file in files)
                foreach (var line in File.ReadLines(file))
                    if (line.Length > 0)
                        result.Add(LogRow.Parse(source, line));
        }

        // 프로그램 간 시각순 병합 (같은 날짜 — HH:mm:ss.fff 문자열 정렬로 충분)
        result.Sort((a, b) => string.CompareOrdinal(a.TimeText, b.TimeText));

        // 상한 초과 시 뒷부분(최근) 유지
        if (result.Count > _maxHistoryRows)
            result.RemoveRange(0, result.Count - _maxHistoryRows);

        return result;
    }

    private static string _Csv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

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
