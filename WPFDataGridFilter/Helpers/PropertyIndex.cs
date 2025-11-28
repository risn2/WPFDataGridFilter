using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WPFDataGridFilter.Helpers
{
    /// <summary>
    /// 選択フィルター高速化のためのプロパティ値インデックス。
    /// プロパティ値 → アイテムインデックスのマッピングを保持し、O(1)でのルックアップを実現します。
    /// </summary>
    public sealed class PropertyIndex
    {
        #region フィールド
        /// <summary>プロパティ名 → (値 → インデックスリスト) のマップ</summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, List<int>>> _indices = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>プロパティアクセサーのキャッシュ</summary>
        private readonly ConcurrentDictionary<(Type, string), Func<object, string?>> _accessors = new();

        /// <summary>インデックス構築元のデータソース</summary>
        private IList? _source;

        /// <summary>データソースのバージョン（変更検出用）</summary>
        private int _sourceVersion;
        #endregion

        #region プロパティ
        /// <summary>インデックスが構築済みのプロパティ一覧</summary>
        public IReadOnlyCollection<string> IndexedProperties => _indices.Keys.ToList();

        /// <summary>データソースが設定済みか</summary>
        public bool HasSource => _source != null;
        #endregion

        #region メソッド
        /// <summary>
        /// データソースを設定（既存インデックスはクリア）
        /// </summary>
        /// <param name="source">インデックス対象のデータソース</param>
        public void SetSource(IList? source)
        {
            if (ReferenceEquals(_source, source)) return;

            _source = source;
            _sourceVersion++;
            _indices.Clear();
        }

        /// <summary>
        /// 指定プロパティのインデックスを構築
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        public void BuildIndex(string propertyName)
        {
            if (_source == null || string.IsNullOrWhiteSpace(propertyName)) return;

            var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < _source.Count; i++)
            {
                var item = _source[i];
                if (item == null) continue;

                var value = GetPropertyValue(item, propertyName) ?? string.Empty;

                if (!index.TryGetValue(value, out var list))
                {
                    list = new List<int>();
                    index[value] = list;
                }
                list.Add(i);
            }

            _indices[propertyName] = index;
        }

        /// <summary>
        /// 指定プロパティのインデックスが存在するか
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        /// <returns>インデックスが存在すれば true</returns>
        public bool HasIndex(string propertyName)
        {
            return _indices.ContainsKey(propertyName);
        }

        /// <summary>
        /// インデックスを使用して選択フィルターに一致するアイテムインデックスを取得
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        /// <param name="allowedValues">許可される値の集合</param>
        /// <returns>一致するアイテムのインデックス集合</returns>
        public HashSet<int> GetMatchingIndices(string propertyName, IReadOnlyCollection<string> allowedValues)
        {
            var result = new HashSet<int>();

            if (!_indices.TryGetValue(propertyName, out var index))
            {
                return result;
            }

            foreach (var value in allowedValues)
            {
                if (index.TryGetValue(value, out var indices))
                {
                    foreach (var idx in indices)
                    {
                        result.Add(idx);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// インデックスから重複排除済み値一覧を取得（高速）
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        /// <returns>値一覧（インデックス未構築の場合は null）</returns>
        public IReadOnlyList<string>? GetDistinctValuesFromIndex(string propertyName)
        {
            if (!_indices.TryGetValue(propertyName, out var index))
            {
                return null;
            }

            var list = index.Keys.ToList();
            list.Sort(StringComparer.CurrentCulture);
            return list;
        }

        /// <summary>
        /// すべてのインデックスをクリア
        /// </summary>
        public void ClearAll()
        {
            _indices.Clear();
        }

        /// <summary>
        /// 指定プロパティのインデックスをクリア
        /// </summary>
        /// <param name="propertyName">対象プロパティ名</param>
        public void Clear(string propertyName)
        {
            _indices.TryRemove(propertyName, out _);
        }

        /// <summary>
        /// プロパティ値を取得
        /// </summary>
        private string? GetPropertyValue(object item, string propertyName)
        {
            var key = (item.GetType(), propertyName);

            var accessor = _accessors.GetOrAdd(key, static tuple =>
            {
                var (type, name) = tuple;
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    return _ => null;
                }

                return target => property.GetValue(target)?.ToString();
            });

            return accessor(item);
        }
        #endregion
    }
}
