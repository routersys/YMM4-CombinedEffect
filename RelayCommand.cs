using System.Windows.Input;

namespace CombinedEffect.Helpers
{
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter is T t)
            {
                return _canExecute == null || _canExecute(t);
            }
            return _canExecute == null || _canExecute(default(T));
        }

        public void Execute(object? parameter)
        {
            if (parameter is T t)
            {
                _execute(t);
            }
            else
            {
                _execute(default(T));
            }
        }
    }
}