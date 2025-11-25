using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// 時刻用のテキストフィルターと日時範囲フィルターを備えた DataGridTextColumn 派生クラス
    /// </summary>
    public class FilterableTimeColumn : DataGridTextColumn
    {
        #region フィールド
        /// <summary>ヘッダー表示用コントロール保持</summary>
        private FilterTimeColumnHeader? headerControl;
        #endregion // フィールド

        #region プロパティ
        /// <summary>ヘッダーに表示する固定文字列</summary>
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        /// <summary>HeaderText 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata(string.Empty, OnHeaderCandidateChanged));

        /// <summary>ヘッダー文字列バインディング先パス</summary>
        public string? HeaderTextPath
        {
            get => (string?)GetValue(HeaderTextPathProperty);
            set => SetValue(HeaderTextPathProperty, value);
        }

        /// <summary>HeaderTextPath 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextPathProperty = DependencyProperty.Register(
            nameof(HeaderTextPath), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata(null, OnHeaderCandidateChanged));

        /// <summary>FilterableDataGrid.FilterTexts で利用するキー</summary>
        public string FilterKey
        {
            get => (string)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        /// <summary>FilterKey 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterKeyProperty = DependencyProperty.Register(
            nameof(FilterKey), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata("Time", OnFilterKeyChanged));

        /// <summary>日時範囲の開始バインディングパス</summary>
        public string RangeFromPath
        {
            get => (string)GetValue(RangeFromPathProperty);
            set => SetValue(RangeFromPathProperty, value);
        }

        /// <summary>RangeFromPath 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeFromPathProperty = DependencyProperty.Register(
            nameof(RangeFromPath), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata("TimeFrom", OnHeaderCandidateChanged));

        /// <summary>日時範囲の終了バインディングパス</summary>
        public string RangeToPath
        {
            get => (string)GetValue(RangeToPathProperty);
            set => SetValue(RangeToPathProperty, value);
        }

        /// <summary>RangeToPath 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeToPathProperty = DependencyProperty.Register(
            nameof(RangeToPath), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata("TimeTo", OnHeaderCandidateChanged));

        /// <summary>日時範囲クリアコマンドのバインディングパス</summary>
        public string RangeClearCommandPath
        {
            get => (string)GetValue(RangeClearCommandPathProperty);
            set => SetValue(RangeClearCommandPathProperty, value);
        }

        /// <summary>RangeClearCommandPath 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeClearCommandPathProperty = DependencyProperty.Register(
            nameof(RangeClearCommandPath), typeof(string), typeof(FilterableTimeColumn), new PropertyMetadata("ClearTimeRangeCommand", OnHeaderCandidateChanged));
        #endregion // プロパティ

        #region コンストラクター
        /// <summary>
        /// FilterableTimeColumn インスタンス生成
        /// </summary>
        public FilterableTimeColumn()
        {
            EnsureHeader();
        }
        #endregion // コンストラクター

        #region メソッド
        /// <summary>
        /// ヘッダー候補変更時のヘッダー再構築
        /// </summary>
        private static void OnHeaderCandidateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((FilterableTimeColumn)dependencyObject).EnsureHeader();
        }

        /// <summary>
        /// フィルターキー変更時のヘッダー再構築
        /// </summary>
        private static void OnFilterKeyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((FilterableTimeColumn)dependencyObject).EnsureHeader();
        }

        /// <summary>
        /// ヘッダーコントロール準備
        /// </summary>
        private void EnsureHeader()
        {
            if (headerControl == null)
            {
                headerControl = new FilterTimeColumnHeader();
                var dataContextBinding = new Binding("DataContext")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
                };
                BindingOperations.SetBinding(headerControl, FrameworkElement.DataContextProperty, dataContextBinding);
            }

            ApplyHeaderBindings(headerControl);
            Header = headerControl;
        }

        /// <summary>
        /// ヘッダー用バインディング適用
        /// </summary>
        private void ApplyHeaderBindings(FilterTimeColumnHeader header)
        {
            if (!string.IsNullOrWhiteSpace(HeaderTextPath))
            {
                var headerBinding = new Binding(HeaderTextPath)
                {
                    Mode = BindingMode.OneWay
                };
                BindingOperations.SetBinding(header, FilterTimeColumnHeader.HeaderTextProperty, headerBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header, FilterTimeColumnHeader.HeaderTextProperty);
                header.HeaderText = HeaderText;
            }

            header.FilterKey = FilterKey;

            if (!string.IsNullOrWhiteSpace(FilterKey))
            {
                var filterBinding = new Binding($"FilterTexts[{FilterKey}]")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FilterableDataGrid), 1),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                BindingOperations.SetBinding(header.FilterTextBox, HeaderFilterTextBox.FilterTextProperty, filterBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header.FilterTextBox, HeaderFilterTextBox.FilterTextProperty);
                header.FilterText = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(RangeFromPath))
            {
                var fromBinding = new Binding(RangeFromPath)
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FilterableDataGrid), 1),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                BindingOperations.SetBinding(header.DateRangeFilter, HeaderDateRangeFilter.FromProperty, fromBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header.DateRangeFilter, HeaderDateRangeFilter.FromProperty);
                header.RangeFrom = null;
            }

            if (!string.IsNullOrWhiteSpace(RangeToPath))
            {
                var toBinding = new Binding(RangeToPath)
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FilterableDataGrid), 1),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                BindingOperations.SetBinding(header.DateRangeFilter, HeaderDateRangeFilter.ToProperty, toBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header.DateRangeFilter, HeaderDateRangeFilter.ToProperty);
                header.RangeTo = null;
            }

            if (!string.IsNullOrWhiteSpace(RangeClearCommandPath))
            {
                var clearBinding = new Binding(RangeClearCommandPath)
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FilterableDataGrid), 1),
                    Mode = BindingMode.OneWay
                };
                BindingOperations.SetBinding(header.DateRangeFilter, HeaderDateRangeFilter.ClearCommandProperty, clearBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header.DateRangeFilter, HeaderDateRangeFilter.ClearCommandProperty);
                header.RangeClearCommand = null;
            }
        }
        #endregion // メソッド
    }
}
