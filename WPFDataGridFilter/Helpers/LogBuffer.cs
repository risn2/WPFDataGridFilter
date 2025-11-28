using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// スレッドセーフなログバッファ。
    /// プロデューサーからのログを蓄積し、バッチで取り出す。
    /// </summary>
    /// <typeparam name="T">バッファに格納する要素の型</typeparam>
    public class LogBuffer<T>
    {
        #region フィールド
        /// <summary>スレッドセーフなキュー</summary>
        private readonly ConcurrentQueue<T> queue = new();

        /// <summary>1回のバッチで取り出す最大件数</summary>
        private readonly int maxBatchSize;
        #endregion

        #region コンストラクタ
        /// <summary>
        /// LogBuffer を初期化します。
        /// </summary>
        /// <param name="maxBatchSize">1回のバッチで取り出す最大件数（デフォルト: 50）</param>
        public LogBuffer(int maxBatchSize = 50)
        {
            this.maxBatchSize = maxBatchSize;
        }
        #endregion

        #region プロパティ
        /// <summary>
        /// バッファ内の件数
        /// </summary>
        public int Count => queue.Count;

        /// <summary>
        /// バッファが空か
        /// </summary>
        public bool IsEmpty => queue.IsEmpty;

        /// <summary>
        /// 1回のバッチで取り出す最大件数
        /// </summary>
        public int MaxBatchSize => maxBatchSize;
        #endregion

        #region メソッド
        /// <summary>
        /// ログを追加（プロデューサースレッドから呼び出し可能）
        /// </summary>
        /// <param name="item">追加するアイテム</param>
        public void Enqueue(T item)
        {
            queue.Enqueue(item);
        }

        /// <summary>
        /// 複数ログを一括追加
        /// </summary>
        /// <param name="items">追加するアイテムのコレクション</param>
        public void EnqueueRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                queue.Enqueue(item);
            }
        }

        /// <summary>
        /// 蓄積されたログをバッチで取り出し。
        /// 最大 MaxBatchSize 件まで取り出します。
        /// </summary>
        /// <returns>取り出したアイテムのリスト</returns>
        public List<T> DequeueBatch()
        {
            var batch = new List<T>(maxBatchSize);

            while (batch.Count < maxBatchSize && queue.TryDequeue(out var item))
            {
                batch.Add(item);
            }

            return batch;
        }

        /// <summary>
        /// バッファをクリア
        /// </summary>
        public void Clear()
        {
            while (queue.TryDequeue(out _)) { }
        }
        #endregion
    }
}
