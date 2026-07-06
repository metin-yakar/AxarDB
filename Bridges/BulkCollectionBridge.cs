using Jint;
using Jint.Native;
using AxarDB.Wrappers;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Bridge for a single JSONL bulk collection.
    /// Supports: insert([]), findall(predicate?), find(predicate), reload(), delete()
    /// </summary>
    public class BulkCollectionBridge
    {
        private readonly BulkStore _store;
        private readonly string _collectionName;
        private readonly Engine? _engine;

        public BulkCollectionBridge(BulkStore store, string collectionName, Engine? engine = null)
        {
            _store = store;
            _collectionName = collectionName;
            _engine = engine;
        }

        /// <summary>
        /// Insert an array of documents into the JSONL collection.
        /// Creates the file if it doesn't exist.
        /// </summary>
        public object insert(object docsObj)
        {
            var list = ConvertToList(docsObj);
            if (list == null || list.Count == 0) return 0;
            _store.Insert(_collectionName, list);
            return list.Count;
        }

        /// <summary>Returns a BulkResultSet of all documents, optionally filtered.</summary>
        public BulkResultSet findall(JsValue? predicate = null)
        {
            var all = _store.GetDocuments(_collectionName);

            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
                return new BulkResultSet(all, _store, _collectionName);

            Func<Dictionary<string, object>, bool> csPredicate = (d) =>
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try
                    {
                        var result = _engine.Invoke(predicate, new object[] { new DocumentWrapper(d) });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            };

            return new BulkResultSet(all.Where(csPredicate), _store, _collectionName);
        }

        /// <summary>Returns the first document matching the predicate, or null.</summary>
        public DocumentWrapper? find(Func<object, bool> predicate)
        {
            var doc = _store.GetDocuments(_collectionName)
                .FirstOrDefault(d =>
                {
                    if (_engine != null) lock (_engine) { try { return predicate(new DocumentWrapper(d)); } catch { return false; } }
                    return predicate(new DocumentWrapper(d));
                });
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public BulkResultSet contains(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var all = _store.GetDocuments(_collectionName);
            return new BulkResultSet(all.Where(safePredicate), _store, _collectionName);
        }

        public BulkResultSet startsWith(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new CaseInsensitiveDocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new CaseInsensitiveDocumentWrapper(d)); } catch { return false; }
                }
            };
            var all = _store.GetDocuments(_collectionName);
            return new BulkResultSet(all.Where(safePredicate), _store, _collectionName);
        }

        /// <summary>Manually reload this collection from disk.</summary>
        public void reload() => _store.Reload(_collectionName);

        /// <summary>Count of all documents in this collection.</summary>
        public int count() => _store.GetDocuments(_collectionName).Count();

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static List<Dictionary<string, object>>? ConvertToList(object? obj)
        {
            if (obj == null) return null;

            // Jint array/list
            if (obj is IEnumerable<object> enumerable)
            {
                var result = new List<Dictionary<string, object>>();
                foreach (var item in enumerable)
                {
                    var dict = ConvertSingle(item);
                    if (dict != null) result.Add(dict);
                }
                return result;
            }

            // Single document wrapped in list
            var single = ConvertSingle(obj);
            return single != null ? new List<Dictionary<string, object>> { single } : null;
        }

        private static Dictionary<string, object>? ConvertSingle(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> d) return d;
            if (obj is IDictionary<string, object> id) return new Dictionary<string, object>(id);
            if (obj is System.Dynamic.ExpandoObject exp) return exp.ToDictionary(k => k.Key, v => v.Value ?? new object());
            return null;
        }
    }
}
