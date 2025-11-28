# WPFDataGridFilter メモリ最適化設計

最終更新: 2025-11-28  
ブランチ: `feature/memory-optimization-design`

## 1. 背景と目的

### 1.1 現状の課題

現在の `FilterableDataGrid` 実装では、以下の理由により大量データ（10,000行以上）を扱う際にメモリとパフォーマンスの問題が発生する可能性があります:

1. **全データをメモリに保持**
   - `ObservableCollection<LogEntry>` がすべてのログエントリをメモリ上に保持
   - 各 `LogEntry` インスタンスが複数のプロパティ（文字列、DateTime?、byte[]など）を持つため、メモリ使用量が増大

2. **フィルター処理の全件走査**
   - `ICollectionView.Filter` は毎回全アイテムを走査してフィルタリング
   - フィルター変更のたびに `Refresh()` が実行され、全件の再評価が発生
   - 正規表現マッチング、プロパティアクセス、DateTime パースなど、行ごとのコストが蓄積

3. **DistinctValues の全件走査**
   - 列フィルターのコンテキストメニュー（選択フィルター）で重複排除リストを生成
   - `GetDistinctValues()` が全件を走査して HashSet を構築

4. **UI の仮想化制限**
   - DataGrid は UI 仮想化をサポートするが、データ自体はすべてメモリ上に存在
   - フィルター後のコレクションビューも全アイテムを保持

### 1.2 目的

- 10,000行以上のログデータを扱う際のメモリ使用量を削減
- フィルター処理のパフォーマンスを向上
- ユーザー体験を維持しつつ、スケーラビリティを改善

---

## 2. メモリ最適化戦略

### 2.1 仮想化データソース（Virtualization）

#### 概要

全データをメモリに保持するのではなく、必要な部分だけをメモリにロードする仮想化アプローチを採用します。

#### 実装アプローチ

##### A. データページング（Data Paging）

```csharp
public class VirtualizedLogCollection : IList, INotifyCollectionChanged
{
    private readonly ILogDataProvider dataProvider;
    private readonly int pageSize = 100;
    private readonly Dictionary<int, LogEntry[]> pageCache = new();
    private int totalCount;

    public object this[int index]
    {
        get
        {
            int pageIndex = index / pageSize;
            if (!pageCache.TryGetValue(pageIndex, out var page))
            {
                page = dataProvider.LoadPage(pageIndex, pageSize);
                pageCache[pageIndex] = page;
                
                // LRU キャッシュ: 古いページを削除
                if (pageCache.Count > 10) // 最大10ページまでキャッシュ
                {
                    var oldestPage = pageCache.Keys.Min();
                    pageCache.Remove(oldestPage);
                }
            }
            return page[index % pageSize];
        }
    }
}
```

**メリット:**

- メモリ使用量: 100行 × 10ページ = 最大1,000行のみメモリ保持
- スクロール時に必要な部分だけロード
- フィルター未適用時の高速表示

**デメリット:**

- データプロバイダーの実装が必要（ファイル、DB、メモリストアなど）
- DataGrid のスクロール挙動との統合が複雑
- フィルター適用時は別戦略が必要

---

##### B. 遅延ロード（Lazy Loading）

```csharp
public class LazyLogEntry
{
    private readonly ILogDataProvider provider;
    private readonly int index;
    private LogEntry? cached;

    public string Time => EnsureLoaded().Time;
    public string Source => EnsureLoaded().Source;
    // ...

    private LogEntry EnsureLoaded()
    {
        if (cached == null)
        {
            cached = provider.LoadSingleEntry(index);
        }
        return cached;
    }
}
```

**メリット:**

- プロパティアクセス時のみデータをロード
- 表示されていない行はメモリに保持しない

**デメリット:**

- フィルター処理で全件アクセスが必要な場合、効果が限定的
- プロバイダー呼び出しのオーバーヘッド

---

### 2.2 フィルター処理の最適化

#### A. インデックス構築（Indexing）

大量データの場合、フィルター対象の列にインデックスを構築します。

```csharp
public class IndexedLogCollection
{
    private readonly List<LogEntry> allEntries;
    private readonly Dictionary<string, Dictionary<string, List<int>>> indices = new();

    public void BuildIndex(string propertyName)
    {
        var index = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        
        for (int i = 0; i < allEntries.Count; i++)
        {
            var value = GetPropertyValue(allEntries[i], propertyName);
            var normalized = NormalizeValue(value);
            
            if (!index.ContainsKey(normalized))
            {
                index[normalized] = new List<int>();
            }
            index[normalized].Add(i);
        }
        
        indices[propertyName] = index;
    }

    public IEnumerable<LogEntry> FilterBySelection(string propertyName, HashSet<string> allowedValues)
    {
        if (!indices.TryGetValue(propertyName, out var index))
        {
            // フォールバック: 全件走査
            return _allEntries.Where(e => allowedValues.Contains(GetPropertyValue(e, propertyName)));
        }

        var matchedIndices = new HashSet<int>();
        foreach (var value in allowedValues)
        {
            if (index.TryGetValue(value, out var indices))
            {
                foreach (var idx in indices)
                {
                    matchedIndices.Add(idx);
                }
            }
        }

        return matchedIndices.Select(i => _allEntries[i]);
    }
}
```

**メリット:**

- 選択フィルターの高速化（O(n) → O(1) × 選択数）
- 特に高頻度フィルター操作で効果的

**デメリット:**

- インデックス構築のコスト（初回のみ）
- インデックス保持のメモリオーバーヘッド
- データ更新時のインデックス再構築が必要

---

#### B. 並列フィルター処理（Parallel Filtering）

```csharp
private bool Filter(object item)
{
    // 軽量な判定を先に実行（Early Exit）
    if (item is null) return false;
    
    // 選択フィルター（インデックス利用可能）
    if (!ApplySelectionFilters(item)) return false;
    
    // 日時フィルター（比較的軽量）
    if (!ApplyTimeFilter(item)) return false;
    
    // テキストフィルター（正規表現、最もコスト高）
    if (!ApplyTextFilters(item)) return false;
    
    return true;
}

// 大量データの場合は並列処理
private void RefreshFilterParallel()
{
    if (ItemsSource is IList<LogEntry> source && source.Count > 5000)
    {
        var filtered = source.AsParallel()
            .Where(item => Filter(item))
            .ToList();
        
        // CollectionView を更新
        ApplyFilteredResults(filtered);
    }
    else
    {
        // 通常の Refresh
        ItemsView?.Refresh();
    }
}
```

**メリット:**

- マルチコアCPUを活用してフィルター処理を高速化
- 5,000行以上で効果的

**デメリット:**

- 並列処理のオーバーヘッド（少量データでは逆効果）
- UI スレッドのブロックに注意（async/await と組み合わせ）

---

#### C. 段階的フィルタリング（Deferred Refresh）

```csharp
private DispatcherTimer? debounceTimer;

public void ScheduleFilterRefresh()
{
    if (debounceTimer == null)
    {
        debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        debounceTimer.Tick += (s, e) =>
        {
            debounceTimer.Stop();
            RefreshFilter();
        };
    }

    debounceTimer.Stop();
    debounceTimer.Start();
}
```

**デバウンス間隔 300ms の根拠:**

| 観点 | 根拠 |
|------|------|
| タイピング間隔 | 一般的なタイピング速度（40-80 WPM）では文字間隔が 150-300ms |
| 知覚遅延 | 人間が「即座」と感じる応答時間は < 100ms、「待ち」と感じ始めるのは > 1000ms |
| 業界標準 | lodash, RxJS, VS Code 検索など多くのライブラリで 200-400ms を採用 |
| 処理コスト | 10,000行データでフィルター処理 ~100ms → 300ms + 100ms = 400ms で結果表示（許容範囲） |

**適応的デバウンス（推奨拡張）:**

```csharp
public class AdaptiveDebounceSettings
{
    public int NormalDelayMs { get; set; } = 200;      // 通常時（業界標準下限）
    public int HighLoadDelayMs { get; set; } = 400;    // 高負荷時（業界標準上限）
    public int IdleDelayMs { get; set; } = 100;        // アイドル時
    public int HighLoadThreshold { get; set; } = 50;   // 高負荷判定閾値（件/秒）
}
```

**デバウンスとスロットリングの分離（リアルタイムログ追加対応）:**

ユーザー入力とコレクション変更で異なる戦略を適用：

| イベント | 戦略 | 間隔 | 理由 |
|---------|------|------|------|
| ユーザー入力 | デバウンス | 200-400ms | 入力完了を待つ |
| コレクション変更 | スロットリング | 500ms | 高頻度更新を抑制 |

```csharp
// コレクション変更用スロットリング
private DateTime lastCollectionFilterRefresh;
private const int MinCollectionRefreshIntervalMs = 500;

private void OnCollectionChanged()
{
    var elapsed = DateTime.Now - lastCollectionFilterRefresh;
    if (elapsed.TotalMilliseconds >= MinCollectionRefreshIntervalMs)
    {
        RefreshFilter();
        lastCollectionFilterRefresh = DateTime.Now;
    }
    // 間隔内の変更は次のサイクルで自動的に反映
}
```

**スロットリング 500ms の根拠:**

| 観点 | 評価 |
|------|------|
| UI保護 | 2回/秒のフィルター更新で十分な負荷軽減 |
| 遅延許容 | 最悪600ms（500ms + 処理100ms）は「待ち」1000ms未満 |
| CPU負荷 | フィルター100ms × 2回 = 200ms/秒（20%以下） |
| バッファ処理との整合 | 50ms × 10回 = 500ms で切りが良い |

| スロットリング値 | フィルター頻度 | 最悪遅延 | 評価 |
|-----------------|--------------|---------|------|
| 300ms | 3.3回/秒 | 400ms | ⚠️ CPU負荷やや高い |
| **500ms** | **2回/秒** | **600ms** | **✅ バランス良好** |
| 750ms | 1.3回/秒 | 850ms | ⚠️ 遅延が目立つ |
| 1000ms | 1回/秒 | 1100ms | ❌ 「待ち」と感じる |

**メリット:**

- ユーザーの入力中に過剰なフィルター処理を防止
- UI の応答性向上

**デメリット:**

- リアルタイム性が若干低下
- タイマー管理の複雑さ

---

### 2.3 データ構造の最適化

#### A. LogEntry の軽量化

```csharp
public class CompactLogEntry
{
    // string の代わりに ReadOnlyMemory<char> や Span<T> を検討
    private readonly string[] fields; // 配列で保持（プロパティごとのオーバーヘッド削減）
    
    public string Time => fields[0];
    public string IFNum => fields[1];
    public string Source => fields[2];
    // ...
    
    // DateTime のキャッシュ（遅延評価）
    private DateTime? timeStamp;
    public DateTime? TimeStamp
    {
        get
        {
            if (!timeStamp.HasValue && !string.IsNullOrWhiteSpace(Time))
            {
                if (TryParseDateTime(Time, out var parsed))
                {
                    timeStamp = parsed;
                }
            }
            return timeStamp;
        }
    }
}
```

**メリット:**

- メモリレイアウトの最適化
- オブジェクトヘッダーのオーバーヘッド削減

**デメリット:**

- コードの可読性低下
- バインディングの複雑化

---

#### B. String Interning

```csharp
public class StringPool
{
    private readonly ConcurrentDictionary<string, string> pool = new();

    public string Intern(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return pool.GetOrAdd(value, v => v);
    }
}

// LogEntry 生成時
var entry = new LogEntry
{
    Source = _stringPool.Intern(sourceValue), // 重複する文字列を共有
    Destination = _stringPool.Intern(destValue)
};
```

**メリット:**

- 同じ値（例: "SRC0", "DST1"など）が多数存在する場合、メモリを大幅削減
- ログデータは重複が多いため効果的

**デメリット:**

- StringPool 自体のメモリ
- 並行アクセス時のロック競合

---

### 2.4 UI 仮想化の強化

#### A. DataGrid の VirtualizingPanel 設定

```xml
<local:FilterableDataGrid
    VirtualizingPanel.IsVirtualizing="True"
    VirtualizingPanel.VirtualizationMode="Recycling"
    VirtualizingPanel.CacheLength="10,10"
    VirtualizingPanel.CacheLengthUnit="Page"
    EnableRowVirtualization="True"
    EnableColumnVirtualization="True">
```

**メリット:**

- UI要素の再利用でメモリ削減
- スクロール性能向上

**デメリット:**

- 複雑なセルテンプレートでは効果が限定的

---

## 3. 推奨実装プラン

### フェーズ1: クイックウィン（短期）

1. **段階的フィルタリング（Debounce）**
   - 実装コスト: 低
   - 効果: 中
   - 既存コードへの影響: 小

2. **String Interning**
   - 実装コスト: 低
   - 効果: 中〜高（データ特性による）
   - 既存コードへの影響: 小

3. **並列フィルター処理**
   - 実装コスト: 中
   - 効果: 高（大量データ時）
   - 既存コードへの影響: 中

4. **UI 仮想化の最適化**
   - 実装コスト: 低
   - 効果: 中
   - 既存コードへの影響: 小

### フェーズ1.5: 選択フィルターの最適化（短期追加）

1. **DistinctValues の上限制限** ✅ 実装済み
   - 実装コスト: 低
   - 効果: 高（Time, Data列など繰り返しのないデータで顕著）
   - 既存コードへの影響: 小
   - 詳細: 候補数が100件を超える列では選択フィルターを無効化し、テキストフィルター使用を促す

```csharp
// GetDistinctValues で上限チェック
if (values.Count > MaxDistinctValuesForSelection)
{
    return null; // 選択フィルター無効化
}

// PopulateFilterMenu でフォールバック
if (distinctValues is null)
{
    menu.Items.Add(CreateDisabledMenuItem("(候補が多すぎるため選択フィルター無効)"));
    menu.Items.Add(CreateDisabledMenuItem("テキストフィルターを使用してください"));
}
```

**メリット:**

- 10,000件のユニーク値を持つ列でメニュー生成が即座に完了
- UI描画のブロッキングを防止
- ユーザーに適切なフィルター方法を案内

**デメリット:**

- 選択フィルターが使えない列が発生
- 上限値（100件）の調整が必要な場合あり

### フェーズ2: 構造改善（中期）

1. **インデックス構築** ✅ 実装済み
   - 実装コスト: 中〜高
   - 効果: 高（選択フィルター）
   - 既存コードへの影響: 中
   - 詳細: PropertyIndex クラスを作成し、IFNum, Source, Destination, Event 列でインデックスを構築

```csharp
// PropertyIndex.cs - プロパティ値からアイテムインデックスへのマッピング
public class PropertyIndex
{
    private Dictionary<string, Dictionary<string, List<int>>> indices = new();
    
    // インデックス構築（ItemsSource 変更時に呼び出し）
    public void BuildIndex(IList source, string propertyName) { ... }
    
    // インデックスから重複なし値を取得（O(1)）
    public IEnumerable<string>? GetDistinctValuesFromIndex(string propertyName) { ... }
}

// FilterableDataGrid での使用
private static readonly HashSet<string> IndexableProperties = 
    new() { "IFNum", "Source", "Destination", "Event" };

private void BuildPropertyIndices()
{
    foreach (var prop in IndexableProperties)
        propertyIndex.BuildIndex(ItemsSource as IList, prop);
}
```

**メリット:**

- GetDistinctValues が O(n) → O(1) に高速化
- FilterMetrics でキャッシュヒット/ミスを追跡可能
- 選択フィルターメニュー生成が即座に完了

1. **LogEntry の軽量化（TimeStamp キャッシュ）** ✅ 実装済み
   - 実装コスト: 中
   - 効果: 中
   - 既存コードへの影響: 小（バインディング変更不要）
   - 詳細: TimeStamp プロパティを遅延評価+キャッシュ化

```csharp
// LogEntry.cs - TimeStamp の遅延評価とキャッシュ
private bool timeStampCached;
private DateTime timeStampValue;

public DateTime TimeStamp
{
    get
    {
        if (!timeStampCached)
        {
            timeStampValue = ParseTimeStamp(Time);
            timeStampCached = true;
        }
        return timeStampValue;
    }
}
```

**メリット:**

- 繰り返しフィルター時のパース処理を回避
- DateTime.TryParseExact の呼び出し回数を大幅削減

### フェーズ3: アーキテクチャ変更（長期）

1. **仮想化データソース**
   - 実装コスト: 高
   - 効果: 非常に高（100,000行以上）
   - 既存コードへの影響: 大（データプロバイダー実装必要）

---

## 4. 性能目標

| データ量 | 現状（推定） | 目標 |
|---------|------------|------|
| 1,000行 | フィルター: 50ms / メモリ: 5MB | フィルター: 30ms / メモリ: 3MB |
| 10,000行 | フィルター: 500ms / メモリ: 50MB | フィルター: 100ms / メモリ: 20MB |
| 100,000行 | フィルター: 5000ms / メモリ: 500MB | フィルター: 500ms / メモリ: 100MB |

---

## 5. リスクと制約

### リスク

1. **互換性の破壊**
   - 既存のデータバインディングやカスタマイズに影響
   - 緩和策: 段階的な移行、フィーチャーフラグの使用

2. **複雑性の増加**
   - 仮想化やインデックス管理でコードが複雑化
   - 緩和策: 適切な抽象化、ドキュメント整備

3. **予期しないバグ**
   - 並列処理やキャッシュでの競合状態
   - 緩和策: 十分なテスト、段階的なロールアウト

### 制約

1. **WPF DataGrid の制約**
   - ICollectionView との統合が前提
   - カスタム仮想化は実装が複雑

2. **UI スレッドの制約**
   - フィルター処理は UI スレッドで実行
   - 長時間処理は応答性を損なう

---

## 6. 測定とモニタリング

### 実装すべきメトリクス

```csharp
public class FilterMetrics
{
    public int TotalItems { get; set; }
    public int FilteredItems { get; set; }
    public TimeSpan FilterDuration { get; set; }
    public long MemoryUsageMB { get; set; }
    public int CacheHitCount { get; set; }
    public int CacheMissCount { get; set; }
}
```

### モニタリングポイント

- フィルター処理時間
- メモリ使用量（GC.GetTotalMemory）
- キャッシュヒット率
- UI 応答性（フレームレート）

---

## 7. 次のステップ

1. **プロトタイプ実装**
   - フェーズ1の施策をブランチで実装
   - 10,000行、50,000行、100,000行のテストデータで性能測定

2. **ベンチマーク作成**
   - BenchmarkDotNet を使用した定量評価
   - メモリプロファイラー（dotMemory など）での分析

3. **ユーザーフィードバック**
   - プロトタイプを実環境で試用
   - パフォーマンスとユーザビリティのバランス確認

4. **本実装とマージ**
   - 効果が確認できた施策から順次マージ
   - ドキュメントとサンプル更新

---

## 8. 参考資料

- [WPF Performance Best Practices - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-data-binding)
- [Virtualizing ItemsControl - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)
- [Memory Management in .NET](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [Parallel LINQ (PLINQ)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/introduction-to-plinq)

---

## 変更履歴

- 2025-11-28: フェーズ2（インデックス構築・LogEntry軽量化）を実装
- 2025-11-28: フェーズ1.5（DistinctValues上限制限）を追加・実装
- 2025-11-28: 初版作成（メモリ最適化の設計検討）
