using System;
using System.Windows.Input;

namespace logMVVM
{
    /// <summary>
    /// MVVM ICommand 구현체
    /// ViewModel에서 버튼 클릭 등 UI 액션을 Command로 바인딩할 때 사용
    /// </summary>
    public class RelayCommand : ICommand
    {
        #region Fields
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        #endregion

        #region Constructor
        /// <param name="execute">실행할 동작</param>
        /// <param name="canExecute">실행 가능 여부 판단 함수 (null = 항상 가능)</param>
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? null : _ => canExecute())
        { }
        #endregion

        #region ICommand
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
            => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter)
            => _execute(parameter);
        #endregion

        /// <summary>CanExecute 재평가 강제 요청</summary>
        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}