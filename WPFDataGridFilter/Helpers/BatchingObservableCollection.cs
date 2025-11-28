using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// バッチ追加時に1回の通知で複数アイテムを追加するコレクション。
    /// UI更新回数を削減し、パフォーマンスを向上させます。
    /// </summary>
    /// <typeparam name="T">コレクションに格納する要素の型</typeparam>
    public class BatchingObservableCollection<T> : ObservableCollection<T>
    {
        #region フィールド
        /// <summary>通知を抑制するフラグ</summary>
        private bool suppressNotification;
        #endregion

        #region メソッド
        /// <summary>
        /// 複数アイテムを一括追加（通知は1回のみ）
        /// </summary>
        /// <param name="items">追加するアイテムのコレクション</param>
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
        /// 最大件数を超えた古いアイテムを削除しつつ追加。
        /// FIFO（先入れ先出し）で古いアイテムから削除されます。
        /// </summary>
        /// <param name="items">追加するアイテムのコレクション</param>
        /// <param name="maxCount">コレクション内の最大件数</param>
        public void AddRangeWithLimit(IEnumerable<T> items, int maxCount)
        {
            if (items == null) return;

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

        /// <summary>
        /// コレクションをクリアして新しいアイテムで置き換え（通知は1回のみ）
        /// </summary>
        /// <param name="items">新しいアイテムのコレクション</param>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null)
            {
                Clear();
                return;
            }

            suppressNotification = true;

            try
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                suppressNotification = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// CollectionChanged イベントを発生させる
        /// </summary>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }
        #endregion
    }
}
