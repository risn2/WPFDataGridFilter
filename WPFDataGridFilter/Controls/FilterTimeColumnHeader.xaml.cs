using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// テキストフィルターと日時範囲フィルターを組み合わせた列ヘッダーコントロール
    /// </summary>
    public partial class FilterTimeColumnHeader : UserControl
    {
        public FilterTimeColumnHeader()
        {
            InitializeComponent();
        }

        #region 依存関係プロパティ
        /// <summary>ヘッダー表示文字列</summary>
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        /// <summary>HeaderText 用依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(FilterTimeColumnHeader), new PropertyMetadata(string.Empty));

        /// <summary>テキストフィルター入力値</summary>
        public string? FilterText
        {
            get => (string?)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        /// <summary>FilterText 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
            nameof(FilterText), typeof(string), typeof(FilterTimeColumnHeader), new PropertyMetadata(string.Empty));

        /// <summary>フィルターキー</summary>
        public string? FilterKey
        {
            get => (string?)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        /// <summary>FilterKey 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterKeyProperty = DependencyProperty.Register(
            nameof(FilterKey), typeof(string), typeof(FilterTimeColumnHeader), new PropertyMetadata(null));

        /// <summary>日時範囲フィルターの開始時刻</summary>
        public DateTime? RangeFrom
        {
            get => (DateTime?)GetValue(RangeFromProperty);
            set => SetValue(RangeFromProperty, value);
        }

        /// <summary>RangeFrom 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeFromProperty = DependencyProperty.Register(
            nameof(RangeFrom), typeof(DateTime?), typeof(FilterTimeColumnHeader), new PropertyMetadata(null));

        /// <summary>日時範囲フィルターの終了時刻</summary>
        public DateTime? RangeTo
        {
            get => (DateTime?)GetValue(RangeToProperty);
            set => SetValue(RangeToProperty, value);
        }

        /// <summary>RangeTo 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeToProperty = DependencyProperty.Register(
            nameof(RangeTo), typeof(DateTime?), typeof(FilterTimeColumnHeader), new PropertyMetadata(null));

        /// <summary>日時範囲クリアコマンド</summary>
        public ICommand? RangeClearCommand
        {
            get => (ICommand?)GetValue(RangeClearCommandProperty);
            set => SetValue(RangeClearCommandProperty, value);
        }

        /// <summary>RangeClearCommand 用依存関係プロパティ</summary>
        public static readonly DependencyProperty RangeClearCommandProperty = DependencyProperty.Register(
            nameof(RangeClearCommand), typeof(ICommand), typeof(FilterTimeColumnHeader), new PropertyMetadata(null));
        #endregion 依存関係プロパティ

        internal HeaderFilterTextBox FilterTextBox => PART_FilterBox;

        internal HeaderDateRangeFilter DateRangeFilter => PART_DateRangeFilter;
    }
}
