using System;

namespace WPFDataGridFilter.Models
{
    /// <summary>
    /// ログ行データのモデル。
    /// 表示用の各文字列と、フィルタで用いるパース済み日時を持ちます。
    /// </summary>
    public class LogEntry
    {
        #region プロパティ（表示用）
        public string? Time { get; set; }
        public string? IFNum { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public string? Event { get; set; }
        public object? Data { get; set; }

        /// <summary>
        /// 画面表示およびテキストフィルターで使用する文字列表現。
        /// </summary>
        public string DataText
        {
            get
            {
                return Data switch
                {
                    null => string.Empty,
                    string text => text,
                    byte[] bytes => BitConverter.ToString(bytes).Replace("-", " "),
                    _ => Data?.ToString() ?? string.Empty
                };
            }
        }
        #endregion プロパティ（表示用）

        #region プロパティ（内部用）
        /// <summary>
        /// フィルタ向けにパース済みの日時。
        /// </summary>
        public DateTime? TimeStamp
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Time)) return null;
                // 代表的なフォーマットを複数試す（必要に応じて拡張可）
                string[] fmts = new[]
                {
                    "yyyy/MM/dd HH:mm:ss.fff",
                    "yyyy/MM/dd HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss.fff",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ss.fff",
                    "yyyy-MM-ddTHH:mm:ss",
                };
                if (DateTime.TryParseExact(Time, fmts, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var dt))
                {
                    return dt;
                }
                if (DateTime.TryParse(Time, out var any)) return any;
                return null;
            }
        }
        #endregion プロパティ（内部用）
    }
}
