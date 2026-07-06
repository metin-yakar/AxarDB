using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Manages JSONL-based bulk collections stored in the "Bulk/" folder.
    /// Supports lazy loading, in-memory caching, and auto-reload via FileSystemWatcher.
    /// Max memory footprint is capped at ~50 MB across all cached collections.
    /// </summary>
    public class BulkStore : IDisposable
    {
        private readonly string _bulkPath;
        private readonly ConcurrentDictionary<string, BulkCacheEntry> _cache = new();
        private readonly FileSystemWatcher _watcher;
        private readonly JsonSerializerOptions _jsonOptions;

        // Approximate cap: we track total serialized byte size
        private long _totalCachedBytes = 0;
        private readonly long _maxCacheBytes;

        public BulkStore(string basePath, long maxCacheBytes)
        {
            _bulkPath = Path.Combine(basePath, "Bulk");
            if (!Directory.Exists(_bulkPath))
                Directory.CreateDirectory(_bulkPath);

            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _jsonOptions.Converters.Add(new AxarDB.Storage.CustomObjectConverter());
            _maxCacheBytes = maxCacheBytes;

            // FileSystemWatcher for auto-reload
            _watcher = new FileSystemWatcher(_bulkPath, "*.jsonl")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
        }

        private class BulkCacheEntry
        {
            public List<Dictionary<string, object>> Documents { get; set; } = new();
            public DateTime LoadedAt { get; set; }
            public long ApproximateBytes { get; set; }
        }

        // ─── File System Events ───────────────────────────────────────────────

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (!string.IsNullOrEmpty(name))
                InvalidateCache(name);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            var name = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (_cache.TryRemove(name, out var entry))
                Interlocked.Add(ref _totalCachedBytes, -entry.ApproximateBytes);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            var oldName = Path.GetFileNameWithoutExtension(e.OldName ?? "");
            var newName = Path.GetFileNameWithoutExtension(e.Name ?? "");
            if (_cache.TryRemove(oldName, out var entry))
                Interlocked.Add(ref _totalCachedBytes, -entry.ApproximateBytes);
            if (!string.IsNullOrEmpty(newName))
                InvalidateCache(newName);
        }

        private void InvalidateCache(string name)
        {
            if (_cache.TryRemove(name, out var old))
                Interlocked.Add(ref _totalCachedBytes, -old.ApproximateBytes);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns names of all existing JSONL collections.</summary>
        public IEnumerable<string> ListCollections()
        {
            if (!Directory.Exists(_bulkPath)) yield break;
            foreach (var f in Directory.EnumerateFiles(_bulkPath, "*.jsonl"))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        /// <summary>Returns all documents from the named JSONL collection (cached).</summary>
        public IEnumerable<Dictionary<string, object>> GetDocuments(string name)
        {
            if (_cache.TryGetValue(name, out var entry))
                return entry.Documents;

            return LoadAndCache(name);
        }

        /// <summary>Insert/append documents to a JSONL file, creating it if it doesn't exist.</summary>
        public void Insert(string name, IEnumerable<Dictionary<string, object>> documents)
        {
            var path = GetFilePath(name);
            var lines = new List<string>();

            foreach (var doc in documents)
            {
                if (!doc.ContainsKey("_id"))
                    doc["_id"] = Guid.NewGuid().ToString();
                lines.Add(JsonSerializer.Serialize(doc));
            }

            // Append to file
            File.AppendAllLines(path, lines, Encoding.UTF8);

            // Invalidate cache so it reloads fresh
            InvalidateCache(name);
        }

        /// <summary>Manually reload a specific collection from disk.</summary>
        public void Reload(string name)
        {
            InvalidateCache(name);
            LoadAndCache(name);
        }

        /// <summary>Reload all collections.</summary>
        public void ReloadAll()
        {
            foreach (var name in ListCollections())
                Reload(name);
        }

        /// <summary>Delete documents matching a predicate and rewrite the JSONL file.</summary>
        public void Delete(string name, Func<Dictionary<string, object>, bool> predicate)
        {
            var docs = GetDocuments(name).ToList();
            var remaining = docs.Where(d => !predicate(d)).ToList();
            var path = GetFilePath(name);
            File.WriteAllLines(path, remaining.Select(d => JsonSerializer.Serialize(d)), Encoding.UTF8);
            InvalidateCache(name);
        }

        // ─── Internal ─────────────────────────────────────────────────────────

        private IEnumerable<Dictionary<string, object>> LoadAndCache(string name)
        {
            var path = GetFilePath(name);
            if (!File.Exists(path)) return Enumerable.Empty<Dictionary<string, object>>();

            var docs = new List<Dictionary<string, object>>();
            long bytes = 0;

            try
            {
                foreach (var line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(line, _jsonOptions);
                        if (doc != null)
                        {
                            docs.Add(doc);
                            bytes += line.Length * 2; // approximate UTF-16 byte size
                        }
                    }
                    catch { /* Skip malformed lines */ }
                }
            }
            catch (IOException) { return Enumerable.Empty<Dictionary<string, object>>(); }

            // Enforce 50MB cap: evict oldest if over budget
            EnforceMemoryCap(bytes);

            var entry = new BulkCacheEntry
            {
                Documents = docs,
                LoadedAt = DateTime.UtcNow,
                ApproximateBytes = bytes
            };

            _cache[name] = entry;
            Interlocked.Add(ref _totalCachedBytes, bytes);
            return docs;
        }

        private void EnforceMemoryCap(long incomingBytes)
        {
            if (_totalCachedBytes + incomingBytes <= _maxCacheBytes) return;

            // Evict oldest entries until we have room
            var ordered = _cache.OrderBy(kv => kv.Value.LoadedAt).ToList();
            foreach (var kv in ordered)
            {
                if (_totalCachedBytes + incomingBytes <= _maxCacheBytes) break;
                if (_cache.TryRemove(kv.Key, out var removed))
                    Interlocked.Add(ref _totalCachedBytes, -removed.ApproximateBytes);
            }
        }

        public IEnumerable<Dictionary<string, object>> QueryChunks(string name, HashSet<string> filterFields, Func<Dictionary<string, object>, bool> predicate)
        {
            var path = GetFilePath(name);
            if (!File.Exists(path)) yield break;

            var chunkLines = new List<string>();
            long currentChunkBytes = 0;

            // Read the file line-by-line
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                chunkLines.Add(line);
                currentChunkBytes += line.Length * 2;

                // When chunk reaches the cache limit, we process it
                if (currentChunkBytes >= _maxCacheBytes)
                {
                    foreach (var doc in ProcessChunk(chunkLines, filterFields, predicate))
                    {
                        yield return doc;
                    }
                    chunkLines.Clear();
                    currentChunkBytes = 0;
                    
                    // Force garbage collection of the chunk
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }

            // Process the remaining lines in the last chunk
            if (chunkLines.Count > 0)
            {
                foreach (var doc in ProcessChunk(chunkLines, filterFields, predicate))
                {
                    yield return doc;
                }
                chunkLines.Clear();
            }
        }

        private IEnumerable<Dictionary<string, object>> ProcessChunk(List<string> lines, HashSet<string> filterFields, Func<Dictionary<string, object>, bool> predicate)
        {
            var matchedDocs = new List<Dictionary<string, object>>();

            // 1. Build temporary lightweight index in memory
            var tempIndex = new List<(int index, Dictionary<string, object> fields)>();
            for (int i = 0; i < lines.Count; i++)
            {
                var lightDoc = ParseSelectedProperties(lines[i], filterFields);
                tempIndex.Add((i, lightDoc));
            }

            // 2. Perform prediction (filtering) on the temporary index
            foreach (var entry in tempIndex)
            {
                if (predicate(entry.fields))
                {
                    // 3. Match found: parse full document
                    try
                    {
                        var fullDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(lines[entry.index], _jsonOptions);
                        if (fullDoc != null)
                        {
                            matchedDocs.Add(fullDoc);
                        }
                    }
                    catch {}
                }
            }

            return matchedDocs;
        }

        private static Dictionary<string, object> ParseSelectedProperties(string json, IEnumerable<string> properties)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                foreach (var prop in properties)
                {
                    if (root.TryGetProperty(prop, out var val))
                    {
                        dict[prop] = GetJsonValue(val);
                    }
                }
            }
            catch {}
            return dict;
        }

        private static object GetJsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? "";
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l)) return l;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null!;
                default:
                    return element.GetRawText(); // fallback for objects/arrays
            }
        }

        private string GetFilePath(string name)
            => Path.Combine(_bulkPath, $"{name}.jsonl");

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
