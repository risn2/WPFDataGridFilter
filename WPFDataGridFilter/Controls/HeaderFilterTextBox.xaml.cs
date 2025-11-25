using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WPFDataGridFilter.ViewModels;

namespace WPFDataGridFilter.Controls
{
    /// <summary>
    /// DataGrid ヘッダー用のテキストフィルターコントロール
    /// トグルで入力欄を開閉し、×で文字列をクリア可能
    /// </summary>
    public partial class HeaderFilterTextBox : UserControl
    {

        #region 依存関係プロパティ
        /// <summary>
        /// <see cref="FilterText"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty FilterTextProperty =
            DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(HeaderFilterTextBox), new PropertyMetadata(null, OnFilterTextChanged));

        /// <summary>フィルタ文字列</summary>
        public string? FilterText
        {
            get => (string?)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        /// <summary>
        /// <see cref="ClearCommand"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty ClearCommandProperty =
                DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(HeaderFilterTextBox), new PropertyMetadata(null));

        /// <summary>クリアコマンド</summary>
        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        /// <summary>
        /// <see cref="IsExpanded"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty IsExpandedProperty =
                DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(HeaderFilterTextBox), new PropertyMetadata(false));

        /// <summary>入力欄の展開状態</summary>
        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        /// <summary>
        /// <see cref="ToggleCommand"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty ToggleCommandProperty =
            DependencyProperty.Register(nameof(ToggleCommand), typeof(ICommand), typeof(HeaderFilterTextBox), new PropertyMetadata(null));

        /// <summary>展開/折りたたみトグルコマンド</summary>
        public ICommand ToggleCommand
        {
            get => (ICommand)GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        /// <summary>
        /// <see cref="FilterKey"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty FilterKeyProperty =
            DependencyProperty.Register(nameof(FilterKey), typeof(string), typeof(HeaderFilterTextBox), new PropertyMetadata(null, OnFilterKeyChanged));

        /// <summary>フィルター対象プロパティキー</summary>
        public string? FilterKey
        {
            get => (string?)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        /// <summary>
        /// <see cref="IsFilterActive"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty IsFilterActiveProperty =
            DependencyProperty.Register(nameof(IsFilterActive), typeof(bool), typeof(HeaderFilterTextBox), new PropertyMetadata(false));

        /// <summary>フィルター有効状態</summary>
        public bool IsFilterActive
        {
            get => (bool)GetValue(IsFilterActiveProperty);
            private set => SetValue(IsFilterActiveProperty, value);
        }

        /// <summary>
        /// <see cref="ExternalFilterActive"/> の依存関係プロパティ定義
        /// </summary>
        public static readonly DependencyProperty ExternalFilterActiveProperty =
            DependencyProperty.Register(nameof(ExternalFilterActive), typeof(bool), typeof(HeaderFilterTextBox), new PropertyMetadata(false, OnExternalFilterActiveChanged));

        /// <summary>外部コントロールによるフィルター状態</summary>
        public bool ExternalFilterActive
        {
            get => (bool)GetValue(ExternalFilterActiveProperty);
            set => SetValue(ExternalFilterActiveProperty, value);
        }
        #endregion // 依存関係プロパティ

        #region メソッド
        //---------------------------------------------------------
        // メソッド
        //---------------------------------------------------------

        #region コンストラクタ
        /// <summary>
        /// コントロールの初期化
        /// </summary>
        public HeaderFilterTextBox()
        {
            InitializeComponent();
            ClearCommand = new RelayCommand(_ =>
            {
                var input = AcquireInputBox();
                if (input is not null) input.Text = string.Empty;
                var binding = input?.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                FilterText = string.Empty;
                // クリア直後にフォーカスをテキストボックスへ戻す
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var tb = AcquireInputBox();
                    tb?.Focus();
                    tb?.SelectAll();
                }));
                UpdateFilterActiveState();
            });
            ToggleCommand = new RelayCommand(_ =>
            {
                IsExpanded = !IsExpanded;
                if (IsExpanded)
                {
                    // 展開時にテキストボックスへフォーカス
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var tb = AcquireInputBox();
                        tb?.Focus();
                        tb?.SelectAll();
                    }));
                }
            });

            Loaded += HeaderFilterTextBox_Loaded;
            Unloaded += HeaderFilterTextBox_Unloaded;
        }
        #endregion // コンストラクタ

        #region イベントハンドラー
        /// <summary>
        /// 入力ボックスからフォーカスが外れたとき、文字列が空なら折りたたむ
        /// </summary>
        private void InputBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Textが空のままコントロールからフォーカスが外れたらボタン表示へ戻す
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                IsExpanded = false;
            }
        }

        /// <summary>
        /// 入力ボックスでEnterが押されたとき、バインディングを更新し、空なら折りたたむ
        /// </summary>
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter でソース更新（UpdateSourceTrigger=LostFocus のため手動更新）
                if (sender is TextBox tb)
                {
                    var be = tb.GetBindingExpression(TextBox.TextProperty);
                    be?.UpdateSource();
                }
                // 空なら折りたたみ
                if (string.IsNullOrWhiteSpace(FilterText))
                {
                    IsExpanded = false;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && sender is TextBox textBox)
            {
                // ESC で入力中断。フィルターは前回値に戻す
                if (sender is TextBox tb)
                {
                    var be = tb.GetBindingExpression(TextBox.TextProperty);
                    be?.UpdateTarget();
                    FilterText = tb.Text;
                }
                // 空なら折りたたみ
                if (string.IsNullOrWhiteSpace(FilterText))
                {
                    IsExpanded = false;
                }
                e.Handled = true;
            }
        }
        #endregion // イベント ハンドラー
        #endregion // メソッド

        #region ヘルパー
        /// <summary>メニューイベントを一度だけ接続するためのフラグ</summary>
        private bool menuAttached;

        /// <summary>コンテキストメニュー用の現在候補</summary>
        private readonly List<string> currentChoices = new();

        /// <summary>現在選択されている値集合</summary>
        private readonly HashSet<string> currentSelection = new(StringComparer.Ordinal);

        /// <summary>全件選択状態を示すフラグ</summary>
        private bool selectionIsAll = true;

        /// <summary>チェック状態更新時にイベントを抑制するフラグ</summary>
        private bool suppressSelectionHandlers;

        /// <summary>
        /// 入力テキストボックス取得
        /// </summary>
        /// <returns>テキストボックス</returns>
        private TextBox? AcquireInputBox() => InputBox ?? FindName("InputBox") as TextBox;

        /// <summary>
        /// フィルターメニュー表示設定
        /// </summary>
        private void AttachMenu()
        {
            if (menuAttached)
            {
                return;
            }

            if (FindName("FilterButton") is Button button && ContextMenu is ContextMenu menu)
            {
                button.Click += FilterButton_Click;
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.Opened += FilterMenu_Opened;
                menu.MaxHeight = 280; // 約10項目分の表示領域
                menu.MaxWidth = 300;
                ScrollViewer.SetCanContentScroll(menu, true);
                ScrollViewer.SetVerticalScrollBarVisibility(menu, ScrollBarVisibility.Auto);
                menuAttached = true;
            }
        }

        /// <summary>
        /// フィルターボタンクリックでコンテキストメニューを開く
        /// </summary>
        private void FilterButton_Click(object? sender, RoutedEventArgs e)
        {
            if (ContextMenu is not ContextMenu menu) return;

            try
            {
                menu.IsOpen = true;
            }
            catch (Exception ex)
            {
                menu.IsOpen = false;
                HandleMenuException("フィルターメニューの表示に失敗しました。", ex);
            }
        }

        /// <summary>
        /// メニュー表示時に項目を動的生成
        /// </summary>
        private void FilterMenu_Opened(object? sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                try
                {
                    PopulateFilterMenu(menu);
                }
                catch (Exception ex)
                {
                    menu.IsOpen = false;
                    HandleMenuException("フィルターメニューの作成に失敗しました。", ex);
                }
            }
        }

        /// <summary>
        /// コンテキストメニューの中身を構築
        /// </summary>
        private void PopulateFilterMenu(ContextMenu menu)
        {
            menu.Items.Clear();

            var filterKey = FilterKey;
            var grid = GetHostDataGrid();
            EnsureHostSubscriptions();
            if (grid is null || string.IsNullOrWhiteSpace(filterKey))
            {
                menu.Items.Add(CreateDisabledMenuItem("フィルター対象がありません"));
                return;
            }

            currentChoices.Clear();
            currentChoices.AddRange(grid.GetDistinctValues(filterKey).Select(NormalizeValue));

            currentSelection.Clear();
            selectionIsAll = true;

            if (grid.FilterSelections.TryGetValue(filterKey, out var stored))
            {
                foreach (var value in stored)
                {
                    currentSelection.Add(NormalizeValue(value));
                }

                if (currentSelection.Count == currentChoices.Count)
                {
                    selectionIsAll = true;
                    currentSelection.Clear();
                }
                else
                {
                    selectionIsAll = false;
                }
            }

            menu.Items.Add(CreateSimpleMenuItem("テキストフィルターを表示", _ => ExpandTextFilter()));
            menu.Items.Add(CreateSimpleMenuItem("テキストフィルターをクリア", _ => ExecuteClear()));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateSimpleMenuItem("すべて選択", _ => SelectAll(grid, filterKey)));
            menu.Items.Add(CreateSimpleMenuItem("すべて解除", _ => SelectNone(grid, filterKey)));
            menu.Items.Add(new Separator());

            if (currentChoices.Count == 0)
            {
                menu.Items.Add(CreateDisabledMenuItem("利用可能な値がありません"));
                return;
            }

            foreach (var choice in currentChoices)
            {
                var display = string.IsNullOrEmpty(choice) ? "(空)" : choice;
                var item = new MenuItem
                {
                    Header = display,
                    IsCheckable = true,
                    StaysOpenOnClick = true,
                    Tag = choice
                };

                item.IsChecked = selectionIsAll || currentSelection.Contains(choice);

                item.Checked += (_, __) => UpdateSelection(grid, filterKey, choice, true);
                item.Unchecked += (_, __) => UpdateSelection(grid, filterKey, choice, false);

                menu.Items.Add(item);
            }
        }

        /// <summary>
        /// テキストフィルターを展開
        /// </summary>
        private void ExpandTextFilter()
        {
            if (ContextMenu is ContextMenu menu)
            {
                menu.IsOpen = false;
            }

            IsExpanded = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var tb = AcquireInputBox();
                tb?.Focus();
                tb?.SelectAll();
            }));
        }

        /// <summary>
        /// テキストフィルターをクリア
        /// </summary>
        private void ExecuteClear()
        {
            if (ClearCommand?.CanExecute(null) == true)
            {
                ClearCommand.Execute(null);
            }
            UpdateFilterActiveState();
        }

        /// <summary>
        /// 全項目を選択状態にする
        /// </summary>
        private void SelectAll(FilterableDataGrid grid, string key)
        {
            selectionIsAll = true;
            currentSelection.Clear();
            CommitSelections(grid, key);
            RefreshMenuChecks(ContextMenu as ContextMenu);
            UpdateFilterActiveState();
        }

        /// <summary>
        /// 全項目を未選択状態にする
        /// </summary>
        private void SelectNone(FilterableDataGrid grid, string key)
        {
            selectionIsAll = false;
            currentSelection.Clear();
            CommitSelections(grid, key);
            RefreshMenuChecks(ContextMenu as ContextMenu);
            UpdateFilterActiveState();
        }

        /// <summary>
        /// 単一値の選択状態を更新
        /// </summary>
        private void UpdateSelection(FilterableDataGrid grid, string key, string choice, bool isChecked)
        {
            if (suppressSelectionHandlers)
            {
                return;
            }

            if (selectionIsAll)
            {
                selectionIsAll = false;
                currentSelection.Clear();

                if (!isChecked)
                {
                    foreach (var candidate in currentChoices)
                    {
                        if (!string.Equals(candidate, choice, StringComparison.Ordinal))
                        {
                            currentSelection.Add(candidate);
                        }
                    }
                }
                else
                {
                    foreach (var candidate in currentChoices)
                    {
                        currentSelection.Add(candidate);
                    }
                }
            }
            else
            {
                if (isChecked)
                {
                    currentSelection.Add(choice);
                }
                else
                {
                    currentSelection.Remove(choice);
                }
            }

            if (currentSelection.Count == currentChoices.Count)
            {
                selectionIsAll = true;
                currentSelection.Clear();
            }

            CommitSelections(grid, key);
            RefreshMenuChecks(ContextMenu as ContextMenu);
            UpdateFilterActiveState();
        }

        /// <summary>
        /// グリッドへ選択状態を反映
        /// </summary>
        private void CommitSelections(FilterableDataGrid grid, string key)
        {
            if (selectionIsAll)
            {
                grid.FilterSelections.Clear(key);
            }
            else
            {
                grid.FilterSelections.SetSelections(key, currentSelection);
            }
        }

        /// <summary>
        /// メニュー項目のチェック状態を更新
        /// </summary>
        private void RefreshMenuChecks(ContextMenu? menu)
        {
            if (menu is null)
            {
                return;
            }

            suppressSelectionHandlers = true;
            try
            {
                foreach (var item in menu.Items.OfType<MenuItem>())
                {
                    if (item.Tag is string choice)
                    {
                        item.IsChecked = selectionIsAll || currentSelection.Contains(choice);
                    }
                }
            }
            finally
            {
                suppressSelectionHandlers = false;
            }
        }

        /// <summary>
        /// コンテキストメニューで使用する無効項目生成
        /// </summary>
        private static MenuItem CreateDisabledMenuItem(string header)
        {
            return new MenuItem
            {
                Header = header,
                IsEnabled = false
            };
        }

        /// <summary>
        /// 単純なクリックメニュー項目生成
        /// </summary>
        private MenuItem CreateSimpleMenuItem(string header, Action<MenuItem> action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (sender, _) =>
            {
                action((MenuItem)sender!);
            };
            return item;
        }

        /// <summary>
        /// フィルター対象の DataGrid を探索
        /// </summary>
        private FilterableDataGrid? GetHostDataGrid() => FindAncestor<FilterableDataGrid>(this);

        /// <summary>
        /// 親要素探索
        /// </summary>
        private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                if (current is FrameworkElement fe)
                {
                    if (fe.Parent is DependencyObject frameworkParent)
                    {
                        current = frameworkParent;
                        continue;
                    }

                    if (fe.TemplatedParent is DependencyObject templateParent)
                    {
                        current = templateParent;
                        continue;
                    }
                }
                else if (current is FrameworkContentElement fce && fce.Parent is DependencyObject contentParent)
                {
                    current = contentParent;
                    continue;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        /// <summary>
        /// null 許容の文字列を正規化
        /// </summary>
        private static string NormalizeValue(string? value) => value ?? string.Empty;

        /// <summary>
        /// メニュー操作で発生した例外をユーザーへ通知
        /// </summary>
        private static void HandleMenuException(string message, Exception exception)
        {
            Debug.WriteLine($"[HeaderFilterTextBox] {message} {exception}");
            MessageBox.Show(
                $"{message}\n{exception.Message}",
                "フィルターメニュー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>ホスト DataGrid 参照</summary>
        private FilterableDataGrid? hostGrid;

        /// <summary>
        /// 読み込み完了時処理
        /// </summary>
        private void HeaderFilterTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            AttachMenu();
            EnsureHostSubscriptions();
            UpdateFilterActiveState();
        }

        /// <summary>
        /// アンロード時処理
        /// </summary>
        private void HeaderFilterTextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachHostSubscriptions();
        }

        /// <summary>
        /// ホスト DataGrid への購読確立
        /// </summary>
        private void EnsureHostSubscriptions()
        {
            var grid = GetHostDataGrid();
            if (grid is null)
            {
                return;
            }

            if (ReferenceEquals(hostGrid, grid))
            {
                return;
            }

            DetachHostSubscriptions();

            hostGrid = grid;
            hostGrid.FilterSelections.CollectionChanged += FilterSelections_CollectionChanged;
            UpdateFilterActiveState();
        }

        /// <summary>
        /// ホスト DataGrid から購読解除
        /// </summary>
        private void DetachHostSubscriptions()
        {
            if (hostGrid is null)
            {
                return;
            }

            hostGrid.FilterSelections.CollectionChanged -= FilterSelections_CollectionChanged;
            hostGrid = null;
        }

        /// <summary>
        /// 選択フィルター変化時のアイコン更新
        /// </summary>
        private void FilterSelections_CollectionChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateFilterActiveState));
        }

        /// <summary>
        /// フィルター有効状態を更新
        /// </summary>
        private void UpdateFilterActiveState()
        {
            var textActive = !string.IsNullOrWhiteSpace(FilterText);
            var selectionActive = HasSelectionFilter();
            var externalActive = ExternalFilterActive;
            IsFilterActive = textActive || selectionActive || externalActive;
        }

        /// <summary>
        /// 選択フィルターの有無を確認
        /// </summary>
        private bool HasSelectionFilter()
        {
            if (hostGrid is null || string.IsNullOrWhiteSpace(FilterKey))
            {
                return false;
            }

            return hostGrid.FilterSelections.ContainsKey(FilterKey);
        }

        /// <summary>
        /// フィルター文字列変更時処理
        /// </summary>
        private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderFilterTextBox control)
            {
                control.UpdateFilterActiveState();
            }
        }

        /// <summary>
        /// フィルターキー変更時処理
        /// </summary>
        private static void OnFilterKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderFilterTextBox control)
            {
                control.EnsureHostSubscriptions();
                control.UpdateFilterActiveState();
            }
        }

        /// <summary>
        /// 外部フィルター状態変更時処理
        /// </summary>
        private static void OnExternalFilterActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderFilterTextBox control)
            {
                control.UpdateFilterActiveState();
            }
        }
        #endregion // ヘルパー
    }
}
