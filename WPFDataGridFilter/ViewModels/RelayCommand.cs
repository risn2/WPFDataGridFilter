using System;
using System.Windows.Input;

namespace WPFDataGridFilter.ViewModels
{
    /// <summary>
    /// シンプルな ICommand 実装。アクションと条件でコマンドを構成します。
    /// </summary>
    public class RelayCommand : ICommand
    {
        #region フィールド
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
    #endregion フィールド

        #region コンストラクタ
        /// <summary>
        /// コマンドを初期化します。
        /// </summary>
        /// <param name="execute">実行デリゲート。</param>
        /// <param name="canExecute">実行可否デリゲート。null の場合は常に実行可。</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
    #endregion コンストラクタ

        #region メソッド
        /// <summary>
        /// コマンドが現在実行可能かどうかを返します。
        /// </summary>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <summary>
        /// コマンドの実行を行います。
        /// </summary>
        public void Execute(object? parameter) => _execute(parameter);
    #endregion メソッド

        #region イベント
        /// <summary>
        /// 実行可否状態が変化した際に通知されます。
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    #endregion イベント
    }
}
