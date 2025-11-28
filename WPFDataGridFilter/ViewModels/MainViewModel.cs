using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WPFDataGridFilter.Helpers;
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
            get => ifNumHeaderText;
            set
            {
                if (ifNumHeaderText != value)
                {
                    ifNumHeaderText = value;
                    OnPropertyChanged(nameof(IFNumHeaderText));
                }
            }
        }
        private string ifNumHeaderText = "IFNum";

        public MainViewModel()
        {
            // 大量データ生成（10000件）でプロトタイプテスト
            const int itemCount = 10000;
            var stringPool = StringPool.Shared;

            for (int i = 0; i < itemCount; i++)
            {
                var entry = new LogEntry
                {
                    Time = DateTime.Now.AddMinutes(-i).ToString("yyyy/MM/dd HH:mm:ss.fff"),
                    // StringPool でインターン化（重複文字列のメモリ削減）
                    IFNum = stringPool.Intern(((i % 3) + 1).ToString()),
                    Source = stringPool.Intern("SRC" + (i % 5)),
                    Destination = stringPool.Intern("DST" + (i % 7)),
                    Event = stringPool.Intern(i % 2 == 0 ? "SEND" : "RECV")
                };

                entry.Data = i % 4 == 0
                    ? BuildPacket(i)
                    : $"Payload {i}";

                Items.Add(entry);
            }
        }

        private static byte[] BuildPacket(int seed)
        {
            var random = new Random(seed);
            var buffer = new byte[16];
            random.NextBytes(buffer);
            return buffer;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
