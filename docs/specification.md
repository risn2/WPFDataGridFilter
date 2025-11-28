# WPFDataGridFilter Specification

最終更新: 2025-11-28

## 1. 概要

- **目的**: DataGrid の列ヘッダーに統合されたフィルター UI を提供し、ユーザーがテキスト検索・値選択・日時範囲を素早く切り替えられるようにする。
- **対象**: .NET 8 / WPF アプリケーション。UI は DataGrid の列定義を `FilterableTextColumn` / `FilterableTimeColumn` へ置き換えるだけで利用可能。
- **主要コンポーネント**:
  - `FilterableDataGrid`: フィルター辞書・選択集合・日時範囲・コマンドを保持し、`ICollectionView.Filter` を実装。
  - `HeaderFilterTextBox`: テキスト入力・トグルボタン・チェックボックス式コンテキストメニューを提供。
  - `HeaderDateRangeFilter`: From/To 日時ポップアップで範囲指定を行う。

## 2. UI/UX 要件

| 項目 | 仕様 |
| --- | --- |
| テキストフィルター展開 | ヘッダー右側のアイコンボタンをクリックでテキストボックスへトグル。空文字のままフォーカスが外れたら再びアイコン表示。 |
| 入力操作 | `Enter` でバインディング更新、`Esc` で前回値に戻す。`×` ボタンで即時クリアし、フォーカスと選択状態をテキストボックスへ戻す。 |
| コンテキストメニュー | `FilterButton` の `ContextMenu` にチェックボックスリストを表示。`すべて選択/解除`、スクロール上限 280px、利用可能値が無い場合は案内文を表示。 |
| アイコン表示 | `filter_24.png`（未適用）/ `filtered_24.png`（適用中）を `IsFilterActive` トリガーで切替。テキスト・選択・日時いずれかが有効ならアクティブ扱い。 |
| 日時フィルター | `HeaderDateRangeFilter` のポップアップで DatePicker + HH:mm:ss テキストを扱い、`TimeFrom` / `TimeTo` を更新。クリアボタンで両方 `null` に戻す。 |
| グローバルクリア | 画面上部「全フィルター解除」ボタンは `HasActiveFilters` が `true` の間だけ有効。押下でテキスト・選択・日時をすべて初期化。 |
| ステータスバー | フィルターの総件数 (`TotalCount`)、表示件数 (`FilteredCount`)、処理時間 (`FilterElapsedMs`) を表示。 |

## 3. データ構造 & コマンド

- `FilterTextCollection`
  - `Dictionary<string, string?>` ベース。
  - `CollectionChanged` 発火で `FilterableDataGrid` が `RefreshFilter()` を実行。
  - `ClearAll()` により全キーを削除、各キーの `PropertyChanged` を通知。
- `FilterSelectionCollection`
  - `Dictionary<string, HashSet<string>>` ベース。選択済み値を正規化して保持。
  - `SetSelections`, `Clear`, `ContainsKey`, `TryGetValue`, `ClearAll()` を提供。
- 依存関係プロパティ
  - `FilterableDataGrid.TimeFrom / TimeTo`: DateTime?。`OnFilterPropertyChanged` で再フィルタ。
  - `HeaderFilterTextBox.FilterKey / FilterText / IsFilterActive / ExternalFilterActive` など。
- コマンド一覧
  - `FilterableDataGrid.ClearTimeRangeCommand`
  - `FilterableDataGrid.ClearAllFiltersCommand` (`CanExecute => HasActiveFilters`)
  - `HeaderFilterTextBox.ClearCommand`, `ToggleCommand`
  - `HeaderDateRangeFilter.ClearCommand`, `ToggleCommand`, `ClosePopupCommand`

## 4. フィルター判定ロジック

1. **日時フィルター**
   - `FilterTexts["Time"]` のテキスト判定。
   - 範囲指定: `TimeFrom <= 値 <= TimeTo`。
   - `TimeStamp` プロパティ → `DateTime?` 優先。無い場合は `Time` 文字列を `supportedTimeFormats` でパース。
2. **テキストフィルター**
   - Regex（IgnoreCase, Compiled）でマッチング。無効なパターンは `null` として扱いフィルターしない。
3. **選択フィルター**
   - `FilterSelections[key]` の集合に含まれるか判定。集合が空の場合は該当列すべて除外。
4. 3種類すべてを満たしたアイテムのみ通過。

## 5. 可視状態更新

- `RefreshFilter()` 実行後に `NotifyFilterStateChanged()` を呼び、`HasActiveFilters` の `PropertyChanged` と `CommandManager.InvalidateRequerySuggested()` を行う。
- `HeaderFilterTextBox` は `FilterSelections.CollectionChanged` を購読し、`IsFilterActive` を更新。

## 6. レイアウト & カスタマイズフック

- `FilterTextColumnHeader`
  - `HeaderDock` / `FilterDock` でタイトルとフィルターの上下左右を切替可能。
- `HeaderFilterTextBox`
  - コンテキストメニューは遅延生成。`MenuItem` が `IsCheckable` な場合のみチェック状態を同期。
  - 外部フィルター（例: 時刻の日時範囲）から状態を伝えるため `ExternalFilterActive` D.P. を公開。
- `FilterableTimeColumn`
  - `RangeFromPath`, `RangeToPath`, `RangeClearCommandPath` で別の ViewModel へバインド可能。

## 7. ビルド / 実行条件

- .NET 8 SDK 以上、`UseWPF` 有効。
- `Resource/filter_24.png`, `Resource/filtered_24.png` は `.csproj` の `<Resource>` として組み込み。
- 起動方法:

  ```powershell
  dotnet build .\WPFDataGridFilter\WPFDataGridFilter.csproj
  dotnet run   --project .\WPFDataGridFilter\WPFDataGridFilter.csproj
  ```

## 8. リアルタイムログ追加 & 自動スクロール

### 8.1 概要

高頻度でログが追加されるユースケース（100件/秒以上）において、UIの応答性を維持しつつ最新ログを表示するためのバッチ処理基盤を提供する。

### 8.2 データフロー

```text
Producer (ログ生成)
    ↓
LogBuffer<T> (ConcurrentQueue)
    ↓ 50ms間隔
LogBufferProcessor
    ↓
BatchingObservableCollection<T>
    ↓ Reset通知（1回のみ）
FilterableDataGrid
    ↓
自動スクロール
```

### 8.3 主要コンポーネント

| コンポーネント | 責務 |
| --- | --- |
| `LogBuffer<T>` | スレッドセーフなログバッファ。`ConcurrentQueue<T>` ベース。`Enqueue()` / `DequeueBatch()` を提供。 |
| `BatchingObservableCollection<T>` | バッチ追加で1回の `Reset` 通知に集約。`AddRange()` / `AddRangeWithLimit()` / `ReplaceAll()` を提供。 |
| `LogBufferProcessor` | 50ms間隔（`DispatcherTimer`）でバッファからUIコレクションへ反映。 |

### 8.4 ログ追加の動作仕様

| 項目 | 仕様 |
| --- | --- |
| 追加位置 | 新規ログは DataGrid の**末尾（下方）**に追加される。 |
| 順序保持 | バッファ内の順序を維持したまま UI コレクションへ反映。 |
| 最大件数 | `AddRangeWithLimit(items, maxCount)` で上限を超えた古いログを自動削除。 |
| バッチサイズ | `DequeueBatch(maxBatchSize)` で1回あたりの処理件数を制限（デフォルト500件）。 |

### 8.5 自動スクロール動作

| 状態 | 動作 |
| --- | --- |
| スクロールバーが最下部 | 新規ログ追加後、自動的に最新行（最下部）へスクロール。 |
| スクロールバーが途中 | ユーザーが過去ログを閲覧中と判断し、スクロール位置を維持。 |
| 閾値 | 最下部から10ピクセル以内を「最下部」と判定。 |

### 8.6 パフォーマンス特性

- **UIスレッドへの負荷軽減**: 個別の `Add` 通知ではなく `Reset` 通知1回に集約。
- **インデックス再構築**: `Reset` 通知時に `BuildPropertyIndices()` を実行し、選択フィルターの一貫性を維持。
- **String Interning**: `LogEntry.InternStrings()` により重複文字列のメモリ使用量を削減。
- **オブジェクトプール対応**: `LogEntry.Reset()` で既存オブジェクトを再利用可能。

### 8.7 使用例

```csharp
// バッファ経由でログを追加
_logBuffer.Enqueue(new LogEntry { ... });

// プロセッサが自動的にUIへ反映（50ms間隔）
// スクロールが最下部なら最新行へ追従
```

## 9. 既知の拡張アイデア

- 入力遅延（typeahead debounce）と `CollectionView.DeferRefresh()` を組み合わせたパフォーマンス最適化。
- DataGrid 以外（ListView など）へのフィルター適用や、サーバー側問い合わせへの転送。
- FilterSelection の状態をディスクやユーザー設定として保存する仕組み。
