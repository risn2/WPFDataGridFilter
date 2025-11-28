using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WPFDataGridFilter.Helpers;
using WPFDataGridFilter.ViewModels;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// フィルター機能を備えた DataGrid の派生コントロール
    /// </summary>
    public class FilterableDataGrid : DataGrid, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged実装
        //---------------------------------------------------------
        // INotifyPropertyChanged実装
        //----------------------------------------------------------
        /// <summary>プロパティ変更時イベント</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// プロパティ値に変化があった場合にPropertyChangedイベントを発生させる
        /// </summary>
        /// <param name="propertyName">変更プロパティ名</param>
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion // INotifyPropertyChanged実装

        #region 定数
        //---------------------------------------------------------
        // 定数
        //---------------------------------------------------------
        /// <summary>日時フィルター用キー</summary>
        private const string TimeFilterKey = "Time";

        /// <summary>タイムスタンプフィルター用キー</summary>
        private const string TimeStampFilterKey = "Timestamp";
        #endregion // 定数

        #region フィールド
        /// <summary>一覧表示用コレクションビュー保持</summary>
        private ICollectionView? itemsView;

        /// <summary>フィルター適用対象コレクションビュー購読先</summary>
        private ICollectionView? itemsViewSubscriptionTarget;

        /// <summary>アイテム変更通知監視コレクション</summary>
        private INotifyCollectionChanged? collectionChangedSource;

        /// <summary>直近フィルター処理時間(ms)</summary>
        private double filterElapsedMs;

        /// <summary>総件数保持</summary>
        private int totalCount;

        /// <summary>フィルター後件数保持</summary>
        private int filteredCount;

        /// <summary>正規表現キャッシュ辞書</summary>
        private static readonly ConcurrentDictionary<string, Regex?> regexCache = new();

        /// <summary>プロパティ参照キャッシュ辞書</summary>
        private static readonly ConcurrentDictionary<(Type Type, string Name), Func<object, object?>> memberAccessors = new();

        /// <summary>日時解析で利用するフォーマット候補</summary>
        private static readonly string[] supportedTimeFormats =
        {
            "yyyy/MM/dd HH:mm:ss.fff",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss"
        };

        /// <summary>フィルター更新デバウンス用タイマー（ユーザー入力用）</summary>
        private DispatcherTimer? debounceTimer;

        /// <summary>通常時デバウンス待機時間（ミリ秒）- 業界標準下限</summary>
        private const int NormalDebounceDelayMs = 200;

        /// <summary>高負荷時デバウンス待機時間（ミリ秒）- 業界標準上限</summary>
        private const int HighLoadDebounceDelayMs = 400;

        /// <summary>アイドル時デバウンス待機時間（ミリ秒）</summary>
        private const int IdleDebounceDelayMs = 100;

        /// <summary>高負荷判定閾値（件/秒）</summary>
        private const int HighLoadThreshold = 50;

        /// <summary>現在のデバウンス間隔（ミリ秒）</summary>
        private int currentDebounceDelayMs = NormalDebounceDelayMs;

        /// <summary>コレクション変更用スロットリング間隔（ミリ秒）</summary>
        private const int CollectionThrottleMs = 500;

        /// <summary>最後のコレクション変更によるフィルター更新時刻</summary>
        private DateTime lastCollectionFilterRefresh = DateTime.MinValue;

        /// <summary>追加頻度計算用カウンター</summary>
        private int recentAddCount;

        /// <summary>追加頻度計算用リセット時刻</summary>
        private DateTime lastAddCountReset = DateTime.Now;

        /// <summary>並列処理を使用する閾値（アイテム数）</summary>
        private const int ParallelThreshold = 5000;

        /// <summary>フィルター結果キャッシュ（並列処理用）</summary>
        private List<object>? filteredResultsCache;

        /// <summary>キャッシュが有効か</summary>
        private bool useFilteredCache;

        /// <summary>並列処理用にキャプチャした TimeFrom</summary>
        private DateTime? capturedTimeFrom;

        /// <summary>並列処理用にキャプチャした TimeTo</summary>
        private DateTime? capturedTimeTo;

        /// <summary>並列処理用にキャプチャした FilterTexts</summary>
        private List<KeyValuePair<string, string?>>? capturedFilterTexts;

        /// <summary>並列処理用にキャプチャした FilterSelections</summary>
        private List<KeyValuePair<string, HashSet<string>>>? capturedFilterSelections;

        /// <summary>プロパティ値インデックス（選択フィルター高速化用）</summary>
        private readonly PropertyIndex propertyIndex = new();

        /// <summary>インデックス構築対象のプロパティ名リスト</summary>
        private static readonly string[] IndexableProperties = { "IFNum", "Source", "Destination", "Event" };
        #endregion // フィールド

        #region プロパティ
        /// <summary>列単位フィルター文字列保持</summary>
        public FilterTextCollection FilterTexts { get; } = new();

        /// <summary>列単位の選択フィルター保持</summary>
        public FilterSelectionCollection FilterSelections { get; } = new();

        /// <summary>いずれかのフィルターが有効か判定</summary>
        public bool HasActiveFilters => FilterTexts.Any() || FilterSelections.Any() || TimeFrom.HasValue || TimeTo.HasValue;

        /// <summary>全フィルタークリアコマンド参照</summary>
        public ICommand ClearAllFiltersCommand { get; }

        /// <summary>現在コレクションビュー参照</summary>
        public ICollectionView? ItemsView
        {
            get => itemsView;
            private set
            {
                if (!ReferenceEquals(itemsView, value))
                {
                    itemsView = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>フィルター処理所要時間(ms)参照</summary>
        public double FilterElapsedMs
        {
            get => filterElapsedMs;
            private set
            {
                if (Math.Abs(filterElapsedMs - value) > double.Epsilon)
                {
                    filterElapsedMs = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>総件数参照</summary>
        public int TotalCount
        {
            get => totalCount;
            private set
            {
                if (totalCount != value)
                {
                    totalCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>フィルター後件数参照</summary>
        public int FilteredCount
        {
            get => filteredCount;
            private set
            {
                if (filteredCount != value)
                {
                    filteredCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>TimeFrom 依存関係プロパティ</summary>
        public static readonly DependencyProperty TimeFromProperty = DependencyProperty.Register(
            nameof(TimeFrom), typeof(DateTime?), typeof(FilterableDataGrid),
            new PropertyMetadata(null, OnFilterPropertyChanged));

        /// <summary>フィルター対象開始日時参照</summary>
        public DateTime? TimeFrom
        {
            get => (DateTime?)GetValue(TimeFromProperty);
            set => SetValue(TimeFromProperty, value);
        }

        /// <summary>TimeTo 依存関係プロパティ</summary>
        public static readonly DependencyProperty TimeToProperty = DependencyProperty.Register(
            nameof(TimeTo), typeof(DateTime?), typeof(FilterableDataGrid),
            new PropertyMetadata(null, OnFilterPropertyChanged));

        /// <summary>フィルター対象終了日時参照</summary>
        public DateTime? TimeTo
        {
            get => (DateTime?)GetValue(TimeToProperty);
            set => SetValue(TimeToProperty, value);
        }

        /// <summary>日時フィルタークリアコマンド参照</summary>
        public ICommand ClearTimeRangeCommand { get; }

        /// <summary>フィルター性能メトリクス参照</summary>
        public FilterMetrics Metrics { get; } = new();
        #endregion // プロパティ

        #region コンストラクター
        /// <summary>
        /// FilterableDataGrid インスタンス生成
        /// </summary>
        public FilterableDataGrid()
        {
            AutoGenerateColumns = false;
            IsReadOnly = true;
            HeadersVisibility = DataGridHeadersVisibility.Column;

            // UI 仮想化の最適化設定
            VirtualizingPanel.SetIsVirtualizing(this, true);
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetCacheLength(this, new VirtualizationCacheLength(10, 10));
            VirtualizingPanel.SetCacheLengthUnit(this, VirtualizationCacheLengthUnit.Page);
            EnableRowVirtualization = true;
            EnableColumnVirtualization = true;

            ClearTimeRangeCommand = new RelayCommand(_ =>
            {
                TimeFrom = null;
                TimeTo = null;
            });

            ClearAllFiltersCommand = new RelayCommand(_ => ClearAllFilters(), _ => HasActiveFilters);

            FilterTexts.CollectionChanged += (_, __) => ScheduleFilterRefresh();
            FilterSelections.CollectionChanged += (_, __) => ScheduleFilterRefresh();

            Unloaded += (_, __) => DetachCollectionEvents();
        }
        #endregion // コンストラクター

        #region メソッド
        /// <summary>
        /// ItemsSource 変更時のフィルター再初期化
        /// </summary>
        /// <param name="oldValue">旧 ItemsSource</param>
        /// <param name="newValue">新 ItemsSource</param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            DetachCollectionEvents();
            base.OnItemsSourceChanged(oldValue, newValue);
            AttachToItemsSource(newValue);
            BuildPropertyIndices();
            RefreshFilter();
        }

        /// <summary>
        /// プロパティインデックスを構築
        /// </summary>
        private void BuildPropertyIndices()
        {
            if (ItemsSource is IList source)
            {
                propertyIndex.SetSource(source);
                foreach (var prop in IndexableProperties)
                {
                    propertyIndex.BuildIndex(prop);
                }
            }
            else
            {
                propertyIndex.SetSource(null);
            }
        }

        /// <summary>
        /// フィルター対象ソースへのコレクションビュー設定
        /// </summary>
        /// <param name="source">フィルター対象コレクション</param>
        private void AttachToItemsSource(IEnumerable? source)
        {
            if (source is null)
            {
                ItemsView = null;
                UpdateCounts();
                return;
            }

            var view = CollectionViewSource.GetDefaultView(source);

            if (itemsViewSubscriptionTarget is INotifyCollectionChanged oldView)
            {
                oldView.CollectionChanged -= ItemsView_CollectionChanged;
                itemsViewSubscriptionTarget = null;
            }

            if (collectionChangedSource is not null)
            {
                collectionChangedSource.CollectionChanged -= SourceIncc_CollectionChanged;
                collectionChangedSource = null;
            }

            ItemsView = view;

            if (view != null)
            {
                view.Filter = Filter;
                if (view is INotifyCollectionChanged viewCollectionChanged)
                {
                    itemsViewSubscriptionTarget = view;
                    viewCollectionChanged.CollectionChanged += ItemsView_CollectionChanged;
                }
            }

            if (source is INotifyCollectionChanged sourceCollectionChanged)
            {
                collectionChangedSource = sourceCollectionChanged;
                sourceCollectionChanged.CollectionChanged += SourceIncc_CollectionChanged;
            }

            UpdateCounts();
        }

        /// <summary>
        /// コレクション関連イベント購読解除
        /// </summary>
        private void DetachCollectionEvents()
        {
            if (itemsViewSubscriptionTarget is INotifyCollectionChanged oldView)
            {
                oldView.CollectionChanged -= ItemsView_CollectionChanged;
                itemsViewSubscriptionTarget = null;
            }

            if (collectionChangedSource is not null)
            {
                collectionChangedSource.CollectionChanged -= SourceIncc_CollectionChanged;
                collectionChangedSource = null;
            }

            // デバウンスタイマーを停止
            debounceTimer?.Stop();
        }

        /// <summary>
        /// デバウンス付きフィルター更新をスケジュール（ユーザー入力用）
        /// </summary>
        private void ScheduleFilterRefresh()
        {
            ScheduleFilterRefresh(isUserInput: true);
        }

        /// <summary>
        /// デバウンス付きフィルター更新をスケジュール
        /// </summary>
        /// <param name="isUserInput">ユーザー入力によるトリガーか</param>
        private void ScheduleFilterRefresh(bool isUserInput)
        {
            if (debounceTimer == null)
            {
                debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(currentDebounceDelayMs)
                };
                debounceTimer.Tick += (_, __) =>
                {
                    debounceTimer.Stop();
                    RefreshFilter();
                };
            }

            if (isUserInput)
            {
                // ユーザー入力時はデバウンス（タイマーリセット）
                debounceTimer.Stop();
                debounceTimer.Interval = TimeSpan.FromMilliseconds(currentDebounceDelayMs);
                debounceTimer.Start();
            }
            else
            {
                // コレクション変更時はスロットリング（最小間隔を保証）
                var elapsed = DateTime.Now - lastCollectionFilterRefresh;
                if (elapsed.TotalMilliseconds >= CollectionThrottleMs)
                {
                    // 間隔経過済み: 即座にフィルター更新
                    if (!debounceTimer.IsEnabled)
                    {
                        RefreshFilter();
                        lastCollectionFilterRefresh = DateTime.Now;
                    }
                }
                // 間隔内: 次のデバウンスタイマー発火で更新される
            }
        }

        /// <summary>
        /// 追加頻度に基づきデバウンス間隔を調整
        /// </summary>
        private void UpdateAdaptiveDebounce()
        {
            var elapsed = (DateTime.Now - lastAddCountReset).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var addRate = recentAddCount / elapsed;
                recentAddCount = 0;
                lastAddCountReset = DateTime.Now;

                // 追加頻度に応じてデバウンス間隔を調整
                if (addRate > HighLoadThreshold)
                {
                    currentDebounceDelayMs = HighLoadDebounceDelayMs;
                }
                else if (addRate < 5)
                {
                    currentDebounceDelayMs = IdleDebounceDelayMs;
                }
                else
                {
                    currentDebounceDelayMs = NormalDebounceDelayMs;
                }

                // メトリクスに現在のデバウンス値を反映
                Metrics.CurrentDebounceMs = currentDebounceDelayMs;
            }
        }

        /// <summary>
        /// フィルター処理実行と所要時間および件数更新
        /// </summary>
        private void RefreshFilter()
        {
            if (ItemsView is null)
            {
                UpdateCounts();
                NotifyFilterStateChanged();
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            // 大量データの場合は並列処理を使用
            var sourceList = ItemsSource as IList;
            if (sourceList != null && sourceList.Count >= ParallelThreshold)
            {
                RefreshFilterParallel(sourceList);
                Metrics.IsParallelProcessing = true;
            }
            else
            {
                // 通常のフィルター処理
                useFilteredCache = false;
                filteredResultsCache = null;
                ItemsView.Refresh();
                Metrics.IsParallelProcessing = false;
            }

            stopwatch.Stop();

            FilterElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            Metrics.FilterDurationMs = FilterElapsedMs;
            Metrics.UpdateMemoryUsage();
            UpdateCounts();
            NotifyFilterStateChanged();
        }

        /// <summary>
        /// 大量データ向けの並列フィルター処理
        /// </summary>
        /// <param name="source">フィルター対象リスト</param>
        private void RefreshFilterParallel(IList source)
        {
            // UIスレッドでのみアクセス可能なプロパティを事前にキャプチャ
            capturedTimeFrom = TimeFrom;
            capturedTimeTo = TimeTo;
            capturedFilterTexts = FilterTexts.ToList();
            capturedFilterSelections = FilterSelections
                .Select(kv => new KeyValuePair<string, HashSet<string>>(kv.Key, new HashSet<string>(kv.Value, StringComparer.Ordinal)))
                .ToList();

            // 並列でフィルター処理を実行
            var filtered = source.Cast<object>()
                .AsParallel()
                .AsOrdered()
                .Where(item => FilterParallel(item))
                .ToList();

            // キャプチャをクリア
            capturedTimeFrom = null;
            capturedTimeTo = null;
            capturedFilterTexts = null;
            capturedFilterSelections = null;

            filteredResultsCache = filtered;
            useFilteredCache = true;

            // キャッシュを使ったフィルターで Refresh
            ItemsView!.Refresh();

            useFilteredCache = false;
        }

        /// <summary>
        /// 並列処理用のフィルター判定（キャプチャ済みの値を使用）
        /// </summary>
        /// <param name="item">判定対象アイテム</param>
        /// <returns>表示対象なら true</returns>
        private bool FilterParallel(object item)
        {
            if (item is null) return false;

            // 日時フィルターを確認（キャプチャ済みの値を使用）
            if (!ApplyTimeFilterParallel(item)) return false;

            // 選択フィルター（キャプチャ済みの値を使用）
            if (capturedFilterSelections != null)
            {
                foreach (var selection in capturedFilterSelections)
                {
                    if (string.IsNullOrWhiteSpace(selection.Key)) continue;
                    if (string.Equals(selection.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)) continue;

                    var allowed = selection.Value;
                    if (allowed.Count == 0) return false;

                    var candidate = NormalizeValue(ResolveMemberAsString(item, selection.Key));
                    if (!allowed.Contains(candidate)) return false;
                }
            }

            // テキストフィルター（キャプチャ済みの値を使用）
            if (capturedFilterTexts != null)
            {
                foreach (var filter in capturedFilterTexts)
                {
                    if (string.IsNullOrWhiteSpace(filter.Key)) continue;
                    if (string.Equals(filter.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)) continue;

                    if (!Match(ResolveMemberAsString(item, filter.Key), filter.Value)) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 並列処理用の日時フィルター判定（キャプチャ済みの値を使用）
        /// </summary>
        /// <param name="item">判定対象アイテム</param>
        /// <returns>条件を満たす場合は true</returns>
        private bool ApplyTimeFilterParallel(object item)
        {
            var timeFilterText = capturedFilterTexts?.FirstOrDefault(kv =>
                string.Equals(kv.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)).Value;
            var hasTimeKey = !string.IsNullOrWhiteSpace(timeFilterText);
            var hasRange = capturedTimeFrom.HasValue || capturedTimeTo.HasValue;

            if (!hasTimeKey && !hasRange) return true;

            var timeText = ResolveMemberAsString(item, TimeFilterKey);
            var normalizedTime = NormalizeValue(timeText);

            if (!Match(timeText, timeFilterText)) return false;

            var timeSelection = capturedFilterSelections?.FirstOrDefault(kv =>
                string.Equals(kv.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)).Value;
            if (timeSelection != null && timeSelection.Count > 0)
            {
                if (!timeSelection.Contains(normalizedTime)) return false;
            }

            if (!hasRange) return true;

            var candidate = ResolveDateTimeCandidate(item, timeText);
            if (!candidate.HasValue) return false;

            if (capturedTimeFrom.HasValue && candidate.Value < capturedTimeFrom.Value) return false;
            if (capturedTimeTo.HasValue && candidate.Value > capturedTimeTo.Value) return false;

            return true;
        }

        /// <summary>
        /// 各アイテムがフィルター条件を満たすか判定
        /// </summary>
        /// <param name="item">判定対象アイテム</param>
        /// <returns>表示対象なら true</returns>
        private bool Filter(object item)
        {
            if (item is null) return false;

            // 並列処理済みキャッシュがある場合はそれを参照
            if (useFilteredCache && filteredResultsCache != null)
            {
                return filteredResultsCache.Contains(item);
            }

            // 日時フィルターを確認（軽量な判定を先に）
            if (!ApplyTimeFilter(item)) return false;

            // 選択フィルター（比較的軽量）
            foreach (var selection in FilterSelections)
            {
                if (string.IsNullOrWhiteSpace(selection.Key)) continue;

                if (string.Equals(selection.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)) continue;

                var allowed = selection.Value;

                if (allowed.Count == 0)
                {
                    return false;
                }

                var candidate = NormalizeValue(ResolveMemberAsString(item, selection.Key));
                if (!allowed.Contains(candidate))
                {
                    return false;
                }
            }

            // テキストフィルター（正規表現、最もコスト高）
            foreach (var filter in FilterTexts)
            {
                if (string.IsNullOrWhiteSpace(filter.Key)) continue;

                // Time キーは日時フィルターで処理済み
                if (string.Equals(filter.Key, TimeFilterKey, StringComparison.OrdinalIgnoreCase)) continue;

                // キーに対応するプロパティ値がフィルターにマッチするか判定
                if (!Match(ResolveMemberAsString(item, filter.Key), filter.Value)) return false;
            }

            return true;
        }

        /// <summary>
        /// 指定値がフィルター文字列にマッチするか判定
        /// </summary>
        /// <param name="value">比較対象値</param>
        /// <param name="filter">フィルター文字列</param>
        /// <returns>マッチすれば true</returns>
        private static bool Match(string? value, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            if (value is null)
            {
                return false;
            }

            var regex = regexCache.GetOrAdd(filter, static pattern =>
            {
                try
                {
                    return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                catch
                {
                    return null;
                }
            });

            return regex?.IsMatch(value) == true;
        }

        /// <summary>
        /// 総件数および表示件数再計算
        /// </summary>
        private void UpdateCounts()
        {
            var source = ItemsSource;

            if (source is ICollection collection)
            {
                TotalCount = collection.Count;
            }
            else if (source != null)
            {
                TotalCount = source.Cast<object>().Count();
            }
            else
            {
                TotalCount = 0;
            }

            FilteredCount = ItemsView?.Cast<object>().Count() ?? 0;

            // メトリクスも更新
            Metrics.TotalItems = TotalCount;
            Metrics.FilteredItems = FilteredCount;
        }

        /// <summary>選択フィルターで表示する候補の上限数</summary>
        private const int MaxDistinctValuesForSelection = 500;

        /// <summary>
        /// 指定プロパティの重複排除済み値一覧を取得
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        /// <returns>値一覧（上限を超えた場合は null）</returns>
        internal IReadOnlyList<string>? GetDistinctValues(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || ItemsSource is null)
            {
                return Array.Empty<string>();
            }

            // インデックスが存在すれば高速パスを使用
            var indexed = propertyIndex.GetDistinctValuesFromIndex(propertyName);
            if (indexed != null)
            {
                if (indexed.Count > MaxDistinctValuesForSelection)
                {
                    return null;
                }
                Metrics.RecordCacheHit();
                return indexed;
            }

            Metrics.RecordCacheMiss();

            // フォールバック: 全件走査
            var values = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in ItemsSource.Cast<object>())
            {
                var candidate = NormalizeValue(ResolveMemberAsString(item, propertyName));
                values.Add(candidate);

                // 上限を超えた場合は早期終了（選択フィルター無効化）
                if (values.Count > MaxDistinctValuesForSelection)
                {
                    return null;
                }
            }

            var list = values.ToList();
            list.Sort(StringComparer.CurrentCulture);
            return list;
        }

        #region イベントハンドラー
        /// <summary>
        /// コレクションビュー変更通知による件数更新
        /// </summary>
        private void ItemsView_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateCounts();
        }

        /// <summary>
        /// 元コレクション変更時のフィルター更新（スロットリング適用）
        /// </summary>
        private void SourceIncc_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 追加件数をカウント（適応的デバウンス用）
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                recentAddCount += e.NewItems.Count;
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Reset の場合は推定値を加算
                recentAddCount += 10;

                // Reset（バッチ追加）時はインデックスを再構築
                BuildPropertyIndices();
            }

            // 適応的デバウンス間隔を更新
            UpdateAdaptiveDebounce();

            // 件数更新
            UpdateCounts();

            // コレクション変更時はスロットリングでフィルター更新
            ScheduleFilterRefresh(isUserInput: false);
        }
        #endregion // イベントハンドラー

        /// <summary>
        /// 依存関係プロパティ変更契機のフィルター更新
        /// </summary>
        private static void OnFilterPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((FilterableDataGrid)dependencyObject).ScheduleFilterRefresh();
        }

        /// <summary>
        /// 時刻フィルターと範囲フィルターの適用判定
        /// </summary>
        /// <param name="item">判定対象アイテム</param>
        /// <returns>条件を満たす場合は true</returns>
        private bool ApplyTimeFilter(object item)
        {
            var hasTimeKey = FilterTexts.ContainsKey(TimeFilterKey);
            var hasRange = TimeFrom.HasValue || TimeTo.HasValue;

            if (!hasTimeKey && !hasRange)
            {
                return true;
            }

            var timeText = ResolveMemberAsString(item, TimeFilterKey);
            var normalizedTime = NormalizeValue(timeText);

            if (!Match(timeText, FilterTexts[TimeFilterKey]))
            {
                return false;
            }

            if (FilterSelections.TryGetValue(TimeFilterKey, out var timeSelection))
            {
                if (timeSelection.Count == 0 || !timeSelection.Contains(normalizedTime))
                {
                    return false;
                }
            }

            if (!hasRange)
            {
                return true;
            }

            var candidate = ResolveDateTimeCandidate(item, timeText);

            if (!candidate.HasValue)
            {
                return false;
            }

            if (TimeFrom.HasValue && candidate.Value < TimeFrom.Value)
            {
                return false;
            }

            if (TimeTo.HasValue && candidate.Value > TimeTo.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定プロパティ値の文字列表現取得
        /// </summary>
        /// <param name="item">対象アイテム</param>
        /// <param name="propertyName">参照プロパティ名</param>
        /// <returns>プロパティ文字列</returns>
        private static string? ResolveMemberAsString(object item, string propertyName)
        {
            var value = ResolveMember(item, propertyName);
            return value?.ToString();
        }

        /// <summary>
        /// 指定プロパティ値取得
        /// </summary>
        /// <param name="item">対象アイテム</param>
        /// <param name="propertyName">参照プロパティ名</param>
        /// <returns>プロパティ値</returns>
        private static object? ResolveMember(object item, string propertyName)
        {
            if (item is null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var key = (item.GetType(), propertyName);

            var accessor = memberAccessors.GetOrAdd(key, static tuple =>
            {
                var (type, name) = tuple;
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property is null)
                {
                    return _ => null;
                }

                return target => property.GetValue(target);
            });

            return accessor(item);
        }

        /// <summary>
        /// 日時候補の取得
        /// </summary>
        /// <param name="item">対象アイテム</param>
        /// <param name="timeText">文字列表現</param>
        /// <returns>日時候補</returns>
        private static DateTime? ResolveDateTimeCandidate(object item, string? timeText)
        {
            var timeStamp = ConvertToDateTime(ResolveMember(item, TimeStampFilterKey));
            if (timeStamp.HasValue)
            {
                return timeStamp;
            }

            if (TryParseDateTime(timeText, out var parsed))
            {
                return parsed;
            }

            return ConvertToDateTime(ResolveMember(item, TimeFilterKey));
        }

        /// <summary>
        /// 任意オブジェクトから DateTime? へ変換
        /// </summary>
        /// <param name="value">変換対象</param>
        /// <returns>変換結果</returns>
        private static DateTime? ConvertToDateTime(object? value)
        {
            return value switch
            {
                null => null,
                DateTime dt => dt,
                DateTimeOffset dto => dto.LocalDateTime,
                string text when TryParseDateTime(text, out var parsed) => parsed,
                _ => null
            };
        }

        /// <summary>
        /// 文字列から日時解析を試行
        /// </summary>
        /// <param name="value">解析対象</param>
        /// <param name="result">解析結果</param>
        /// <returns>成功時は true</returns>
        private static bool TryParseDateTime(string? value, out DateTime result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default;
                return false;
            }

            if (DateTime.TryParseExact(value, supportedTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            {
                return true;
            }

            return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
        }

        /// <summary>
        /// null 許容文字列を比較用に正規化
        /// </summary>
        /// <param name="value">入力文字列</param>
        /// <returns>null が与えられた場合は空文字</returns>
        private static string NormalizeValue(string? value) => value ?? string.Empty;

        /// <summary>
        /// すべてのフィルターをリセット
        /// </summary>
        private void ClearAllFilters()
        {
            FilterTexts.ClearAll();
            FilterSelections.ClearAll();
            TimeFrom = null;
            TimeTo = null;

            if (!HasActiveFilters)
            {
                RefreshFilter();
            }
        }

        /// <summary>
        /// フィルター状態変更通知
        /// </summary>
        private void NotifyFilterStateChanged()
        {
            OnPropertyChanged(nameof(HasActiveFilters));
            CommandManager.InvalidateRequerySuggested();
        }
        #endregion // メソッド
    }

    /// <summary>
    /// 文字列キーとフィルター語句のペアを保持するコレクション
    /// </summary>
    public sealed class FilterTextCollection : INotifyPropertyChanged, IEnumerable<KeyValuePair<string, string?>>
    {
        #region INotifyPropertyChanged実装
        //---------------------------------------------------------
        // INotifyPropertyChanged実装
        //----------------------------------------------------------
        /// <summary>プロパティ変更時イベント</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// プロパティ値に変化があった場合にPropertyChangedイベントを発生させる
        /// </summary>
        /// <param name="propertyName">変更プロパティ名</param>
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion // INotifyPropertyChanged実装

        #region フィールド
        /// <summary>フィルター語句格納ディクショナリ</summary>
        private readonly Dictionary<string, string?> inner = new(StringComparer.OrdinalIgnoreCase);
        #endregion // フィールド

        #region イベント
        /// <summary>コレクション状態変化イベント</summary>
        public event EventHandler? CollectionChanged;
        #endregion // イベント

        #region プロパティ
        /// <summary>キーに紐づくフィルター文字列参照</summary>
        /// <param name="key">フィルター対象キー</param>
        public string? this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return null;

                return inner.TryGetValue(key, out var value) ? value : null;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(key)) return;

                var normalizedKey = key.Trim();
                var newValue = string.IsNullOrWhiteSpace(value) ? null : value;

                if (inner.TryGetValue(normalizedKey, out var existing))
                {
                    if (existing == newValue) return;

                    if (newValue is null)
                    {
                        inner.Remove(normalizedKey);
                    }
                    else
                    {
                        inner[normalizedKey] = newValue;
                    }
                }
                else
                {
                    if (newValue is null) return;

                    inner.Add(normalizedKey, newValue);
                }

                OnPropertyChanged("Item[]");
                OnPropertyChanged(normalizedKey);
                CollectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion // プロパティ

        #region メソッド
        /// <summary>
        /// コレクション列挙子取得
        /// </summary>
        /// <returns>列挙子</returns>
        public IEnumerator<KeyValuePair<string, string?>> GetEnumerator() => inner.GetEnumerator();

        /// <summary>
        /// コレクション列挙子取得(非ジェネリック)
        /// </summary>
        /// <returns>列挙子</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 指定キー登録有無確認
        /// </summary>
        /// <param name="key">検査キー</param>
        /// <returns>登録済みなら true</returns>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            return inner.ContainsKey(key.Trim());
        }

        /// <summary>
        /// 指定キーの値取得を試行
        /// </summary>
        /// <param name="key">検査キー</param>
        /// <param name="value">取得値</param>
        /// <returns>取得できれば true</returns>
        public bool TryGetValue(string key, out string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            return inner.TryGetValue(key.Trim(), out value);
        }

        /// <summary>
        /// すべてのフィルター文字列を削除
        /// </summary>
        public void ClearAll()
        {
            if (inner.Count == 0)
            {
                return;
            }

            var keys = inner.Keys.ToList();
            inner.Clear();

            OnPropertyChanged("Item[]");
            foreach (var key in keys)
            {
                OnPropertyChanged(key);
            }

            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion // メソッド
    }

    /// <summary>
    /// 文字列キーに対応する選択済み値集合を保持するコレクション
    /// </summary>
    public sealed class FilterSelectionCollection : INotifyPropertyChanged, IEnumerable<KeyValuePair<string, IReadOnlyCollection<string>>>
    {
        #region INotifyPropertyChanged実装
        //---------------------------------------------------------
        // INotifyPropertyChanged実装
        //----------------------------------------------------------
        /// <summary>プロパティ変更時イベント</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// プロパティ値に変化があった場合にPropertyChangedイベントを発生させる
        /// </summary>
        /// <param name="propertyName">変更プロパティ名</param>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion // INotifyPropertyChanged実装

        #region フィールド
        /// <summary>選択値格納ディクショナリ</summary>
        private readonly Dictionary<string, HashSet<string>> inner = new(StringComparer.OrdinalIgnoreCase);
        #endregion // フィールド

        #region イベント
        /// <summary>コレクション状態変化イベント</summary>
        public event EventHandler? CollectionChanged;
        #endregion // イベント

        #region プロパティ・メソッド
        /// <summary>指定キーに対応する選択集合参照</summary>
        /// <param name="key">対象キー</param>
        public IReadOnlyCollection<string>? this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return null;

                return inner.TryGetValue(key.Trim(), out var value) ? value : null;
            }
        }

        /// <summary>
        /// 選択集合を設定
        /// </summary>
        /// <param name="key">対象キー</param>
        /// <param name="selections">選択集合（null の場合は削除）</param>
        public void SetSelections(string key, IEnumerable<string>? selections)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var normalizedKey = key.Trim();

            if (selections is null)
            {
                Clear(normalizedKey);
                return;
            }

            var normalizedValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in selections)
            {
                normalizedValues.Add(value ?? string.Empty);
            }

            inner[normalizedKey] = normalizedValues;

            OnPropertyChanged("Item[]");
            OnPropertyChanged(normalizedKey);
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 指定キーを削除
        /// </summary>
        /// <param name="key">削除キー</param>
        public void Clear(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var normalizedKey = key.Trim();
            if (!inner.Remove(normalizedKey)) return;

            OnPropertyChanged("Item[]");
            OnPropertyChanged(normalizedKey);
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 指定キーの存在確認
        /// </summary>
        /// <param name="key">確認キー</param>
        /// <returns>存在すれば true</returns>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            return inner.ContainsKey(key.Trim());
        }

        /// <summary>
        /// 選択集合取得を試行
        /// </summary>
        /// <param name="key">確認キー</param>
        /// <param name="values">取得集合</param>
        /// <returns>取得できれば true</returns>
        public bool TryGetValue(string key, out IReadOnlyCollection<string> values)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                values = Array.Empty<string>();
                return false;
            }

            var normalizedKey = key.Trim();
            if (inner.TryGetValue(normalizedKey, out var set))
            {
                values = set;
                return true;
            }

            values = Array.Empty<string>();
            return false;
        }

        /// <summary>
        /// コレクション列挙子取得
        /// </summary>
        /// <returns>列挙子</returns>
        public IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>> GetEnumerator()
        {
            foreach (var pair in inner)
            {
                yield return new KeyValuePair<string, IReadOnlyCollection<string>>(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// コレクション列挙子取得(非ジェネリック)
        /// </summary>
        /// <returns>列挙子</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// すべての選択状態を削除
        /// </summary>
        public void ClearAll()
        {
            if (inner.Count == 0)
            {
                return;
            }

            var keys = inner.Keys.ToList();
            inner.Clear();

            OnPropertyChanged("Item[]");
            foreach (var key in keys)
            {
                OnPropertyChanged(key);
            }

            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion プロパティ・メソッド
    }
}
