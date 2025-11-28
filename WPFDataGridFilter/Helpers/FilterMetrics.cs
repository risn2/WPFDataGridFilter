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
        private int _totalItems;
        private int _filteredItems;
        private double _filterDurationMs;
        private long _memoryUsageBytes;
        private int _cacheHitCount;
        private int _cacheMissCount;
        private bool _isParallelProcessing;
        #endregion

        #region プロパティ
        /// <summary>総アイテム数</summary>
        public int TotalItems
        {
            get => _totalItems;
            set { if (_totalItems != value) { _totalItems = value; OnPropertyChanged(); } }
        }

        /// <summary>フィルター後のアイテム数</summary>
        public int FilteredItems
        {
            get => _filteredItems;
            set { if (_filteredItems != value) { _filteredItems = value; OnPropertyChanged(); } }
        }

        /// <summary>フィルター処理時間（ミリ秒）</summary>
        public double FilterDurationMs
        {
            get => _filterDurationMs;
            set { if (Math.Abs(_filterDurationMs - value) > double.Epsilon) { _filterDurationMs = value; OnPropertyChanged(); } }
        }

        /// <summary>メモリ使用量（バイト）</summary>
        public long MemoryUsageBytes
        {
            get => _memoryUsageBytes;
            set { if (_memoryUsageBytes != value) { _memoryUsageBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(MemoryUsageMB)); } }
        }

        /// <summary>メモリ使用量（MB）</summary>
        public double MemoryUsageMB => _memoryUsageBytes / (1024.0 * 1024.0);

        /// <summary>キャッシュヒット回数</summary>
        public int CacheHitCount
        {
            get => _cacheHitCount;
            set { if (_cacheHitCount != value) { _cacheHitCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CacheHitRate)); } }
        }

        /// <summary>キャッシュミス回数</summary>
        public int CacheMissCount
        {
            get => _cacheMissCount;
            set { if (_cacheMissCount != value) { _cacheMissCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CacheHitRate)); } }
        }

        /// <summary>キャッシュヒット率（0.0〜1.0）</summary>
        public double CacheHitRate
        {
            get
            {
                var total = _cacheHitCount + _cacheMissCount;
                return total > 0 ? (double)_cacheHitCount / total : 0.0;
            }
        }

        /// <summary>並列処理を使用中か</summary>
        public bool IsParallelProcessing
        {
            get => _isParallelProcessing;
            set { if (_isParallelProcessing != value) { _isParallelProcessing = value; OnPropertyChanged(); } }
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
        }
        #endregion
    }
}
