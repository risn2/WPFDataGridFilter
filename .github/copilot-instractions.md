<!-- version: 2025-11-13-2 -->
# Repository Copilot Instructions (Common Guidelines)

このリポジトリ全体に共通する生成/提案方針。

## General Principles

- 可読性と一貫性を最優先。局所的最適化より理解容易性。
- 早期リターンでネスト軽減。
- セキュアコーディング: 外部入力は常に検証し、パラメータ化クエリと安全 API を使用。

## Documentation & Comments

- 公開 API にはサマリ (C# 等では XML summary) を付与。
- コメントは「なぜ」を中心。同一意図の重複コメントは避ける。
- 言語固有方針（例: 日本語 summary 体現止めなど）は各言語 instructions で上書き。

## Error Handling

- 例外/エラーを黙殺しない。構造化ロギングでコンテキスト付与。
- 再試行は指数バックオフ + 最大回数。

## Performance

- 計測結果に基づき最適化。推測による premature optimization を避ける。

## Security

- 機密値は Secrets / Key Vault / 環境変数。コード直書き禁止。

## Logging

- 構造化（プレースホルダ / JSON）を推奨。

## Testing

- 境界・例外・非同期・コンカレンシを優先的にテスト。
- テストは振る舞い (公開契約) にフォーカス。

## Copilot Behavior / 対話ポリシ

- 日本語で応答すること（ユーザが明示的に他言語を指定した場合を除く）。
- 必要に応じてユーザへ質問し要求を明確化すること（曖昧・不足情報・衝突条件検知時）。
- 作業後は行った内容を要約し、ユーザが次に取れる具体的アクションを箇条書きで提示すること。
- あいまい要求には明確化の質問を返し、想定を安易に決め打ちしない。
- 競合指示は「より具体的 (言語別 / パターン限定)」を優先し、解決不能ならユーザ確認。

## Conflict Resolution

- 旧規約での C# プライベートフィールド `_camelCase` 指示と region 非使用方針は C# 言語専用ファイルで明示的に override される。
- このファイルより下位 (`.github/instructions/*.instructions.md`) が優先。
