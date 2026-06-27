using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB.Bridges
{
    /// <summary>
    /// ResultSet for bulk (JSONL) collections. Supports chaining but no direct modification.
    /// delete() rewrites the JSONL file without the matched rows.
    /// </summary>
    public class BulkResultSet : IEnumerable<DocumentWrapper>
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly BulkStore _store;
        private readonly string _collectionName;

        public BulkResultSet(IEnumerable<Dictionary<string, object>> source, BulkStore store, string collectionName)
        {
            _source = source;
            _store = store;
            _collectionName = collectionName;
        }

        public IEnumerator<DocumentWrapper> GetEnumerator()
            => _source.Select(d => new DocumentWrapper(d)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public List<DocumentWrapper> toList() => _source.Select(d => new DocumentWrapper(d)).ToList();
        public List<DocumentWrapper> ToList() => toList();

        public BulkResultSet take(int count)
            => new BulkResultSet(_source.Take(count), _store, _collectionName);

        public BulkResultSet skip(int count)
            => new BulkResultSet(_source.Skip(count), _store, _collectionName);

        public AxarList select(Func<object, object> selector)
            => new AxarList(_source.Select(d => selector(new DocumentWrapper(d))));

        public DocumentWrapper? first()
        {
            var doc = _source.FirstOrDefault();
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return _source.Count();
            return _source.Count(d => predicate(new DocumentWrapper(d)));
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
            foreach (var doc in _source)
                action(new DocumentWrapper(doc));
        }

        /// <summary>
        /// Removes all matched documents from the JSONL file (rewrites file without them).
        /// </summary>
        public void delete()
        {
            var ids = _source
                .Where(d => d.ContainsKey("_id"))
                .Select(d => d["_id"].ToString()!)
                .ToHashSet();

            _store.Delete(_collectionName, d =>
                d.TryGetValue("_id", out var id) && ids.Contains(id.ToString()!));
        }
    }
}
