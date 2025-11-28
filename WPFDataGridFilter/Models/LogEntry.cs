using System;
using System.Globalization;

namespace WPFDataGridFilter.Models
{
    /// <summary>
    /// ログ行データのモデル。
    /// 表示用の各文字列と、フィルタで用いるパース済み日時を持ちます。
    /// </summary>
    public class LogEntry
    {
        #region 定数
        /// <summary>日時解析で利用するフォーマット候補</summary>
        private static readonly string[] SupportedTimeFormats =
        {
            "yyyy/MM/dd HH:mm:ss.fff",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
        };
        #endregion

        #region フィールド
        /// <summary>TimeStamp のキャッシュ済みフラグ</summary>
        private bool timeStampCached;

        /// <summary>TimeStamp のキャッシュ値</summary>
        private DateTime? timeStampValue;
        #endregion

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
        /// フィルタ向けにパース済みの日時（キャッシュ付き遅延評価）。
        /// </summary>
        public DateTime? TimeStamp
        {
            get
            {
                if (timeStampCached)
                {
                    return timeStampValue;
                }

                timeStampCached = true;

                if (string.IsNullOrWhiteSpace(Time))
                {
                    timeStampValue = null;
                    return null;
                }

                if (DateTime.TryParseExact(Time, SupportedTimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var dt))
                {
                    timeStampValue = dt;
                    return dt;
                }

                if (DateTime.TryParse(Time, out var any))
                {
                    timeStampValue = any;
                    return any;
                }

                timeStampValue = null;
                return null;
            }
        }
        #endregion プロパティ（内部用）
    }
}
