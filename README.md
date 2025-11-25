# WPFDataGridFilter

.NET 8 / WPF 環境で `DataGrid` ヘッダーにフィルター UI を組み込んだサンプルです。カラムごとのテキスト検索に加えて、複数値のチェックボックス選択や日時範囲の指定、アクティブ状態の視覚化など、業務アプリで必要になる操作感をミニマムなコードで再現しています。

## 主な機能

- **カスタム列コントロール**: `FilterableTextColumn` / `FilterableTimeColumn` がヘッダー UI（`HeaderFilterTextBox`、`HeaderDateRangeFilter`）を自動配置し、ViewModel の `FilterableDataGrid.FilterTexts` / `FilterSelections` へバインド。
- **テキストフィルター**: ヘッダーボタン→テキストボックスへトグルし、`×` ボタンや `Esc` でクリア。空文字のままフォーカスが抜けると自動で折りたたみ。
- **選択フィルター**: フィルターボタンのコンテキストメニューにチェックボックスリストを表示。全選択 / 全解除、スクロール対応、残件ゼロ時はメッセージ表示。
- **アイコン切り替え**: `Resource/filter_24.png` → `Resource/filtered_24.png` へ自動切り替え。テキスト・選択・日時のいずれかが有効ならフィルター済みアイコンを表示。
- **日時範囲フィルター**: 時刻列ヘッダーのポップアップで From/To 日付＋時刻を指定。`FilterableDataGrid.TimeFrom/TimeTo` にバインドし、`ClearTimeRangeCommand` で即時リセット。
- **グローバルクリア**: `FilterableDataGrid.ClearAllFiltersCommand` をメイン画面の「全フィルター解除」ボタンにバインド。フィルターが無効な間はボタンも自動的に無効化。
- **ICollectionView.Filter 実装**: テキストは Regex、選択は HashSet、日時は範囲判定で絞り込み。件数と処理時間 (ms) をステータスバーに表示。

より詳細な仕様は `docs/specification.md` を参照してください。

## プロジェクト構成

```text
WPFDataGridFilter/
  Controls/
    FilterableDataGrid.cs           # フィルタ辞書、コマンド、Filter ロジック
    FilterableTextColumn.cs         # テキスト列ヘッダーの自動生成
    FilterableTimeColumn.cs         # 時刻列＋範囲フィルター
    HeaderFilterTextBox.xaml(.cs)   # テキスト入力＋チェックボックスメニュー
    HeaderDateRangeFilter.xaml(.cs) # From/To 日付＋時刻ポップアップ
  Models/LogEntry.cs
  ViewModels/
    MainViewModel.cs, RelayCommand.cs
  Resource/
    filter_24.png / filtered_24.png
  MainWindow.xaml(.cs)
```

## ビルド & 実行

VS Code の F5（.vscode/launch.json）でそのまま起動できます。PowerShell から手動で動かす場合は次の通りです。

```powershell
dotnet build .\WPFDataGridFilter\WPFDataGridFilter.csproj
dotnet run --project .\WPFDataGridFilter\WPFDataGridFilter.csproj
```

## 画面操作のポイント

1. 各列右端のフィルターアイコンをクリックするとテキスト入力へ展開。文字列を入力し `Enter` またはフォーカスアウトで確定します。
2. テキストを空にすると折りたたまれ、フィルターも解除。`×` ボタンまたは `Esc` でも同様にリセットできます。
3. コンテキストメニューからチェックボックスで値を選択／除外。ヘッダーアイコンの変化でフィルター状態を確認可能。
4. 時刻列の「⏱ フィルタ」トグルでポップアップを表示し、From/To 日付と時刻を設定。クリアボタンで即時解除。
5. 画面上部の「全フィルター解除」は、何らかのフィルターが有効なときのみ押下でき、テキスト・選択・日時すべてをまとめてリセットします。

## カスタマイズ例

- `supportedTimeFormats` にフォーマットを追加して独自ログ形式へ対応。
- `FilterableDataGrid.Match` の Regex オプションを変更して大文字小文字の扱いを調整。
- 大規模データセットではテキスト入力にディレイを設けたり、`CollectionView.DeferRefresh()` でバッチ更新する実装を追加。

## ライセンス

このリポジトリはサンプル用途です。個々のプロジェクトポリシーに従って利用してください。
