# リアルタイムログ追加パフォーマンス設計

最終更新: 2025-11-28  
ブランチ: `feature/memory-optimization-design`

## 1. 要件と課題

### 1.1 要件

- **ログ追加速度**: 100件/秒
- **目標**: 追加遅延を最小化し、UIの応答性を維持
- **制約**: フィルター機能を維持しつつリアルタイム表示

### 1.2 現状の課題

現在の実装では、ログ追加時に以下の問題が発生する可能性があります：

| 問題 | 原因 | 影響 |
|------|------|------|
| UI フリーズ | 追加ごとに CollectionChanged 発火 | 100回/秒のUI更新 |
| フィルター遅延 | 300ms デバウンスによる待機 | 最新ログの表示遅延 |
| インデックス再構築 | 追加ごとのインデックス更新 | CPU負荷増大 |
| メモリ断片化 | 頻繁なオブジェクト生成 | GC 圧迫 |

### 1.3 パフォーマンス目標

| 指標 | 目標値 |
|------|--------|
| 追加遅延（UI反映まで） | < 50ms |
| UIフレームレート | 60fps維持 |
| CPU使用率増加 | < 10% |
| メモリ増加率 | < 1MB/分 |

---

## 2. 設計方針

### 2.1 バッチ処理アーキテクチャ

個別追加ではなく、一定間隔でバッチ処理することで UI 更新頻度を削減します。

```text
ログ生成 → バッファ蓄積 → バッチ追加 → UI更新
     ↓           ↓             ↓           ↓
 10件/100ms   キュー      50ms間隔    20回/秒
```

### 2.2 レイヤー分離

```text
┌─────────────────────────────────────────────────┐
│                   UI Layer                       │
│  FilterableDataGrid (表示のみ、更新は受動的)      │
└─────────────────────────────────────────────────┘
                        ↑ INotifyCollectionChanged
┌─────────────────────────────────────────────────┐
│              Collection Layer                    │
│  BatchingObservableCollection (バッチ通知)        │
└─────────────────────────────────────────────────┘
                        ↑ Add(item)
┌─────────────────────────────────────────────────┐
│               Buffer Layer                       │
│  LogBuffer (スレッドセーフ、バッチ抽出)           │
└─────────────────────────────────────────────────┘
                        ↑ Enqueue(item)
┌─────────────────────────────────────────────────┐
│              Producer Layer                      │
│  ログ生成元 (バックグラウンドスレッド)            │
└─────────────────────────────────────────────────┘
```

---

## 3. 詳細設計

### 3.1 LogBuffer（スレッドセーフバッファ）

```csharp
/// <summary>
/// スレッドセーフなログバッファ。
/// プロデューサーからのログを蓄積し、バッチで取り出す。
/// </summary>
public class LogBuffer<T>
{
    private readonly ConcurrentQueue<T> queue = new();
    private readonly int maxBatchSize;
    
    public LogBuffer(int maxBatchSize = 50)
    {
        this.maxBatchSize = maxBatchSize;
    }
    
    /// <summary>
    /// ログを追加（プロデューサースレッドから呼び出し）
    /// </summary>
    public void Enqueue(T item)
    {
        queue.Enqueue(item);
    }
    
    /// <summary>
    /// 複数ログを一括追加
    /// </summary>
    public void EnqueueRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            queue.Enqueue(item);
        }
    }
    
    /// <summary>
    /// 蓄積されたログをバッチで取り出し
    /// </summary>
    public List<T> DequeueBatch()
    {
        var batch = new List<T>(maxBatchSize);
        
        while (batch.Count < maxBatchSize && queue.TryDequeue(out var item))
        {
            batch.Add(item);
        }
        
        return batch;
    }
    
    /// <summary>
    /// バッファ内の件数
    /// </summary>
    public int Count => queue.Count;
    
    /// <summary>
    /// バッファが空か
    /// </summary>
    public bool IsEmpty => queue.IsEmpty;
}
```

**設計ポイント:**

- `ConcurrentQueue` でロックフリーな追加
- バッチサイズ上限で1回の処理量を制御
- プロデューサーをブロックしない

---

### 3.2 BatchingObservableCollection（バッチ通知コレクション）

```csharp
/// <summary>
/// バッチ追加時に1回の通知で複数アイテムを追加するコレクション。
/// UI更新回数を削減し、パフォーマンスを向上。
/// </summary>
public class BatchingObservableCollection<T> : ObservableCollection<T>
{
    private bool suppressNotification;
    
    /// <summary>
    /// 複数アイテムを一括追加（通知は1回のみ）
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) return;
        
        var itemList = items.ToList();
        if (itemList.Count == 0) return;
        
        suppressNotification = true;
        
        try
        {
            foreach (var item in itemList)
            {
                Items.Add(item);
            }
        }
        finally
        {
            suppressNotification = false;
        }
        
        // Reset 通知で一括更新（Add 通知より効率的）
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
    
    /// <summary>
    /// 最大件数を超えた古いアイテムを削除しつつ追加
    /// </summary>
    public void AddRangeWithLimit(IEnumerable<T> items, int maxCount)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return;
        
        suppressNotification = true;
        
        try
        {
            // 新規追加
            foreach (var item in itemList)
            {
                Items.Add(item);
            }
            
            // 古いアイテムを削除
            while (Items.Count > maxCount)
            {
                Items.RemoveAt(0);
            }
        }
        finally
        {
            suppressNotification = false;
        }
        
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
    
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
```

**設計ポイント:**

- `AddRange` で複数追加を1回の `Reset` 通知に集約
- 通知抑制フラグで中間状態の通知を防止
- `AddRangeWithLimit` でメモリ上限を制御

---

### 3.3 LogBufferProcessor（バッファ処理サービス）

```csharp
/// <summary>
/// バッファからログを定期的に取り出してUIに反映するサービス。
/// </summary>
public class LogBufferProcessor : IDisposable
{
    private readonly LogBuffer<LogEntry> buffer;
    private readonly BatchingObservableCollection<LogEntry> collection;
    private readonly DispatcherTimer processTimer;
    private readonly StringPool stringPool;
    
    // 設定パラメータ
    private readonly int processIntervalMs;
    private readonly int maxItemsInCollection;
    
    // メトリクス
    public int ProcessedBatchCount { get; private set; }
    public int TotalProcessedItems { get; private set; }
    public double AverageProcessingTimeMs { get; private set; }
    
    public LogBufferProcessor(
        LogBuffer<LogEntry> buffer,
        BatchingObservableCollection<LogEntry> collection,
        int processIntervalMs = 50,
        int maxItemsInCollection = 50000)
    {
        this.buffer = buffer;
        this.collection = collection;
        this.processIntervalMs = processIntervalMs;
        this.maxItemsInCollection = maxItemsInCollection;
        stringPool = StringPool.Shared;
        
        processTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(this.processIntervalMs)
        };
        processTimer.Tick += ProcessBuffer;
    }
    
    /// <summary>
    /// 処理開始
    /// </summary>
    public void Start()
    {
        processTimer.Start();
    }
    
    /// <summary>
    /// 処理停止
    /// </summary>
    public void Stop()
    {
        processTimer.Stop();
    }
    
    /// <summary>
    /// バッファ処理（タイマーイベント）
    /// </summary>
    private void ProcessBuffer(object? sender, EventArgs e)
    {
        if (buffer.IsEmpty) return;
        
        var sw = Stopwatch.StartNew();
        
        // バッチ取り出し
        var batch = buffer.DequeueBatch();
        
        if (batch.Count > 0)
        {
            // String Interning を適用
            foreach (var entry in batch)
            {
                entry.InternStrings(stringPool);
            }
            
            // コレクションに追加（上限付き）
            collection.AddRangeWithLimit(batch, maxItemsInCollection);
            
            // メトリクス更新
            ProcessedBatchCount++;
            TotalProcessedItems += batch.Count;
            
            sw.Stop();
            AverageProcessingTimeMs = 
                (AverageProcessingTimeMs * (ProcessedBatchCount - 1) + sw.Elapsed.TotalMilliseconds) 
                / ProcessedBatchCount;
        }
    }
    
    public void Dispose()
    {
        processTimer.Stop();
    }
}
```

**設計ポイント:**

- 50ms 間隔でバッファをポーリング（UI更新は最大20回/秒）
- `StringPool` で文字列メモリを最適化
- 上限件数で無限増加を防止
- メトリクスで処理状況を可視化

---

### 3.4 FilterableDataGrid の適応的デバウンス

高頻度追加時はフィルター更新頻度を自動調整します。

```csharp
/// <summary>
/// 適応的デバウンス設定
/// </summary>
public class AdaptiveDebounceSettings
{
    /// <summary>通常時のデバウンス間隔（ms）</summary>
    public int NormalDelayMs { get; set; } = 200;
    
    /// <summary>高頻度追加時のデバウンス間隔（ms）</summary>
    public int HighLoadDelayMs { get; set; } = 400;
    
    /// <summary>高頻度追加と判定する閾値（件/秒）</summary>
    public int HighLoadThreshold { get; set; } = 50;
    
    /// <summary>アイドル時のデバウンス間隔（ms）</summary>
    public int IdleDelayMs { get; set; } = 100;
}

// FilterableDataGrid 内での実装
private int recentAddCount;
private DateTime lastAddCountReset = DateTime.Now;

private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    if (e.Action == NotifyCollectionChangedAction.Reset)
    {
        // バッチ追加を検出
        recentAddCount += e.NewItems?.Count ?? 10; // Reset の場合は推定
    }
    
    UpdateCounts();
    ScheduleAdaptiveFilterRefresh();
}

private void ScheduleAdaptiveFilterRefresh()
{
    // 追加頻度を計算
    var elapsed = (DateTime.Now - lastAddCountReset).TotalSeconds;
    if (elapsed >= 1.0)
    {
        var addRate = recentAddCount / elapsed;
        recentAddCount = 0;
        lastAddCountReset = DateTime.Now;
        
        // 追加頻度に応じてデバウンス間隔を調整
        int delayMs;
        if (addRate > debounceSettings.HighLoadThreshold)
        {
            delayMs = debounceSettings.HighLoadDelayMs;
        }
        else if (addRate < 5)
        {
            delayMs = debounceSettings.IdleDelayMs;
        }
        else
        {
            delayMs = debounceSettings.NormalDelayMs;
        }
        
        if (debounceTimer != null)
        {
            debounceTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        }
    }
    
    ScheduleFilterRefresh();
}
```

**設計ポイント:**

- 追加頻度を監視し、デバウンス間隔を動的に調整
- 高負荷時はフィルター更新頻度を下げてUIを保護
- アイドル時は素早いレスポンスを維持

---

### 3.5 インデックスの増分更新

全再構築ではなく、追加分のみをインデックスに反映します。

```csharp
/// <summary>
/// 増分更新対応のプロパティインデックス
/// </summary>
public class IncrementalPropertyIndex
{
    private readonly Dictionary<string, Dictionary<string, List<int>>> indices = new();
    private readonly Dictionary<string, Func<object, object?>> accessors = new();
    private int indexedCount;
    
    /// <summary>
    /// 新規アイテムをインデックスに追加（増分更新）
    /// </summary>
    public void AddToIndex(IList source, string propertyName, int startIndex)
    {
        if (!indices.TryGetValue(propertyName, out var index))
        {
            // インデックスが存在しない場合は構築
            BuildIndex(source, propertyName);
            return;
        }
        
        var accessor = GetAccessor(source[0]?.GetType(), propertyName);
        if (accessor == null) return;
        
        // 新規アイテムのみ追加
        for (int i = startIndex; i < source.Count; i++)
        {
            var item = source[i];
            if (item == null) continue;
            
            var value = NormalizeValue(accessor(item)?.ToString());
            
            if (!index.TryGetValue(value, out var indices))
            {
                indices = new List<int>();
                index[value] = indices;
            }
            indices.Add(i);
        }
        
        indexedCount = source.Count;
    }
    
    /// <summary>
    /// バッチ追加後のインデックス更新
    /// </summary>
    public void UpdateIndicesAfterBatchAdd(IList source, IEnumerable<string> propertyNames, int addedCount)
    {
        int startIndex = source.Count - addedCount;
        
        foreach (var propertyName in propertyNames)
        {
            AddToIndex(source, propertyName, startIndex);
        }
    }
    
    /// <summary>
    /// インデックスのクリア（コレクションリセット時）
    /// </summary>
    public void ClearIndices()
    {
        indices.Clear();
        indexedCount = 0;
    }
    
    // ... 既存の GetDistinctValuesFromIndex, GetMatchingIndices は維持
}
```

**設計ポイント:**

- 追加分のみインデックス化（O(追加件数)）
- `startIndex` で増分範囲を指定
- Reset 時は全クリアして再構築

---

### 3.6 LogEntry の軽量生成

オブジェクト生成コストを削減します。

```csharp
/// <summary>
/// LogEntry のファクトリ（オブジェクトプール対応）
/// </summary>
public class LogEntryFactory
{
    private readonly ObjectPool<LogEntry> pool;
    private readonly StringPool stringPool;
    
    public LogEntryFactory(StringPool? stringPool = null)
    {
        this.stringPool = stringPool ?? StringPool.Shared;
        pool = new DefaultObjectPool<LogEntry>(new LogEntryPoolPolicy());
    }
    
    /// <summary>
    /// プールから LogEntry を取得して初期化
    /// </summary>
    public LogEntry Create(
        string time, string ifNum, string source, string destination,
        string @event, string data)
    {
        var entry = pool.Get();
        
        entry.Time = time;
        entry.IFNum = stringPool.Intern(ifNum);
        entry.Source = stringPool.Intern(source);
        entry.Destination = stringPool.Intern(destination);
        entry.Event = stringPool.Intern(@event);
        entry.Data = data; // Data は通常ユニークなので Intern しない
        
        return entry;
    }
    
    /// <summary>
    /// LogEntry をプールに返却
    /// </summary>
    public void Return(LogEntry entry)
    {
        entry.Reset(); // 状態をクリア
        pool.Return(entry);
    }
}

/// <summary>
/// LogEntry のプールポリシー
/// </summary>
public class LogEntryPoolPolicy : IPooledObjectPolicy<LogEntry>
{
    public LogEntry Create() => new LogEntry();
    
    public bool Return(LogEntry obj)
    {
        obj.Reset();
        return true;
    }
}

// LogEntry にリセットメソッドを追加
public partial class LogEntry
{
    /// <summary>
    /// 状態をリセット（プール返却用）
    /// </summary>
    public void Reset()
    {
        Time = string.Empty;
        IFNum = string.Empty;
        Source = string.Empty;
        Destination = string.Empty;
        Event = string.Empty;
        Data = null;
        timeStampCached = false;
        timeStampValue = default;
    }
    
    /// <summary>
    /// StringPool を使って文字列をインターン化
    /// </summary>
    public void InternStrings(StringPool pool)
    {
        IFNum = pool.Intern(IFNum);
        Source = pool.Intern(Source);
        Destination = pool.Intern(Destination);
        Event = pool.Intern(Event);
    }
}
```

**設計ポイント:**

- `ObjectPool` でアロケーションを削減
- `StringPool.Intern` で重複文字列を共有
- `Data` はユニークなためインターンしない（メモリリーク防止）

---

## 4. 統合アーキテクチャ

### 4.1 全体フロー

```text
┌─────────────────┐
│   ログ生成元     │  10件/100ms
│ (Background)    │
└────────┬────────┘
         │ Enqueue
         ▼
┌─────────────────┐
│   LogBuffer     │  ConcurrentQueue
│ (Thread-safe)   │  蓄積
└────────┬────────┘
         │ DequeueBatch (50ms間隔)
         ▼
┌─────────────────┐
│ LogBufferProcessor │
│ (UI Thread)     │  String Interning
└────────┬────────┘
         │ AddRangeWithLimit
         ▼
┌─────────────────┐
│ BatchingObservable │
│ Collection      │  1回の Reset 通知
└────────┬────────┘
         │ CollectionChanged
         ▼
┌─────────────────┐
│ FilterableDataGrid │
│                 │  適応的デバウンス
│ - PropertyIndex │  増分インデックス更新
│ - Filter        │
└─────────────────┘
```

### 4.2 タイミングチャート

```text
時間 (ms)    0    50   100  150  200  250  300  350  400  450  500
            |    |    |    |    |    |    |    |    |    |    |
ログ生成    ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
            10件 10件 10件 10件 10件 ...
            
バッファ    ────┬────┬────┬────┬────┬────┬────┬────┬────┬────
処理             ●    ●    ●    ●    ●    ●    ●    ●    ●
            50ms間隔でバッチ処理
            
UI更新      ────────────────────────────────────┬─────────────
                                               ●
                                          300ms後にフィルター更新
                                          (高負荷時は500ms)
```

### 4.3 性能予測

| シナリオ | 追加頻度 | バッファ処理 | UI更新 | 予想遅延 |
|---------|---------|-------------|--------|---------|
| 通常 | 10件/100ms | 5件/50ms | 3回/秒 | 50-100ms |
| バースト | 100件/100ms | 50件/50ms | 2回/秒 | 100-200ms |
| アイドル | 1件/1000ms | 1件/50ms | 10回/秒 | 50ms |

---

## 5. 実装優先順位

### Phase 1: 基盤（必須）

1. **LogBuffer** - スレッドセーフバッファ
2. **BatchingObservableCollection** - バッチ通知
3. **LogBufferProcessor** - 50ms間隔処理

### Phase 2: 最適化

1. **適応的デバウンス** - 負荷に応じた調整
2. **IncrementalPropertyIndex** - 増分インデックス

### Phase 3: 高度な最適化

1. **LogEntryFactory** - オブジェクトプール
2. **メモリ上限管理** - 古いログの自動削除

---

## 6. 設定パラメータ

| パラメータ | デフォルト | 説明 |
|-----------|-----------|------|
| `BufferProcessIntervalMs` | 50 | バッファ処理間隔 |
| `MaxBatchSize` | 50 | 1回のバッチ処理件数上限 |
| `MaxItemsInCollection` | 50,000 | コレクション内の最大件数 |
| `NormalDebounceMs` | 200 | 通常時フィルターデバウンス（業界標準下限） |
| `HighLoadDebounceMs` | 400 | 高負荷時フィルターデバウンス（業界標準上限） |
| `CollectionThrottleMs` | 500 | コレクション変更スロットリング |
| `HighLoadThreshold` | 50 | 高負荷判定閾値（件/秒） |

### 6.1 デバウンス値の根拠（200ms / 400ms）

| 観点 | 根拠 |
|------|------|
| タイピング間隔 | 一般的なタイピング速度（40-80 WPM）では文字間隔が 150-300ms |
| 知覚遅延 | 人間が「即座」と感じる応答時間は < 100ms、「待ち」と感じ始めるのは > 1000ms |
| 業界標準 | lodash, RxJS, VS Code 検索など多くのライブラリで 200-400ms を採用 |
| 処理コスト | 10,000行データでフィルター処理 ~100ms → 200ms + 100ms = 300ms で結果表示（許容範囲） |

### 6.2 スロットリング値の根拠（500ms）

| 観点 | 評価 |
|------|------|
| UI保護 | 2回/秒のフィルター更新で十分な負荷軽減 |
| 遅延許容 | 最悪600ms（500ms + 処理100ms）は「待ち」1000ms未満 |
| CPU負荷 | フィルター100ms × 2回 = 200ms/秒（20%以下） |
| バッファ処理との整合 | 50ms × 10回 = 500ms で切りが良い |

**代替案との比較:**

| スロットリング値 | フィルター頻度 | 最悪遅延 | 評価 |
|-----------------|--------------|---------|------|
| 300ms | 3.3回/秒 | 400ms | ⚠️ CPU負荷やや高い |
| **500ms** | **2回/秒** | **600ms** | **✅ バランス良好** |
| 750ms | 1.3回/秒 | 850ms | ⚠️ 遅延が目立つ |
| 1000ms | 1回/秒 | 1100ms | ❌ 「待ち」と感じる |

---

## 7. 監視メトリクス

```csharp
public class RealtimeLogMetrics
{
    // バッファ状態
    public int BufferQueueLength { get; set; }
    public int BufferPeakLength { get; set; }
    
    // 処理性能
    public double BatchProcessingTimeMs { get; set; }
    public int ProcessedItemsPerSecond { get; set; }
    
    // UI状態
    public int CurrentCollectionCount { get; set; }
    public double FilterRefreshTimeMs { get; set; }
    public int CurrentDebounceMs { get; set; }
    
    // メモリ
    public long MemoryUsageMB { get; set; }
    public int StringPoolSize { get; set; }
}
```

---

## 変更履歴

- 2025-11-28: 初版作成（リアルタイムログ追加のパフォーマンス設計）
