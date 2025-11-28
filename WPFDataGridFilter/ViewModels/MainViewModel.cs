using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WPFDataGridFilter.Helpers;
using WPFDataGridFilter.Models;

namespace WPFDataGridFilter.ViewModels
{
    /// <summary>
    /// サンプル データを提供するメイン ViewModel。
    /// バッチ処理によるリアルタイムログ追加をサポートします。
    /// フィルタ処理は <see cref="Controls.FilterableDataGrid"/> 側で実装される。
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        #region フィールド
        private readonly LogBuffer<LogEntry> logBuffer;
        private readonly LogBufferProcessor bufferProcessor;
        private readonly StringPool stringPool;
        private CancellationTokenSource? logGeneratorCts;
        private bool isGeneratingLogs;
        private string ifNumHeaderText = "IFNum";
        private bool disposed;
        #endregion

        #region プロパティ
        /// <summary>表示するログ項目のコレクション（バッチ通知対応）</summary>
        public BatchingObservableCollection<LogEntry> Items { get; } = new();

        /// <summary>IFNum 列のヘッダー表示文字列</summary>
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

        /// <summary>ログ生成中か</summary>
        public bool IsGeneratingLogs
        {
            get => isGeneratingLogs;
            private set
            {
                if (isGeneratingLogs != value)
                {
                    isGeneratingLogs = value;
                    OnPropertyChanged(nameof(IsGeneratingLogs));
                }
            }
        }

        /// <summary>バッファ内の未処理件数</summary>
        public int BufferQueueLength => logBuffer.Count;

        /// <summary>処理済みバッチ数</summary>
        public int ProcessedBatchCount => bufferProcessor.ProcessedBatchCount;

        /// <summary>処理済み総アイテム数</summary>
        public int TotalProcessedItems => bufferProcessor.TotalProcessedItems;

        /// <summary>平均処理時間（ミリ秒）</summary>
        public double AverageProcessingTimeMs => bufferProcessor.AverageProcessingTimeMs;
        #endregion

        #region コマンド
        /// <summary>ログ生成開始/停止コマンド</summary>
        public ICommand ToggleLogGenerationCommand { get; }

        /// <summary>初期データ読み込みコマンド</summary>
        public ICommand LoadInitialDataCommand { get; }

        /// <summary>ログクリアコマンド</summary>
        public ICommand ClearLogsCommand { get; }
        #endregion

        #region コンストラクタ
        public MainViewModel()
        {
            stringPool = StringPool.Shared;
            logBuffer = new LogBuffer<LogEntry>(maxBatchSize: 50);
            bufferProcessor = new LogBufferProcessor(
                logBuffer,
                Items,
                processIntervalMs: 50,
                maxItemsInCollection: 50000);

            // コマンド初期化
            ToggleLogGenerationCommand = new RelayCommand(_ => ToggleLogGeneration());
            LoadInitialDataCommand = new RelayCommand(_ => LoadInitialData());
            ClearLogsCommand = new RelayCommand(_ => ClearLogs());

            // バッファ処理を開始
            bufferProcessor.Start();

            // 初期データを読み込み
            LoadInitialData();
        }
        #endregion

        #region メソッド
        /// <summary>
        /// 初期データを読み込み（10000件）
        /// </summary>
        private void LoadInitialData()
        {
            const int itemCount = 10000;

            for (int i = 0; i < itemCount; i++)
            {
                var entry = new LogEntry
                {
                    Time = DateTime.Now.AddMinutes(-i).ToString("yyyy/MM/dd HH:mm:ss.fff"),
                    IFNum = stringPool.Intern(((i % 3) + 1).ToString()),
                    Source = stringPool.Intern("SRC" + (i % 5)),
                    Destination = stringPool.Intern("DST" + (i % 7)),
                    Event = stringPool.Intern(i % 2 == 0 ? "SEND" : "RECV"),
                    Data = i % 4 == 0 ? BuildPacket(i) : $"Payload {i}"
                };

                // バッファ経由で追加（バッチ処理される）
                logBuffer.Enqueue(entry);
            }
        }

        /// <summary>
        /// ログ生成の開始/停止を切り替え
        /// </summary>
        private void ToggleLogGeneration()
        {
            if (IsGeneratingLogs)
            {
                StopLogGeneration();
            }
            else
            {
                StartLogGeneration();
            }
        }

        /// <summary>
        /// リアルタイムログ生成を開始（100件/秒）
        /// </summary>
        private void StartLogGeneration()
        {
            if (IsGeneratingLogs) return;

            logGeneratorCts = new CancellationTokenSource();
            IsGeneratingLogs = true;

            Task.Run(async () =>
            {
                var random = new Random();
                int logIndex = 0;

                while (!logGeneratorCts.Token.IsCancellationRequested)
                {
                    // 10件を10ms間隔で追加 = 100件/秒
                    for (int i = 0; i < 10 && !logGeneratorCts.Token.IsCancellationRequested; i++)
                    {
                        var entry = new LogEntry
                        {
                            Time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                            IFNum = ((logIndex % 3) + 1).ToString(),
                            Source = "SRC" + (logIndex % 5),
                            Destination = "DST" + (logIndex % 7),
                            Event = logIndex % 2 == 0 ? "SEND" : "RECV",
                            Data = $"Realtime Log #{logIndex}"
                        };

                        logBuffer.Enqueue(entry);
                        logIndex++;
                    }

                    try
                    {
                        await Task.Delay(100, logGeneratorCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }, logGeneratorCts.Token);
        }

        /// <summary>
        /// リアルタイムログ生成を停止
        /// </summary>
        private void StopLogGeneration()
        {
            logGeneratorCts?.Cancel();
            logGeneratorCts?.Dispose();
            logGeneratorCts = null;
            IsGeneratingLogs = false;
        }

        /// <summary>
        /// ログをクリア
        /// </summary>
        private void ClearLogs()
        {
            logBuffer.Clear();
            Items.Clear();
            bufferProcessor.ResetMetrics();
        }

        private static byte[] BuildPacket(int seed)
        {
            var random = new Random(seed);
            var buffer = new byte[16];
            random.NextBytes(buffer);
            return buffer;
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                StopLogGeneration();
                bufferProcessor.Dispose();
                disposed = true;
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
