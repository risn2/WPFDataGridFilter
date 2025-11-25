using System.Windows;
using System.Windows.Controls;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// テキストフィルター列のヘッダー表示用コントロール
    /// </summary>
    public partial class FilterTextColumnHeader : UserControl
    {
        public FilterTextColumnHeader()
        {
            InitializeComponent();
        }

        #region 依存関係プロパティ
        /// <summary>ヘッダーに表示する文字列</summary>
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        /// <summary>ヘッダー文字列用の依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(FilterTextColumnHeader), new PropertyMetadata(string.Empty));

        /// <summary>フィルターキー</summary>
        public string? FilterKey
        {
            get => (string?)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        /// <summary>FilterKey 用依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterKeyProperty = DependencyProperty.Register(
            nameof(FilterKey), typeof(string), typeof(FilterTextColumnHeader), new PropertyMetadata(null));

        /// <summary>フィルター文字列（DataGrid 側の ViewModel プロパティとバインドされる）</summary>
        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        /// <summary>フィルター文字列用の依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
            nameof(FilterText), typeof(string), typeof(FilterTextColumnHeader), new PropertyMetadata(string.Empty));

        /// <summary>ヘッダー文字列配置位置</summary>
        public Dock HeaderDock
        {
            get => (Dock)GetValue(HeaderDockProperty);
            set => SetValue(HeaderDockProperty, value);
        }

        /// <summary>HeaderDock 用の依存関係プロパティ</summary>
        public static readonly DependencyProperty HeaderDockProperty = DependencyProperty.Register(
            nameof(HeaderDock), typeof(Dock), typeof(FilterTextColumnHeader), new PropertyMetadata(Dock.Left));

        /// <summary>フィルター入力配置位置</summary>
        public Dock FilterDock
        {
            get => (Dock)GetValue(FilterDockProperty);
            set => SetValue(FilterDockProperty, value);
        }

        /// <summary>FilterDock 用の依存関係プロパティ</summary>
        public static readonly DependencyProperty FilterDockProperty = DependencyProperty.Register(
            nameof(FilterDock), typeof(Dock), typeof(FilterTextColumnHeader), new PropertyMetadata(Dock.Right));
        #endregion 依存関係プロパティ

        internal HeaderFilterTextBox FilterTextBox => PART_FilterBox;
    }
}
