using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using lssLib.Log;

namespace logMVVM
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
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    LogLevel.Warn => new SolidColorBrush(Color.FromRgb(80, 60, 20)),
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(80, 20, 20)),
                    LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(120, 0, 0)),
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
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    LogLevel.Warn => new SolidColorBrush(Color.FromRgb(255, 200, 60)),
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                    LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    _ => new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                };
            }
            return Brushes.White;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    // ═══════════════════════════════════════════════════════════
    //  LogViewerControl - View 코드비하인드
    //  ViewModel 담당: 필터·컬렉션·커맨드·상태 (LogViewerViewModel)
    //  View 담당     : DataContext 설정, ScrollIntoView (ListView 직접 참조 필요)
    // ═══════════════════════════════════════════════════════════
    public partial class LogViewerControl : UserControl
    {
        #region Fields
        private LogViewerViewModel _vm;
        #endregion

        #region Constructor
        public LogViewerControl()
        {
            InitializeComponent();
        }
        #endregion

        #region Loaded / Unloaded
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // ViewModel 생성 및 DataContext 설정
            _vm = new LogViewerViewModel();
            DataContext = _vm;

            // ListView ItemsSource → ViewModel의 LogView 바인딩
            LvLog.ItemsSource = _vm.LogView;

            // ScrollIntoView 는 ListView 직접 참조가 필요 → View에서 이벤트 수신
            _vm.RequestScrollToTop += OnRequestScrollToTop;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            // 이벤트 해제 후 ViewModel Dispose (LogAdded 구독 해제)
            _vm.RequestScrollToTop -= OnRequestScrollToTop;
            _vm.Dispose();
        }
        #endregion

        #region Scroll (View 전용 - ListView 직접 참조 필요)
        /// <summary>
        /// ViewModel의 RequestScrollToTop 이벤트 수신 →
        /// 최신순 정렬(Insert 0) 이므로 첫 번째 항목으로 스크롤
        /// </summary>
        private void OnRequestScrollToTop()
        {
            if (LvLog.Items.Count > 0)
                LvLog.ScrollIntoView(LvLog.Items[0]);
        }
        #endregion
    }
}