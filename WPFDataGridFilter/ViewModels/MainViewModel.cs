using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WPFDataGridFilter.Models;

namespace WPFDataGridFilter.ViewModels
{
    /// <summary>
    /// サンプル データを提供するだけのメイン ViewModel。
    /// フィルタ処理は <see cref="Controls.FilterableDataGrid"/> 側で実装される。
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        /// <summary>表示するログ項目のコレクション。</summary>
        public ObservableCollection<LogEntry> Items { get; } = new();

        /// <summary>IFNum 列のヘッダー表示文字列。</summary>
        public string IFNumHeaderText
        {
            get => _ifNumHeaderText;
            set
            {
                if (_ifNumHeaderText != value)
                {
                    _ifNumHeaderText = value;
                    OnPropertyChanged(nameof(IFNumHeaderText));
                }
            }
        }
        private string _ifNumHeaderText = "IFNum";

        public MainViewModel()
        {
            for (int i = 0; i < 200; i++)
            {
                Items.Add(new LogEntry
                {
                    Time = DateTime.Now.AddMinutes(-i).ToString("yyyy/MM/dd HH:mm:ss.fff"),
                    IFNum = ((i % 3) + 1).ToString(),
                    Source = "SRC" + (i % 5),
                    Destination = "DST" + (i % 7),
                    Event = i % 2 == 0 ? "SEND" : "RECV",
                    Data = $"Payload {i}"
                });
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
