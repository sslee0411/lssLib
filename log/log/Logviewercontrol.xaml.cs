using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using lssLib.Log;
using System.ComponentModel;

namespace log
{
    // ═══════════════════════════════════════════════════════════
    //  값 변환기 - LogLevel → 배경 Brush
    // ═══════════════════════════════════════════════════════════
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(45, 45, 48)),  // 어두운 회색
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(30, 30, 30)),  // 기본 배경
                    LogLevel.Warn => new SolidColorBrush(Color.FromRgb(80, 60, 20)),  // 주황 계열
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(80, 20, 20)),  // 빨강 계열
                    LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(120, 0, 0)),  // 진한 빨강
                    _ => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                };
            }
            return Brushes.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    // ═══════════════════════════════════════════════════════════
    //  값 변환기 - LogLevel → 글자 색 Brush
    // ═══════════════════════════════════════════════════════════
    public class LogLevelToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(140, 140, 140)),  // 회색
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(220, 220, 220)),  // 밝은 흰색
                    LogLevel.Warn => new SolidColorBrush(Color.FromRgb(255, 200, 60)),  // 노랑
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 100, 100)),  // 빨강
                    LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(255, 255, 255)),  // 흰색 (강조)
                    _ => new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                };
            }
            return Brushes.White;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    // ═══════════════════════════════════════════════════════════
    //  LogViewerControl  코드비하인드
    // ═══════════════════════════════════════════════════════════
    public partial class LogViewerControl : UserControl
    {
        // ─── 전체 로그 원본 (최신순 유지) ───────────────────
        private readonly ObservableCollection<LogData> _allLogs
            = new ObservableCollection<LogData>();

        // ─── WPF 필터링 View ────────────────────────────────
        // WPF의 데이터 바인딩 시스템과 UI(DataGrid, ListView 등) 사이를 연결하는 '스마트한 필터링 엔진'
        private ICollectionView _logView;

        // ─── 화면 최대 표시 건수 (LogConfig.MaxDisplayCount 연동) ─
        private int _maxDisplayCount = 1000;

        // ─── 현재 필터 상태 ─────────────────────────────────
        private LogLevel? _filterLevel = null;
        private string _filterSource = "";
        private string _filterText = "";

        // ════════════════════════════════════════════════════
        //  생성자 / Loaded / Unloaded
        // ════════════════════════════════════════════════════
        public LogViewerControl()
        {
            InitializeComponent();

            // CollectionViewSource.GetDefaultView()를 통해 생성된 뷰 객체를 담아두는 변수
            _logView = CollectionViewSource.GetDefaultView(_allLogs);
            _logView.Filter = ApplyFilter;
            LvLog.ItemsSource = _logView;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // LogConfig의 MaxDisplayCount 반영
            _maxDisplayCount = LogManager.Instance.Config?.MaxDisplayCount ?? 1000;

            LogManager.Instance.LogAdded += OnLogAdded;
            UpdateStatus();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            LogManager.Instance.LogAdded -= OnLogAdded;
        }

        // ════════════════════════════════════════════════════
        //  LogManager 이벤트 핸들러 (백그라운드 스레드 → UI 전환)
        // ════════════════════════════════════════════════════
        private void OnLogAdded(LogData data)
        {
            Dispatcher.InvokeAsync(() =>
            {
                // 최신 데이터를 맨 앞(index 0)에 삽입
                _allLogs.Insert(0, data);

                // 최대 표시 건수 초과 시 정리
                TrimDisplayList();

                UpdateStatus();

                // 자동 스크롤: 최신순(Insert 0) 이므로 맨 위로 이동
                if (ChkAutoScroll.IsChecked == true && LvLog.Items.Count > 0)
                    LvLog.ScrollIntoView(LvLog.Items[0]);
            });
        }

        // ════════════════════════════════════════════════════
        //  최대 표시 건수 동적 변경
        // ════════════════════════════════════════════════════
        /// <summary>
        /// 런타임에 최대 표시 건수를 변경한다.
        /// 줄어든 경우 오래된 항목을 즉시 제거한다.
        /// </summary>
        public void SetMaxDisplayCount(int count)
        {
            if (count < 1) return;
            _maxDisplayCount = count;

            Dispatcher.InvokeAsync(() =>
            {
                TrimDisplayList();
                UpdateStatus();
            });
        }

        // ════════════════════════════════════════════════════
        //  최대 표시 건수 초과 항목 제거
        // ════════════════════════════════════════════════════
        /// <summary>
        /// _allLogs 가 MaxDisplayCount 를 초과할 경우
        /// 가장 오래된 항목(맨 뒤)부터 일괄 제거한다.
        /// ※ 반드시 UI 스레드에서 호출할 것
        /// </summary>
        private void TrimDisplayList()
        {
            int excess = _allLogs.Count - _maxDisplayCount;
            if (excess <= 0) return;

            // RemoveAt 반복보다 한 번에 범위 제거가 성능상 유리
            for (int i = 0; i < excess; i++)
                _allLogs.RemoveAt(_allLogs.Count - 1);
        }

        // ════════════════════════════════════════════════════
        //  필터 처리
        // _logView.Filter = ApplyFilter;라고 등록해두면,
        // WPF는 리스트의 모든 항목을 하나씩 이 메서드에 던져보고,
        // rue를 반환하는 항목만 화면에 남깁
        // ════════════════════════════════════════════════════
        private bool ApplyFilter(object item)
        {   
            // 들어온 데이터가 LogData 타입이 아니면 무시하고 화면에서 치움(false).
            // 프로그램이 죽지 않게 보호하는 역할
            if (item is not LogData log) return false;

            // 사용자가 'Error'나 'Info' 같은 특정 등급을 선택했다면,
            // 그 등급과 일치하지 않는 로그는 모두 탈락
            if (_filterLevel.HasValue && log.Level != _filterLevel.Value)
                return false;

            // 사용자가 출처 텍스트를 입력했다면,
            // 로그의 출처(Source)에 그 텍스트가 포함되어 있지 않으면 탈락
            // StringComparison.OrdinalIgnoreCase : 대소문자 구분 없이 비교
            if (!string.IsNullOrEmpty(_filterSource) &&
                !log.Source.Contains(_filterSource, StringComparison.OrdinalIgnoreCase))
                return false;
            //
            // 사용자가 검색 텍스트를 입력했다면,
            // 
            if (!string.IsNullOrEmpty(_filterText) &&
                !log.Contents.Contains(_filterText, StringComparison.OrdinalIgnoreCase) &&
                !log.Source.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void RefreshFilter()
        {
            if (_logView == null) return;
            _logView.Refresh();
            UpdateStatus();
        }

        // ════════════════════════════════════════════════════
        //  툴바 이벤트
        // ════════════════════════════════════════════════════
        private void CmbLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLevel.SelectedItem is ComboBoxItem item)
            {
                _filterLevel = item.Content?.ToString() switch
                {
                    "DEBUG" => LogLevel.Debug,
                    "INFO" => LogLevel.Info,
                    "WARN" => LogLevel.Warn,
                    "ERROR" => LogLevel.Error,
                    "FATAL" => LogLevel.Fatal,
                    _ => (LogLevel?)null
                };
                RefreshFilter();
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterSource = TxtSource?.Text ?? "";
            _filterText = TxtSearch?.Text ?? "";
            RefreshFilter();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _allLogs.Clear();
            UpdateStatus();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            ExportLogs();
        }

        // ════════════════════════════════════════════════════
        //  로그 내보내기 (현재 필터 결과 기준)
        // ════════════════════════════════════════════════════
        private void ExportLogs()
        {
            var dlg = new SaveFileDialog
            {
                Title = "로그 내보내기",
                Filter = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
                FileName = $"Log_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                bool isCsv = Path.GetExtension(dlg.FileName)
                                 .Equals(".csv", StringComparison.OrdinalIgnoreCase);

                using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);

                if (isCsv)
                {
                    sw.WriteLine("날짜,레벨,출처,내용");
                    foreach (LogData log in _logView.Cast<LogData>())
                    {
                        string contents = log.Contents?.Replace("\"", "\"\"") ?? "";
                        sw.WriteLine($"\"{log.Date}\",\"{log.LevelText}\"," +
                                     $"\"{log.Source}\",\"{contents}\"");
                    }
                }
                else
                {
                    string sep = new string('─', 100);
                    sw.WriteLine($"[내보내기] {DateTime.Now:yyyy-MM-dd HH:mm:ss}  " +
                                 $"총 {_logView.Cast<LogData>().Count()}건");
                    sw.WriteLine(sep);
                    sw.WriteLine($"{"날짜",-22}  {"레벨",-6}  {"출처",-18}  내용");
                    sw.WriteLine(sep);
                    foreach (LogData log in _logView.Cast<LogData>())
                        sw.WriteLine(log.ToString());
                }

                TxtStatus.Text = $"내보내기 완료: {dlg.FileName}";
                MessageBox.Show($"내보내기 완료\n{dlg.FileName}", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 실패\n{ex.Message}", "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════
        //  상태바 업데이트
        // ════════════════════════════════════════════════════
        private void UpdateStatus()
        {
            int total = _allLogs.Count;
            int filtered = _logView.Cast<object>().Count();

            TxtStatus.Text = LogManager.Instance.IsRunning ? "실행 중" : "정지";
            TxtCount.Text = total == filtered
                ? $"최신 {total:N0} / 최대 {_maxDisplayCount:N0}건"
                : $"필터: {filtered:N0} / 표시: {total:N0} / 최대: {_maxDisplayCount:N0}건";
        }
    }
}