using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WPFDataGridFilter.Behaviors
{
    /// <summary>
    /// DataGrid の列ヘッダクリックで Asc -> Desc -> None の3状態で循環させるビヘイビア。
    /// </summary>
    public static class ThreeStateSortBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ThreeStateSortBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid) return;

            if ((bool)e.NewValue)
            {
                grid.Sorting += OnSorting;
            }
            else
            {
                grid.Sorting -= OnSorting;
            }
        }

        private static void OnSorting(object? sender, DataGridSortingEventArgs e)
        {
            if (sender is not DataGrid grid) return;

            // 既定のソートを抑止
            e.Handled = true;

            var column = e.Column;
            var direction = column.SortDirection;

            // 現在の方向に応じて次の状態を決定
            ListSortDirection? next;
            if (direction == null)
            {
                next = ListSortDirection.Ascending;
            }
            else if (direction == ListSortDirection.Ascending)
            {
                next = ListSortDirection.Descending;
            }
            else
            {
                next = null; // 3回目で解除
            }

            // コレクションビュー取得
            var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
            if (view == null) return;

            // 他列のSortDirectionをクリア（一般的な単一列ソート想定）
            foreach (var col in grid.Columns)
            {
                if (!ReferenceEquals(col, column)) col.SortDirection = null;
            }

            view.SortDescriptions.Clear();

            if (next is ListSortDirection dir)
            {
                // ソート適用
                column.SortDirection = dir;
                view.SortDescriptions.Add(new SortDescription(column.SortMemberPath, dir));
            }
            else
            {
                // 解除
                column.SortDirection = null;
            }

            // ビュー更新
            view.Refresh();
        }
    }
}
