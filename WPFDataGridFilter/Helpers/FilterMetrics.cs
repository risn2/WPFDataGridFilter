using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// フィルター処理のパフォーマンス計測情報を保持するクラス。
    /// メモリ使用量、フィルター時間、キャッシュヒット率などを追跡します。
    /// </summary>
    public sealed class FilterMetrics : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion

        #region フィールド
        /// <summary>総アイテム数</summary>
        private int totalItems;

        /// <summary>フィルター後のアイテム数</summary>
        private int filteredItems;

        /// <summary>フィルター処理時間（ミリ秒）</summary>
        private double filterDurationMs;

        /// <summary>メモリ使用量（バイト）</summary>
        private long memoryUsageBytes;

        /// <summary>キャッシュヒット回数</summary>
        private int cacheHitCount;

        /// <summary>キャッシュミス回数</summary>
        private int cacheMissCount;

        /// <summary>並列処理使用中か</summary>
        private bool isParallelProcessing;

        /// <summary>現在のデバウンス間隔（ミリ秒）</summary>
        private int currentDebounceMs = 200;
        #endregion

        #region プロパティ
        /// <summary>総アイテム数</summary>
        public int TotalItems
        {
            get => totalItems;
            set { if (totalItems != value) { totalItems = value; OnPropertyChanged(); } }
        }

        /// <summary>フィルター後のアイテム数</summary>
        public int FilteredItems
        {
            get => filteredItems;
            set { if (filteredItems != value) { filteredItems = value; OnPropertyChanged(); } }
        }

        /// <summary>フィルター処理時間（ミリ秒）</summary>
        public double FilterDurationMs
        {
            get => filterDurationMs;
            set { if (Math.Abs(filterDurationMs - value) > double.Epsilon) { filterDurationMs = value; OnPropertyChanged(); } }
        }

        /// <summary>メモリ使用量（バイト）</summary>
        public long MemoryUsageBytes
        {
            get => memoryUsageBytes;
            set { if (memoryUsageBytes != value) { memoryUsageBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(MemoryUsageMB)); } }
        }

        /// <summary>メモリ使用量（MB）</summary>
        public double MemoryUsageMB => memoryUsageBytes / (1024.0 * 1024.0);

        /// <summary>キャッシュヒット回数</summary>
        public int CacheHitCount
        {
            get => cacheHitCount;
            set { if (cacheHitCount != value) { cacheHitCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CacheHitRate)); } }
        }

        /// <summary>キャッシュミス回数</summary>
        public int CacheMissCount
        {
            get => cacheMissCount;
            set { if (cacheMissCount != value) { cacheMissCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CacheHitRate)); } }
        }

        /// <summary>キャッシュヒット率（0.0〜1.0）</summary>
        public double CacheHitRate
        {
            get
            {
                var total = cacheHitCount + cacheMissCount;
                return total > 0 ? (double)cacheHitCount / total : 0.0;
            }
        }

        /// <summary>並列処理を使用中か</summary>
        public bool IsParallelProcessing
        {
            get => isParallelProcessing;
            set { if (isParallelProcessing != value) { isParallelProcessing = value; OnPropertyChanged(); } }
        }

        /// <summary>現在のデバウンス間隔（ミリ秒）</summary>
        public int CurrentDebounceMs
        {
            get => currentDebounceMs;
            set { if (currentDebounceMs != value) { currentDebounceMs = value; OnPropertyChanged(); } }
        }
        #endregion

        #region メソッド
        /// <summary>
        /// 現在のメモリ使用量を更新
        /// </summary>
        public void UpdateMemoryUsage()
        {
            MemoryUsageBytes = GC.GetTotalMemory(forceFullCollection: false);
        }

        /// <summary>
        /// キャッシュヒットを記録
        /// </summary>
        public void RecordCacheHit()
        {
            CacheHitCount++;
        }

        /// <summary>
        /// キャッシュミスを記録
        /// </summary>
        public void RecordCacheMiss()
        {
            CacheMissCount++;
        }

        /// <summary>
        /// すべてのカウンターをリセット
        /// </summary>
        public void Reset()
        {
            TotalItems = 0;
            FilteredItems = 0;
            FilterDurationMs = 0;
            MemoryUsageBytes = 0;
            CacheHitCount = 0;
            CacheMissCount = 0;
            IsParallelProcessing = false;
            CurrentDebounceMs = 200;
        }
        #endregion
    }
}
