---
applyTo: "**/*.cs,**/*.razor"
---
<!-- version: 2025-11-13-2 -->
# C# (.NET 8) Coding & Design Instructions

Copilot へ: このリポジトリで C# コード生成・修正・レビュー時は以下方針を強く優先する。  
（旧方針の「region 非使用」「private フィールドに '_' 接頭辞」は本ファイルで上書き）

## Target Framework

- 既定ターゲットは `net8.0`。テンプレート/提案コードは `<TargetFramework>net8.0</TargetFramework>` 前提。

## Regions（日本語名での構造化）

- クラス内要素を `#region フィールド`, `#region プロパティ`, `#region コンストラクタ`, `#region メソッド`, `#region 内部ヘルパー` 等でグルーピング。
- region 名は日本語。パスカル/キャメル不要。
- 過剰な入れ子は避け最上位カテゴリを明瞭化。
- 生成時は必須。短小クラスでも原則 region を付与。

## 命名規約

- private/protected フィールド: `camelCase` （先頭小文字・接頭辞 `_` 不使用）。
- public/protected/internal プロパティ: `PascalCase`.
- インターフェイス: 先頭 `I` + PascalCase.
- 非同期メソッド名末尾は動詞+目的語。`Async` サフィックスは公開 API のみ（例: `CalculateTotalAsync`）。
- DTO / Record は PascalCase、ファイル名一致。

## XML ドキュメンテーション（日本語 / 体現止め）

- すべての フィールド / プロパティ / メソッド / クラス / インターフェイス に `/// <summary>` を付与。
- サマリは日本語「体現止め」（名詞・連体形）で文末に「。」を付けない。
  例: `/// <summary>注文合計金額を計算する非同期メソッド</summary>`
- パラメータ説明は `/// <param name="order">注文エンティティ</param>` のように簡潔。
- 例外発生想定が明確なら `/// <exception cref="DomainValidationException">検証失敗</exception>`。

## コメントスタイル

- 行コメントも体現止め、句点「。」不要。
- 英語コメントを混在させず日本語で統一（外部 OSS 互換部分を除く）。
- 自明な代入・単純な LINQ はコメント不要。意図・理由に焦点。

## Nullability

- Nullable 有効 (`<Nullable>enable</Nullable>`) 前提。
- 引数 null チェックはガード句で即例外。`ArgumentNullException` を使用。

## Async / Await (.NET 8 / C# 12)

- 非同期 I/O 境界は `async`。同期ブロック回避 (例: `.Result`, `.Wait()` 禁止)。
- ライブラリ内部では `ConfigureAwait(false)`。
- `CancellationToken` は末尾。不要なら引数を省略したオーバーロードを別途提供。

## Exceptions

- 再スローは `throw;`。
- 業務ルール違反は意味的なカスタム例外。汎用 `Exception` 最小化。
- ログ出力時は機密情報を含めない。

## Patterns / Features (C# 12 / .NET 8)

- 必要に応じ Primary Constructor, Collection Expression (`[]`), `using` alias / any-type alias を活用。濫用は避ける。
- Data キャリアには `record` を優先（不変性表現）。
- インターフェイス デフォルト実装は慎重に利用し、テスト容易性を損なわない。

## LINQ / Collections

- ホットパス性能重視箇所はループ。非クリティカルは LINQ 可読性優先。
- 不変返却は `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey,TValue>`。

## Dependency Injection

- コンストラクタインジェクション標準。Service Locator 回避。
- 過剰依存 (>4 依存) は責務分割を提案。

## Logging

- `ILogger<T>` で構造化。`logger.LogInformation("注文計算開始 OrderId={OrderId}", order.Id);`
- 例外ログは `logger.LogError(ex, "注文計算失敗 OrderId={OrderId}", order.Id);`

## Configuration

- 設定は `IOptions<T>` / `IOptionsMonitor<T>`。グローバル静的キャッシュ禁止。

## Data / EF Core

- 非同期 API (`ToListAsync`, `FirstOrDefaultAsync`) を使用。
- N+1 回避 (Include / Select projection)。
- トランザクションは `await using` を活用。

## Serialization

- 標準は `System.Text.Json`。カスタムは `JsonConverter` 実装。
- 日本語ローカライズは表示層で行い、永続化は中立値。

## Performance

- 文字列結合頻発は `StringBuilder` / `string.Create`.
- Span/Memory はホットパス・パース用途限定。

## Testing

- AAA パターン。メソッド名: `MethodName_条件_期待結果`.
- 時刻・ランダムは抽象化インターフェイス注入でテスト容易性確保。

## Sample (.NET 8 / 規約反映)

```csharp
namespace MyApp.Orders;

/// <summary>金額を保持する値オブジェクト</summary>
public readonly record struct Money(decimal Amount);

/// <summary>注文アイテム</summary>
public sealed class OrderItem
{
    /// <summary>単価</summary>
    public decimal unitPrice { get; }

    /// <summary>数量</summary>
    public int quantity { get; }

    /// <summary>注文アイテムを表すコンストラクタ</summary>
    public OrderItem(decimal unitPrice, int quantity)
    {
        this.unitPrice = unitPrice;
        this.quantity = quantity;
    }
}

/// <summary>注文エンティティ</summary>
public sealed class Order
{
    #region フィールド
    /// <summary>アイテム一覧</summary>
    private readonly List<OrderItem> items = [];
    #endregion

    #region プロパティ
    /// <summary>注文識別子</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>注文アイテム列挙</summary>
    public IReadOnlyList<OrderItem> Items => items;
    #endregion

    #region メソッド
    /// <summary>アイテム追加</summary>
    public void AddItem(OrderItem item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        items.Add(item);
    }
    #endregion
}

/// <summary>注文合計計算サービスインターフェイス</summary>
public interface IOrderCalculator
{
    /// <summary>合計金額計算非同期処理</summary>
    Task<Money> CalculateAsync(Order order, CancellationToken cancellationToken);
}

/// <summary>注文合計計算サービス実装</summary>
public sealed class OrderCalculator : IOrderCalculator
{
    #region フィールド
    /// <summary>税計算サービス</summary>
    private readonly ITaxService taxService;
    /// <summary>割引ポリシサービス</summary>
    private readonly IDiscountPolicy discountPolicy;
    /// <summary>ロガー</summary>
    private readonly ILogger<OrderCalculator> logger;
    #endregion

    #region コンストラクタ
    /// <summary>注文合計計算サービスコンストラクタ</summary>
    public OrderCalculator(
        ITaxService taxService,
        IDiscountPolicy discountPolicy,
        ILogger<OrderCalculator> logger)
    {
        this.taxService = taxService ?? throw new ArgumentNullException(nameof(taxService));
        this.discountPolicy = discountPolicy ?? throw new ArgumentNullException(nameof(discountPolicy));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    #endregion

    #region メソッド
    /// <summary>合計金額計算非同期処理</summary>
    public async Task<Money> CalculateAsync(Order order, CancellationToken cancellationToken)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));

        logger.LogInformation("注文計算開始 OrderId={OrderId}", order.Id);

        decimal subtotal = 0m;
        foreach (var i in order.Items)
        {
            subtotal += i.unitPrice * i.quantity;
        }

        var discount = await discountPolicy.GetDiscountAsync(order, cancellationToken).ConfigureAwait(false);
        var taxable = subtotal - discount.Amount;
        var tax = await taxService.CalculateAsync(taxable, cancellationToken).ConfigureAwait(false);

        var total = subtotal - discount.Amount + tax.Amount;

        logger.LogInformation("注文計算完了 OrderId={OrderId} Total={Total}", order.Id, total);

        return new Money(total);
    }
    #endregion
}
```

## Copilot Interaction Examples

- 「このクラスに日本語 summary を追加し region で再構成」
- 「フィールド命名を camelCase に変更」
- 「.NET 8 対応で primary constructor 利用例を提示」

## Anti-Patterns to Avoid

- `_fieldName` のような旧命名（本規約では禁止）
- 英語と日本語コメントの混在
- 文末「。」付き summary
- 無差別 #region ネスト
- `.Result` / `.Wait()` による同期ブロック

## Conflict Resolution

- 旧規約の underscore 付きフィールド命名と region 不使用方針は本ファイルの指示が優先。
- 生成時に衝突があればユーザーへ日本語命名方針確認 → 本ファイル準拠。
