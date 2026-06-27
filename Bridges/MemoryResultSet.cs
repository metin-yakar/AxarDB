using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB.Bridges
{
    /// <summary>
    /// A ResultSet variant for in-memory collections.
    /// Supports chaining (take, skip, select, toList, foreach, count, delete) 
    /// where delete() works directly against the MemoryStore.
    /// </summary>
    public class MemoryResultSet : IEnumerable<DocumentWrapper>
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly MemoryStore _store;
        private readonly string _collectionName;

        public MemoryResultSet(IEnumerable<Dictionary<string, object>> source, MemoryStore store, string collectionName)
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

        public MemoryResultSet take(int count)
            => new MemoryResultSet(_source.Take(count), _store, _collectionName);

        public MemoryResultSet skip(int count)
            => new MemoryResultSet(_source.Skip(count), _store, _collectionName);

        public AxarList select(Func<object, object> selector)
        {
            var list = _source.Select(d => selector(new DocumentWrapper(d)));
            return new AxarList(list);
        }

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
        /// Deletes all documents in this result set from the MemoryStore.
        /// </summary>
        public void delete()
        {
            // Materialize IDs first to avoid modifying the collection while iterating
            var ids = _source
                .Where(d => d.ContainsKey("_id"))
                .Select(d => d["_id"].ToString()!)
                .ToList();

            _store.Delete(_collectionName, d =>
                d.TryGetValue("_id", out var id) && ids.Contains(id.ToString()!));
        }
    }
}
