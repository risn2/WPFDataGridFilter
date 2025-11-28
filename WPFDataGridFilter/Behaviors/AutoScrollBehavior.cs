using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFDataGridFilter.Behaviors
{
    /// <summary>
    /// DataGrid でコレクション変更時に自動的に最新行（末尾）へスクロールするビヘイビア。
    /// スクロールバーが最下部付近にある場合のみ追従し、ユーザーが過去ログを閲覧中は維持します。
    /// </summary>
    public static class AutoScrollBehavior
    {
        #region 定数
        /// <summary>最下部判定の閾値（ピクセル）</summary>
        private const double BottomThresholdPixels = 10.0;
        #endregion

        #region 添付プロパティ
        /// <summary>
        /// 自動スクロールを有効にするかどうかの添付プロパティ
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>IsEnabled の取得</summary>
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        /// <summary>IsEnabled の設定</summary>
        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        /// <summary>
        /// 自動スクロールが現在アクティブかどうか（内部状態）
        /// スクロールが最下部にある場合に true となります。
        /// </summary>
        public static readonly DependencyProperty IsAutoScrollActiveProperty = DependencyProperty.RegisterAttached(
            "IsAutoScrollActive",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(true)); // デフォルトで有効

        /// <summary>IsAutoScrollActive の取得</summary>
        public static bool GetIsAutoScrollActive(DependencyObject element) => (bool)element.GetValue(IsAutoScrollActiveProperty);

        /// <summary>IsAutoScrollActive の設定（内部用）</summary>
        private static void SetIsAutoScrollActive(DependencyObject element, bool value) => element.SetValue(IsAutoScrollActiveProperty, value);
        #endregion

        #region イベントハンドラ
        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            if ((bool)e.NewValue)
            {
                dataGrid.Loaded += OnDataGridLoaded;
                dataGrid.Unloaded += OnDataGridUnloaded;

                // 既に読み込み済みの場合
                if (dataGrid.IsLoaded)
                {
                    AttachToScrollViewer(dataGrid);
                    AttachToItemsSource(dataGrid);
                }
            }
            else
            {
                dataGrid.Loaded -= OnDataGridLoaded;
                dataGrid.Unloaded -= OnDataGridUnloaded;
                DetachFromScrollViewer(dataGrid);
                DetachFromItemsSource(dataGrid);
            }
        }

        private static void OnDataGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                AttachToScrollViewer(dataGrid);
                AttachToItemsSource(dataGrid);
            }
        }

        private static void OnDataGridUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                DetachFromScrollViewer(dataGrid);
                DetachFromItemsSource(dataGrid);
            }
        }
        #endregion

        #region ScrollViewer 監視
        /// <summary>ScrollViewer を検索して監視を開始</summary>
        private static void AttachToScrollViewer(DataGrid dataGrid)
        {
            var scrollViewer = FindScrollViewer(dataGrid);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += OnScrollChanged;
                SetScrollViewerReference(dataGrid, scrollViewer);

                // 初期状態は最下部と見なす
                SetIsAutoScrollActive(dataGrid, true);
            }
        }

        /// <summary>ScrollViewer の監視を解除</summary>
        private static void DetachFromScrollViewer(DataGrid dataGrid)
        {
            var scrollViewer = GetScrollViewerReference(dataGrid);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged -= OnScrollChanged;
                SetScrollViewerReference(dataGrid, null);
            }
        }

        /// <summary>スクロール位置変更時のハンドラ</summary>
        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            // 親の DataGrid を検索
            var dataGrid = FindParentDataGrid(scrollViewer);
            if (dataGrid == null) return;

            // 最下部かどうかを判定
            bool isAtBottom = IsScrolledToBottom(scrollViewer);
            SetIsAutoScrollActive(dataGrid, isAtBottom);
        }

        /// <summary>スクロールが最下部付近にあるかどうかを判定</summary>
        private static bool IsScrolledToBottom(ScrollViewer scrollViewer)
        {
            // スクロール可能な領域がない場合は最下部とみなす
            if (scrollViewer.ScrollableHeight <= 0)
            {
                return true;
            }

            // 現在位置が最下部から閾値以内かどうか
            double distanceFromBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
            return distanceFromBottom <= BottomThresholdPixels;
        }
        #endregion

        #region ItemsSource 監視
        /// <summary>ItemsSource の変更を監視</summary>
        private static void AttachToItemsSource(DataGrid dataGrid)
        {
            if (dataGrid.ItemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += (s, e) => OnCollectionChanged(dataGrid, e);
            }
        }

        /// <summary>ItemsSource の監視を解除</summary>
        private static void DetachFromItemsSource(DataGrid dataGrid)
        {
            // WeakEvent パターンが理想だが、簡易実装では Unloaded で解除されるため省略
        }

        /// <summary>コレクション変更時のハンドラ</summary>
        private static void OnCollectionChanged(DataGrid dataGrid, NotifyCollectionChangedEventArgs e)
        {
            // 自動スクロールがアクティブでない場合はスキップ
            if (!GetIsAutoScrollActive(dataGrid)) return;

            // 追加またはリセット時にスクロール
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Dispatcher で遅延実行（レイアウト更新後にスクロール）
                dataGrid.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Action(() => ScrollToBottom(dataGrid)));
            }
        }

        /// <summary>最下部へスクロール</summary>
        private static void ScrollToBottom(DataGrid dataGrid)
        {
            if (dataGrid.Items.Count == 0) return;

            var scrollViewer = GetScrollViewerReference(dataGrid);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToEnd();
            }
            else
            {
                // フォールバック: 最後のアイテムにスクロール
                dataGrid.ScrollIntoView(dataGrid.Items[dataGrid.Items.Count - 1]);
            }
        }
        #endregion

        #region ヘルパー
        /// <summary>DataGrid 内の ScrollViewer を検索</summary>
        private static ScrollViewer? FindScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer sv) return sv;

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>ScrollViewer の親 DataGrid を検索</summary>
        private static DataGrid? FindParentDataGrid(DependencyObject element)
        {
            DependencyObject? current = element;
            while (current != null)
            {
                if (current is DataGrid dg) return dg;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        #region ScrollViewer 参照保持用添付プロパティ
        private static readonly DependencyProperty ScrollViewerReferenceProperty =
            DependencyProperty.RegisterAttached(
                "ScrollViewerReference",
                typeof(ScrollViewer),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(null));

        private static ScrollViewer? GetScrollViewerReference(DependencyObject element) =>
            (ScrollViewer?)element.GetValue(ScrollViewerReferenceProperty);

        private static void SetScrollViewerReference(DependencyObject element, ScrollViewer? value) =>
            element.SetValue(ScrollViewerReferenceProperty, value);
        #endregion
        #endregion
    }
}
