using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using lssLib.Log;

namespace logMVVM
{
    /// <summary>
    /// LogViewerControl 의 ViewModel
    /// ─────────────────────────────────────────────
    ///  • INotifyPropertyChanged 구현
    ///  • 로그 컬렉션 / 필터 / 최대 표시 건수 관리
    ///  • ClearCommand / ExportCommand 제공
    ///  • LogAdded 이벤트 구독 → UI 스레드 디스패치
    ///  • RequestScrollToTop 이벤트 → View 의 ScrollIntoView 트리거
    /// </summary>
    public class LogViewerViewModel : INotifyPropertyChanged
    {
        // ════════════════════════════════════════════════════
        //  INotifyPropertyChanged
        // ════════════════════════════════════════════════════
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  이벤트 (View → ScrollIntoView 트리거)
        // ════════════════════════════════════════════════════
        #region Events
        /// <summary>
        /// 자동스크롤 활성화 상태에서 새 로그가 추가되면 발생.
        /// View 코드비하인드에서 구독하여 ListView.ScrollIntoView 호출.
        /// </summary>
        public event Action RequestScrollToTop;
        #endregion

        // ════════════════════════════════════════════════════
        //  필드
        // ════════════════════════════════════════════════════
        #region Fields
        private readonly ObservableCollection<LogData> _allLogs
            = new ObservableCollection<LogData>();

        private readonly ICollectionView _logView;

        private int _maxDisplayCount;
        private LogLevel? _filterLevel = null;
        private string _filterSource = "";
        private string _filterText = "";
        private bool _autoScroll = true;
        private string _statusText = "대기 중";
        private string _countText = "";
        #endregion

        // ════════════════════════════════════════════════════
        //  Properties
        // ════════════════════════════════════════════════════
        #region Properties
        /// <summary>ListView ItemsSource 바인딩 대상</summary>
        public ICollectionView LogView => _logView;

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetField(ref _autoScroll, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value);
        }

        public string CountText
        {
            get => _countText;
            private set => SetField(ref _countText, value);
        }

        // ─── 필터 프로퍼티 (변경 시 자동 필터 갱신) ─────────
        public string FilterLevelText
        {
            get => _filterLevel?.ToString().ToUpper() ?? "ALL";
            set
            {
                _filterLevel = value switch
                {
                    "DEBUG" => LogLevel.Debug,
                    "INFO" => LogLevel.Info,
                    "WARN" => LogLevel.Warn,
                    "ERROR" => LogLevel.Error,
                    "FATAL" => LogLevel.Fatal,
                    _ => (LogLevel?)null
                };
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        public string FilterSource
        {
            get => _filterSource;
            set { if (SetField(ref _filterSource, value ?? "")) RefreshFilter(); }
        }

        public string FilterText
        {
            get => _filterText;
            set { if (SetField(ref _filterText, value ?? "")) RefreshFilter(); }
        }

        /// <summary>런타임 최대 표시 건수 변경</summary>
        public int MaxDisplayCount
        {
            get => _maxDisplayCount;
            set
            {
                if (value < 1 || !SetField(ref _maxDisplayCount, value)) return;
                TrimDisplayList();
                UpdateStatus();
            }
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  Commands
        // ════════════════════════════════════════════════════
        #region Commands
        public ICommand ClearCommand { get; }
        public ICommand ExportCommand { get; }
        #endregion

        // ════════════════════════════════════════════════════
        //  Constructor
        // ════════════════════════════════════════════════════
        public LogViewerViewModel()
        {
            // LogConfig의 MaxDisplayCount 반영
            _maxDisplayCount = LogManager.Instance.Config?.MaxDisplayCount ?? 1000;

            // CollectionView 초기화
            _logView = CollectionViewSource.GetDefaultView(_allLogs);
            _logView.Filter = ApplyFilter;

            // Commands
            ClearCommand = new RelayCommand(ExecuteClear);
            ExportCommand = new RelayCommand(ExecuteExport);

            // LogManager 이벤트 구독
            LogManager.Instance.LogAdded += OnLogAdded;

            UpdateStatus();
        }

        // ════════════════════════════════════════════════════
        //  Dispose (View Unloaded 시 호출)
        // ════════════════════════════════════════════════════
        /// <summary>View Unloaded 시 호출하여 이벤트 구독 해제</summary>
        public void Dispose()
        {
            LogManager.Instance.LogAdded -= OnLogAdded;
        }

        // ════════════════════════════════════════════════════
        //  LogAdded 핸들러 (백그라운드 → UI 스레드 전환)
        // ════════════════════════════════════════════════════
        #region LogAdded
        private void OnLogAdded(LogData data)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _allLogs.Insert(0, data);
                TrimDisplayList();
                UpdateStatus();

                if (AutoScroll)
                    RequestScrollToTop?.Invoke();
            });
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  필터
        // ════════════════════════════════════════════════════
        #region Filter
        private bool ApplyFilter(object item)
        {
            if (item is not LogData log) return false;

            if (_filterLevel.HasValue && log.Level != _filterLevel.Value)
                return false;

            if (!string.IsNullOrEmpty(_filterSource) &&
                !log.Source.Contains(_filterSource, StringComparison.OrdinalIgnoreCase))
                return false;

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
        #endregion

        // ════════════════════════════════════════════════════
        //  최대 표시 건수 초과 제거
        // ════════════════════════════════════════════════════
        #region TrimDisplayList
        /// <summary>
        /// 최대 표시 건수 초과 시 오래된 항목(맨 뒤)부터 제거.
        /// ※ 반드시 UI 스레드에서 호출.
        /// </summary>
        private void TrimDisplayList()
        {
            int excess = _allLogs.Count - _maxDisplayCount;
            if (excess <= 0) return;

            for (int i = 0; i < excess; i++)
                _allLogs.RemoveAt(_allLogs.Count - 1);
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  Command 실행
        // ════════════════════════════════════════════════════
        #region Command Execute
        private void ExecuteClear(object _)
        {
            _allLogs.Clear();
            UpdateStatus();
        }

        private void ExecuteExport(object _)
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

                StatusText = $"내보내기 완료: {dlg.FileName}";
                System.Windows.MessageBox.Show($"내보내기 완료\n{dlg.FileName}", "알림",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"내보내기 실패\n{ex.Message}", "오류",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        #endregion

        // ════════════════════════════════════════════════════
        //  상태바
        // ════════════════════════════════════════════════════
        #region Status
        private void UpdateStatus()
        {
            int total = _allLogs.Count;
            int filtered = _logView?.Cast<object>().Count() ?? 0;

            StatusText = LogManager.Instance.IsRunning ? "실행 중" : "정지";
            CountText = total == filtered
                ? $"최신 {total:N0} / 최대 {_maxDisplayCount:N0}건"
                : $"필터: {filtered:N0} / 표시: {total:N0} / 최대: {_maxDisplayCount:N0}건";
        }
        #endregion
    }
}