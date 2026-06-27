using Jint;
using Jint.Native;
using AxarDB.Wrappers;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Bridge for a single in-memory collection.
    /// Exposes insert, findall, find, and delete — matching CollectionBridge's API.
    /// </summary>
    public class MemoryCollectionBridge
    {
        private readonly MemoryStore _store;
        private readonly string _collectionName;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public MemoryCollectionBridge(MemoryStore store, string collectionName, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _store = store;
            _collectionName = collectionName;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Insert a document into the in-memory collection.
        /// </summary>
        /// <param name="docObj">JavaScript object to insert.</param>
        /// <param name="hours">TTL in hours. Defaults to 1 hour if not provided.</param>
        public object? insert(object docObj, double hours = 1.0)
        {
            var dict = ConvertToDictionary(docObj);
            if (dict == null) return null;

            return _store.Insert(_collectionName, dict, hours);
        }

        /// <summary>
        /// Returns a MemoryResultSet of all non-expired documents, optionally filtered by a predicate.
        /// </summary>
        public MemoryResultSet findall(JsValue? predicate = null)
        {
            var all = _store.FindAll(_collectionName);

            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                return new MemoryResultSet(all, _store, _collectionName);
            }

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

            return new MemoryResultSet(all.Where(csPredicate), _store, _collectionName);
        }

        /// <summary>
        /// Returns the first non-expired document matching the predicate, or null if not found.
        /// </summary>
        public DocumentWrapper? find(Func<object, bool> predicate)
        {
            Func<Dictionary<string, object>, bool> safePredicate = (d) =>
            {
                if (_engine == null) return predicate(new DocumentWrapper(d));
                lock (_engine)
                {
                    try { return predicate(new DocumentWrapper(d)); } catch { return false; }
                }
            };

            var doc = _store.FindAll(_collectionName).FirstOrDefault(safePredicate);
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        /// <summary>
        /// Deletes documents matching the predicate from the in-memory collection.
        /// </summary>
        public void delete(JsValue? predicate = null)
        {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                // Delete all
                _store.Delete(_collectionName, _ => true);
                return;
            }

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

            _store.Delete(_collectionName, csPredicate);
        }

        private static Dictionary<string, object>? ConvertToDictionary(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> dict) return dict;
            if (obj is IDictionary<string, object> idict) return new Dictionary<string, object>(idict);
            if (obj is System.Dynamic.ExpandoObject expando)
                return expando.ToDictionary(k => k.Key, v => v.Value ?? new object());
            return null;
        }
    }
}
