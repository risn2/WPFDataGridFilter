using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFDataGridFilter.ViewModels;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// DataGrid ヘッダーで使用する日時範囲フィルター用コントロール。
    /// From/To と時刻文字列（HH:mm:ss）を合成し、ViewModel の DateTime? に反映します。
    /// </summary>
    public partial class HeaderDateRangeFilter : UserControl
    {
        // UserControlはView専用にし、親のViewModelとつなぐために依存関係プロパティ（DP）とRoutedEvent/ICommandを公開する。
        // 画面（Window/Page）側のViewModelが状態（例: FilterText, From/To）を持ち、UserControlはDP経由でBindingする。
        // 見た目やフォーカス制御など純Viewの都合はコードビハインドで最小限に扱う。

        #region 依存関係プロパティ フィールド
        /// <summary>IsFilterd プロパティ（読み取り専用）の DependencyPropertyKey</summary>
        private static readonly DependencyPropertyKey IsFilterdPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsFilterd), typeof(bool), typeof(HeaderDateRangeFilter), new PropertyMetadata(false));

        /// <summary>IsFilterd プロパティ（読み取り専用）の DependencyProperty</summary>
        public static readonly DependencyProperty IsFilterdProperty = IsFilterdPropertyKey.DependencyProperty;

        /// <summary>
        /// From プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(DateTime?), typeof(HeaderDateRangeFilter), new PropertyMetadata(null, OnDateChanged));

        /// <summary>
        /// To プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(DateTime?), typeof(HeaderDateRangeFilter), new PropertyMetadata(null, OnDateChanged));

        /// <summary>
        /// ClearCommand プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty ClearCommandProperty =
            DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(HeaderDateRangeFilter), new PropertyMetadata(null));

        /// <summary>
        /// ToggleCommand プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty ToggleCommandProperty =
            DependencyProperty.Register(nameof(ToggleCommand), typeof(ICommand), typeof(HeaderDateRangeFilter), new PropertyMetadata(null));

        /// <summary>
        /// ClosePopupCommand プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty ClosePopupCommandProperty =
            DependencyProperty.Register(nameof(ClosePopupCommand), typeof(ICommand), typeof(HeaderDateRangeFilter), new PropertyMetadata(null));

        /// <summary>
        /// IsOpen プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(HeaderDateRangeFilter), new PropertyMetadata(false));

        /// <summary>
        /// FromTimeText プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty FromTimeTextProperty =
            DependencyProperty.Register(nameof(FromTimeText), typeof(string), typeof(HeaderDateRangeFilter), new PropertyMetadata("00:00:00", OnTimePartChanged));

        /// <summary>
        /// ToTimeText プロパティの DependencyProperty
        /// </summary>
        public static readonly DependencyProperty ToTimeTextProperty =
            DependencyProperty.Register(nameof(ToTimeText), typeof(string), typeof(HeaderDateRangeFilter), new PropertyMetadata("00:00:00", OnTimePartChanged));
        #endregion // 依存関係プロパティ フィールド

        #region プロパティ
        /// <summary>
        /// フィルターが有効/無効（From または To のどちらかが設定されている）
        /// </summary>
        public bool IsFilterd
        {
            get => (bool)GetValue(IsFilterdProperty);
            private set => SetValue(IsFilterdPropertyKey, value);
        }

        /// <summary>
        /// 期間の開始日時。DatePicker の日付と FromTimeText を合成して設定
        /// </summary>
        public DateTime? From
        {
            get => (DateTime?)GetValue(FromProperty);
            set { SetValue(FromProperty, value); UpdateComposed(); }
        }

        /// <summary>
        /// 期間の終了日時。DatePicker の日付と ToTimeText を合成して設定
        /// </summary>
        public DateTime? To
        {
            get => (DateTime?)GetValue(ToProperty);
            set { SetValue(ToProperty, value); UpdateComposed(); }
        }

        /// <summary>フィルターのクリアコマンド</summary>
        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        /// <summary>ポップアップの開閉コマンド</summary>
        public ICommand ToggleCommand
        {
            get => (ICommand)GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        /// <summary>ポップアップを閉じるコマンド</summary>
        public ICommand ClosePopupCommand
        {
            get => (ICommand)GetValue(ClosePopupCommandProperty);
            set => SetValue(ClosePopupCommandProperty, value);
        }

        /// <summary>ポップアップの開閉状態</summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>開始側の時刻文字列（HH:mm:ss）</summary>
        public string FromTimeText
        {
            get => (string)GetValue(FromTimeTextProperty);
            set => SetValue(FromTimeTextProperty, value);
        }

        /// <summary>終了側の時刻文字列（HH:mm:ss）</summary>
        public string ToTimeText
        {
            get => (string)GetValue(ToTimeTextProperty);
            set => SetValue(ToTimeTextProperty, value);
        }
        #endregion // プロパティ

        #region メソッド
        #region コンストラクタ
        /// <summary>
        /// コントロールのコンストラクタ
        /// </summary>
        public HeaderDateRangeFilter()
        {
            InitializeComponent();

            ClearCommand = new RelayCommand(_ =>
            {
                // クリア時は当日と既定の時刻へ戻す
                From = null;
                To = null;
                FromTimeText = "00:00:00";
                ToTimeText = "00:00:00";
            });

            ToggleCommand = new RelayCommand(_ => IsOpen = !IsOpen);
            ClosePopupCommand = new RelayCommand(_ => IsOpen = false);
        }
        #endregion // コンストラクタ

        #region イベントハンドラー
        /// <summary>
        /// 時刻テキストボックスで Enter 押下時にバインディングを確定
        /// </summary>
        private void TimeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                var be = tb.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();
                e.Handled = true;
            }
        }

        /// <summary>
        /// DatePicker の日付選択変更
        /// </summary>
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker dp)
            {
                var be = dp.GetBindingExpression(DatePicker.SelectedDateProperty);
                be?.UpdateSource();
                UpdateComposed();
            }
        }

        /// <summary>
        /// From/To の日付変更を反映
        /// </summary>
        private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderDateRangeFilter c)
            {
                c.UpdateComposed();
            }
        }

        /// <summary>
        /// 時刻文字列の変更をFrom/Toへ反映
        /// </summary>
        private static void OnTimePartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderDateRangeFilter c)
            {
                c.UpdateComposed();
            }
        }
        #endregion // イベントハンドラー

        /// <summary>
        /// DatePickerで選択された日付に、時刻文字列（HH:mm:ss）を合成して From/To を更新
        /// </summary>
        private void UpdateComposed()
        {
            if (From.HasValue && TimeSpan.TryParse(FromTimeText, out var t1))
            {
                var d1 = From.Value.Date + t1;
                if (!Equals(From, d1))
                    SetValue(FromProperty, d1);
            }
            if (To.HasValue && TimeSpan.TryParse(ToTimeText, out var t2))
            {
                var d2 = To.Value.Date + t2;
                if (!Equals(To, d2))
                    SetValue(ToProperty, d2);
            }
            UpdateIsFilterd();
        }

        /// <summary>
        /// Filter有効無効をFrom/To の設定状態から更新
        /// </summary>
        private void UpdateIsFilterd() => IsFilterd = (From.HasValue || To.HasValue);

        #endregion // メソッド
    }
}
