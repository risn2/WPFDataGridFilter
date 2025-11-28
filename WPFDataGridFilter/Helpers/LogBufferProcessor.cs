using System;
using System.Diagnostics;
using System.Windows.Threading;
using WPFDataGridFilter.Models;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// バッファからログを定期的に取り出してUIに反映するサービス。
    /// バックグラウンドスレッドからのログ追加をUIスレッドで処理します。
    /// </summary>
    public class LogBufferProcessor : IDisposable
    {
        #region フィールド
        /// <summary>ログバッファ</summary>
        private readonly LogBuffer<LogEntry> buffer;

        /// <summary>出力先コレクション</summary>
        private readonly BatchingObservableCollection<LogEntry> collection;

        /// <summary>処理タイマー</summary>
        private readonly DispatcherTimer processTimer;

        /// <summary>文字列プール</summary>
        private readonly StringPool stringPool;

        /// <summary>処理間隔（ミリ秒）</summary>
        private readonly int processIntervalMs;

        /// <summary>コレクション内の最大件数</summary>
        private readonly int maxItemsInCollection;

        /// <summary>破棄済みフラグ</summary>
        private bool disposed;
        #endregion

        #region プロパティ
        /// <summary>処理済みバッチ数</summary>
        public int ProcessedBatchCount { get; private set; }

        /// <summary>処理済み総アイテム数</summary>
        public int TotalProcessedItems { get; private set; }

        /// <summary>平均処理時間（ミリ秒）</summary>
        public double AverageProcessingTimeMs { get; private set; }

        /// <summary>最新バッチの処理時間（ミリ秒）</summary>
        public double LastProcessingTimeMs { get; private set; }

        /// <summary>処理中か</summary>
        public bool IsRunning => processTimer.IsEnabled;

        /// <summary>バッファの参照</summary>
        public LogBuffer<LogEntry> Buffer => buffer;
        #endregion

        #region コンストラクタ
        /// <summary>
        /// LogBufferProcessor を初期化します。
        /// </summary>
        /// <param name="buffer">ログバッファ</param>
        /// <param name="collection">出力先コレクション</param>
        /// <param name="processIntervalMs">処理間隔（ミリ秒、デフォルト: 50）</param>
        /// <param name="maxItemsInCollection">コレクション内の最大件数（デフォルト: 50000）</param>
        public LogBufferProcessor(
            LogBuffer<LogEntry> buffer,
            BatchingObservableCollection<LogEntry> collection,
            int processIntervalMs = 50,
            int maxItemsInCollection = 50000)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
            this.processIntervalMs = processIntervalMs;
            this.maxItemsInCollection = maxItemsInCollection;
            stringPool = StringPool.Shared;

            processTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(this.processIntervalMs)
            };
            processTimer.Tick += ProcessBuffer;
        }
        #endregion

        #region メソッド
        /// <summary>
        /// 処理を開始
        /// </summary>
        public void Start()
        {
            if (!disposed)
            {
                processTimer.Start();
            }
        }

        /// <summary>
        /// 処理を停止
        /// </summary>
        public void Stop()
        {
            processTimer.Stop();
        }

        /// <summary>
        /// バッファ処理（タイマーイベント）
        /// </summary>
        private void ProcessBuffer(object? sender, EventArgs e)
        {
            if (buffer.IsEmpty) return;

            var sw = Stopwatch.StartNew();

            // バッチ取り出し
            var batch = buffer.DequeueBatch();

            if (batch.Count > 0)
            {
                // String Interning を適用
                foreach (var entry in batch)
                {
                    entry.InternStrings(stringPool);
                }

                // コレクションに追加（上限付き）
                collection.AddRangeWithLimit(batch, maxItemsInCollection);

                // メトリクス更新
                ProcessedBatchCount++;
                TotalProcessedItems += batch.Count;

                sw.Stop();
                LastProcessingTimeMs = sw.Elapsed.TotalMilliseconds;
                AverageProcessingTimeMs =
                    (AverageProcessingTimeMs * (ProcessedBatchCount - 1) + LastProcessingTimeMs)
                    / ProcessedBatchCount;
            }
        }

        /// <summary>
        /// 即座にバッファを処理（手動フラッシュ）
        /// </summary>
        public void Flush()
        {
            while (!buffer.IsEmpty)
            {
                ProcessBuffer(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// メトリクスをリセット
        /// </summary>
        public void ResetMetrics()
        {
            ProcessedBatchCount = 0;
            TotalProcessedItems = 0;
            AverageProcessingTimeMs = 0;
            LastProcessingTimeMs = 0;
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                processTimer.Stop();
                disposed = true;
            }
        }
        #endregion
    }
}
