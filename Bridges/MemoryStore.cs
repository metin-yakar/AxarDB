using System.Collections.Concurrent;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Singleton in-memory store with TTL (Time-To-Live) support.
    /// Collections are stored in memory only; data is not persisted to disk.
    /// Expired entries are cleaned up lazily on access and periodically via a background timer.
    /// </summary>
    public class MemoryStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemoryEntry>> _collections = new();
        private readonly Timer _cleanupTimer;

        public MemoryStore()
        {
            // Cleanup expired entries every 5 minutes
            _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public struct MemoryEntry
        {
            public Dictionary<string, object> Document { get; set; }
            public DateTime ExpiresAt { get; set; }

            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }

        public IEnumerable<string> GetCollectionNames() => _collections.Keys;

        public int GetRecordCount(string name)
        {
            if (_collections.TryGetValue(name, out var col))
            {
                return col.Values.Count(v => !v.IsExpired);
            }
            return 0;
        }

        private ConcurrentDictionary<string, MemoryEntry> GetOrCreateCollection(string name)
        {
            return _collections.GetOrAdd(name, _ => new ConcurrentDictionary<string, MemoryEntry>());
        }

        /// <summary>
        /// Insert a document into the named collection with a TTL in hours (default: 1 hour).
        /// If a document with the same _id already exists, it is overwritten.
        /// </summary>
        public Dictionary<string, object> Insert(string collectionName, Dictionary<string, object> document, double hours = 1.0)
        {
            if (!document.ContainsKey("_id"))
            {
                document["_id"] = AxarDB.Helpers.GuidV7.NewGuid().ToString();
            }

            var id = document["_id"].ToString()!;
            var col = GetOrCreateCollection(collectionName);

            var entry = new MemoryEntry
            {
                Document = document,
                ExpiresAt = DateTime.UtcNow.AddHours(hours)
            };

            col[id] = entry;
            return document;
        }

        /// <summary>
        /// Returns all non-expired documents from the named collection.
        /// </summary>
        public IEnumerable<Dictionary<string, object>> FindAll(string collectionName)
        {
            if (!_collections.TryGetValue(collectionName, out var col))
                yield break;

            foreach (var kvp in col)
            {
                if (!kvp.Value.IsExpired)
                    yield return kvp.Value.Document;
                else
                    col.TryRemove(kvp.Key, out _); // Lazy cleanup
            }
        }

        /// <summary>
        /// Deletes documents matching the predicate from the named collection.
        /// </summary>
        public void Delete(string collectionName, Func<Dictionary<string, object>, bool> predicate)
        {
            if (!_collections.TryGetValue(collectionName, out var col)) return;

            var toDelete = col
                .Where(kvp => !kvp.Value.IsExpired && predicate(kvp.Value.Document))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toDelete)
                col.TryRemove(key, out _);
        }

        /// <summary>
        /// Removes all expired entries across all collections.
        /// </summary>
        private void CleanupExpired()
        {
            foreach (var col in _collections.Values)
            {
                var expired = col.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
                foreach (var key in expired)
                    col.TryRemove(key, out _);
            }
        }
    }
}
