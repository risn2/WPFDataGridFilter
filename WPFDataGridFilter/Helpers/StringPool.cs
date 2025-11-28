using System.Collections.Concurrent;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// 文字列をインターン化（共有化）して、重複文字列のメモリ使用を削減するプール。
    /// ログデータのように同一値が繰り返し出現する場合に効果的です。
    /// </summary>
    public sealed class StringPool
    {
        #region フィールド
        /// <summary>インターン済み文字列の保持辞書</summary>
        private readonly ConcurrentDictionary<string, string> pool = new();
        #endregion

        #region プロパティ
        /// <summary>プール内のエントリ数</summary>
        public int Count => pool.Count;

        /// <summary>グローバル共有インスタンス</summary>
        public static StringPool Shared { get; } = new();
        #endregion

        #region メソッド
        /// <summary>
        /// 文字列をインターン化（共有化）して返す。
        /// 同一の文字列が既にプールにあれば、プール内のインスタンスを返します。
        /// </summary>
        /// <param name="value">インターン対象の文字列</param>
        /// <returns>インターン済みの文字列（または null/空文字列はそのまま返却）</returns>
        public string? Intern(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return pool.GetOrAdd(value, static v => v);
        }

        /// <summary>
        /// プールをクリアしてメモリを解放
        /// </summary>
        public void Clear()
        {
            pool.Clear();
        }
        #endregion
    }
}
