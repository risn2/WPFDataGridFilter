using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// ヘッダーに HeaderFilterTextBox を自動配置する DataGridTextColumn 派生クラス
    /// </summary>
    public class FilterableTextColumn : DataGridTextColumn
    {
        #region フィールド
        /// <summary>ヘッダー表示用コントロール保持</summary>
        private FilterTextColumnHeader? headerControl;
        #endregion // フィールド

        #region プロパティ
        /// <summary>静的なヘッダー文字列参照</summary>
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        /// <summary>HeaderText 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(FilterableTextColumn), new PropertyMetadata(string.Empty, OnHeaderCandidateChanged));

        /// <summary>ヘッダー文字列用バインディングパス参照</summary>
        public string? HeaderTextPath
        {
            get => (string?)GetValue(HeaderTextPathProperty);
            set => SetValue(HeaderTextPathProperty, value);
        }

        /// <summary>HeaderTextPath 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextPathProperty = DependencyProperty.Register(
            nameof(HeaderTextPath), typeof(string), typeof(FilterableTextColumn), new PropertyMetadata(null, OnHeaderCandidateChanged));

        /// <summary>FilterableDataGrid.FilterTexts で利用するキー参照</summary>
        public string? FilterKey
        {
            get => (string?)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        /// <summary>FilterKey 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterKeyProperty = DependencyProperty.Register(
            nameof(FilterKey), typeof(string), typeof(FilterableTextColumn), new PropertyMetadata(null, OnFilterKeyChanged));

        /// <summary>ヘッダー文字列配置 Dock 参照</summary>
        public Dock HeaderDock
        {
            get => (Dock)GetValue(HeaderDockProperty);
            set => SetValue(HeaderDockProperty, value);
        }

        /// <summary>HeaderDock 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderDockProperty = DependencyProperty.Register(
            nameof(HeaderDock), typeof(Dock), typeof(FilterableTextColumn), new PropertyMetadata(Dock.Left, OnHeaderCandidateChanged));

        /// <summary>フィルター入力配置 Dock 参照</summary>
        public Dock FilterDock
        {
            get => (Dock)GetValue(FilterDockProperty);
            set => SetValue(FilterDockProperty, value);
        }

        /// <summary>FilterDock 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterDockProperty = DependencyProperty.Register(
            nameof(FilterDock), typeof(Dock), typeof(FilterableTextColumn), new PropertyMetadata(Dock.Right, OnHeaderCandidateChanged));
        #endregion // プロパティ

        #region メソッド
        /// <summary>
        /// FilterableTextColumn インスタンス生成
        /// </summary>
        public FilterableTextColumn()
        {
            EnsureHeader();
        }

        /// <summary>
        /// ヘッダー文字列候補変更時のヘッダー更新
        /// </summary>
        private static void OnHeaderCandidateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((FilterableTextColumn)dependencyObject).EnsureHeader();
        }

        /// <summary>
        /// フィルターキー変更時のヘッダー更新
        /// </summary>
        private static void OnFilterKeyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            ((FilterableTextColumn)dependencyObject).EnsureHeader();
        }

        /// <summary>
        /// ヘッダーコントロール準備
        /// </summary>
        private void EnsureHeader()
        {
            if (headerControl == null)
            {
                headerControl = new FilterTextColumnHeader();
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
        /// ヘッダーコントロールへのバインディング適用
        /// </summary>
        private void ApplyHeaderBindings(FilterTextColumnHeader header)
        {
            if (!string.IsNullOrWhiteSpace(HeaderTextPath))
            {
                var headerBinding = new Binding(HeaderTextPath)
                {
                    Mode = BindingMode.OneWay
                };
                BindingOperations.SetBinding(header, FilterTextColumnHeader.HeaderTextProperty, headerBinding);
            }
            else
            {
                BindingOperations.ClearBinding(header, FilterTextColumnHeader.HeaderTextProperty);
                header.HeaderText = HeaderText;
            }

            header.HeaderDock = HeaderDock;
            header.FilterDock = FilterDock;
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
        }
        #endregion // メソッド
    }
}
