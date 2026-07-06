using System.Collections.Concurrent;
using AxarDB.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace AxarDB.Definitions
{
    public class Collection
    {
        public string Name { get; private set; }
        public List<IndexDefinition> Indices { get; set; } = new();
        private readonly DiskStorage _storage;
        
        // Hybrid Caching - Shared
        private readonly IMemoryCache _cache;
        private readonly HashSet<string> _primaryIndex = new();
        private readonly object _indexLock = new object();

        public Collection(string name, DiskStorage storage, IMemoryCache sharedCache)
        {
            Name = name;
            _storage = storage;
            // NOTE: EnsureCollection is NOT called here intentionally.
            // The collection directory is created lazily on the first write (Insert).
            // This prevents empty directories from being created for collections
            // that are only read (e.g. db.nonExistent.findall()).
            _cache = sharedCache;
            
        // 1. Load Primary Index (IDs only) - FAST
            Reload();
        }

        public void Reload()
        {            
            lock (_indexLock)
            {
                _primaryIndex.Clear();
                foreach (var id in _storage.GetAllDocumentIds(Name))
                {
                    _primaryIndex.Add(id);
                }
            }
            
            // Indices
            Indices.Clear();
            var path = Path.Combine("Data", Name);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "idx_*.json"))
                {
                    var idx = IndexDefinition.Load(file);
                    if (idx != null) Indices.Add(idx);
                }
            }
        }

        private string GetCacheKey(string id) => $"{Name}:{id}";

        private Dictionary<string, object>? GetDocument(string id)
        {
            var key = GetCacheKey(id);
            Dictionary<string, object>? doc = null;

            // Cache Look-aside
            if (_cache.TryGetValue<Dictionary<string, object>>(key, out var cachedDoc))
            {
                 doc = cachedDoc;
            }
            else
            {
                // Disk Read
                doc = _storage.LoadDocument(Name, id);
                
                if (doc != null)
                {
                    // Accurate Bytes estimate (UTF-16 string length * 2)
                    long size = System.Text.Json.JsonSerializer.Serialize(doc).Length * 2L; 
                    if (size < 1000) size = 1000; // minimum size threshold
                    
                    var entryOptions = new MemoryCacheEntryOptions
                    {
                        Size = size,
                        SlidingExpiration = TimeSpan.FromMinutes(10) 
                    };
                    
                    _cache.Set(key, doc, entryOptions);
                }
            }
            
            // CRITICAL: Return DeepCopy to prevent in-memory mutation by Safe Queries
            if (doc != null)
            {
                // We use ScriptUtils.DeepCopy which uses our new JSON converter
                var copy = AxarDB.Helpers.ScriptUtils.DeepCopy(doc) as Dictionary<string, object>;
                return copy;
            }
            return null;
        }

        public void Insert(Dictionary<string, object> document, CancellationToken cancellationToken = default, bool bypassSystemRules = false)
        {
            if (Name.StartsWith("sys", StringComparison.OrdinalIgnoreCase))
            {
                if (!Name.Equals("sysusers", StringComparison.OrdinalIgnoreCase) &&
                    !Name.Equals("sysqueue", StringComparison.OrdinalIgnoreCase) &&
                    !Name.Equals("sysvaults", StringComparison.OrdinalIgnoreCase) &&
                    !Name.Equals("sysconfig", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Users cannot create custom system collections starting with 'sys'.");
                }

                if (Name.Equals("sysconfig", StringComparison.OrdinalIgnoreCase) && !bypassSystemRules)
                {
                    throw new InvalidOperationException("Insert operation is not allowed on sysconfig collection.");
                }
            }

            if (!document.ContainsKey("_id"))
            {
                document["_id"] = Guid.NewGuid().ToString();
            }
            string id = document["_id"].ToString()!;
            
            // Validate system collection structure
            ValidateSystemCollectionStructure(document);

            // Ensure the collection directory exists on first write (lazy creation)
            _storage.EnsureCollection(Name);

            // 1. Disk Persist
            _storage.SaveDocument(Name, document);

            // 2. Update Index
            lock (_indexLock) { _primaryIndex.Add(id); }

            // 3. Update/Invalidate Cache
            long size = System.Text.Json.JsonSerializer.Serialize(document).Length * 2L;
            if (size < 1000) size = 1000;
            
            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = size,
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            _cache.Set(GetCacheKey(id), document, entryOptions);

            // 4. Update Secondary Indexes
            foreach (var index in Indices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index.IndexDocument(document);
                // Save index periodically or on change (Debounce logic implied for production, direct here)
                index.Save(Path.Combine("Data", Name)); 
            }
        }

        public IEnumerable<Dictionary<string, object>> FindAll(CancellationToken cancellationToken = default)
        {
            string[] ids;
            lock (_indexLock) { ids = _primaryIndex.ToArray(); }
            
            return ids.AsParallel().WithCancellation(cancellationToken)
                      .Select(id => GetDocument(id))
                      .Where(doc => doc != null)!;
        }

        public IEnumerable<Dictionary<string, object>> FindAll(Func<Dictionary<string, object>, bool> predicate, AxarDB.Query.QueryOptimizer.AnalysisResult? analysis = null, CancellationToken cancellationToken = default)
        {
             // Optimization: If analyzed and index available, use it
             if (analysis != null && analysis.Value.Prop != null)
             {
                 var idx = Indices.FirstOrDefault(i => i.PropertyName == analysis.Value.Prop);
                 if (idx != null)
                 {
                     var ids = idx.Search(analysis.Value.Val!, analysis.Value.Op);
                     
                     var docs = ids.AsParallel().WithCancellation(cancellationToken)
                               .Select(id => GetDocument(id))
                               .Where(doc => doc != null)
                               .Select(doc => doc!)
                               .Where(predicate)
                               .ToList();
                     return docs;
                 }
             }

             // Full Scan with Parallelism
             string[] allIds;
             lock (_indexLock) { allIds = _primaryIndex.ToArray(); }

             var allDocs = allIds.AsParallel().WithCancellation(cancellationToken)
                          .Select(id => GetDocument(id))
                          .Where(doc => doc != null)
                          .Select(doc => doc!)
                          .Where(predicate)
                          .ToList();
             return allDocs;
        }

        public void CreateIndex(string propertyName, string type)
        {
            var index = new IndexDefinition(propertyName, type);
            // Re-index existing data
            // Use local FindAll to leverage parallel load
            foreach (var doc in FindAll())
            {
                index.IndexDocument(doc);
            }
            Indices.Add(index);
            index.Save(Path.Combine("Data", Name));
        }

        public void Update(Func<Dictionary<string, object>, bool> predicate, Dictionary<string, object> updateFields, CancellationToken cancellationToken = default)
        {
            // 1. Identify matches (Parallel Scan) -- pass token!
            var matches = FindAll(predicate, null, cancellationToken);
            
            // 2. Perform Updates
            foreach (var doc in matches)
            {
                 cancellationToken.ThrowIfCancellationRequested();
                 // Update fields
                 foreach (var kvp in updateFields)
                 {
                     doc[kvp.Key] = kvp.Value;
                 }
                 // Save
                 Insert(doc, cancellationToken, bypassSystemRules: true); // Re-inserts/Updates cache & disk
            }
        }

        public void Delete(Func<Dictionary<string, object>, bool> predicate, CancellationToken cancellationToken = default)
        {
            var matches = FindAll(predicate, null, cancellationToken);
            
            foreach (var doc in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (doc.TryGetValue("_id", out var idObj))
                {
                    string id = idObj.ToString()!;
                    // Update Disk
                    _storage.DeleteDocument(Name, id);
                    // Update Index
                    lock (_indexLock) { _primaryIndex.Remove(id); }
                    // Update Cache
                    _cache.Remove(GetCacheKey(id));
                }
            }
        }
        public void OnExternalChange(string id, string evtType)
        {
            // Handle external file changes (manual edit, FTP, etc.)
            // We need to reflect these changes in Memory (Index & Cache)
            
            if (evtType == "deleted")
            {
                lock (_indexLock) { _primaryIndex.Remove(id); }
                _cache.Remove(GetCacheKey(id));
                // TODO: Remove from secondary indices if supported
            }
            else if (evtType == "created")
            {
                lock (_indexLock) { _primaryIndex.Add(id); }
                // We don't populate cache here, let next read do it
                // We should update secondary indices
                var doc = _storage.LoadDocument(Name, id);
                if (doc != null)
                {
                    foreach (var index in Indices) index.IndexDocument(doc);
                }
            }
            else if (evtType == "changed")
            {
                // Invalidate cache
                _cache.Remove(GetCacheKey(id));
                
                // Update secondary indices
                var doc = _storage.LoadDocument(Name, id);
                if (doc != null)
                {
                    foreach (var index in Indices) index.IndexDocument(doc);
                }
            }
        }

        // --- System Collection Validation Infrastructure ---
        private static readonly Dictionary<string, HashSet<string>> SystemCollectionSchemas = new(StringComparer.OrdinalIgnoreCase)
        {
            { "sysusers", new HashSet<string> { "_id", "username", "password" } },
            { "sysvaults", new HashSet<string> { "_id", "key", "value", "created" } },
            { "sysconfig", new HashSet<string> { "_id", "memoryLimitPercentage", "bulkStoreMaxCacheBytes", "maxRecursionDepth", "queryTimeoutMinutes", "queuePollIntervalSeconds" } },
            { "sysqueue", new HashSet<string> { "_id", "queryTemplate", "parameters", "options", "createdAt", "executionTime", "priority", "duration", "successResult", "errorMessage", "completedAt" } }
        };

        private static readonly Dictionary<string, Dictionary<string, Type>> SystemCollectionTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "sysusers", new Dictionary<string, Type>
                {
                    { "_id", typeof(string) },
                    { "username", typeof(string) },
                    { "password", typeof(string) }
                }
            },
            {
                "sysvaults", new Dictionary<string, Type>
                {
                    { "_id", typeof(string) },
                    { "key", typeof(string) },
                    { "created", typeof(DateTime) }
                }
            },
            {
                "sysconfig", new Dictionary<string, Type>
                {
                    { "_id", typeof(string) },
                    { "memoryLimitPercentage", typeof(double) },
                    { "bulkStoreMaxCacheBytes", typeof(long) },
                    { "maxRecursionDepth", typeof(int) },
                    { "queryTimeoutMinutes", typeof(int) },
                    { "queuePollIntervalSeconds", typeof(double) }
                }
            },
            {
                "sysqueue", new Dictionary<string, Type>
                {
                    { "_id", typeof(string) },
                    { "queryTemplate", typeof(string) },
                    { "createdAt", typeof(DateTime) },
                    { "priority", typeof(int) },
                    { "duration", typeof(long) },
                    { "completedAt", typeof(DateTime) }
                }
            }
        };

        private static readonly HashSet<string> FlexibleFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "value", "parameters", "options", "successResult"
        };

        private void ValidateSystemCollectionStructure(Dictionary<string, object> document)
        {
            if (!Name.StartsWith("sys")) return;

            HashSet<string>? expectedKeys = null;
            Dictionary<string, Type>? expectedTypes = null;

            if (SystemCollectionSchemas.TryGetValue(Name, out var predefinedKeys))
            {
                expectedKeys = predefinedKeys;
                SystemCollectionTypes.TryGetValue(Name, out expectedTypes);

                // Add any missing expected keys to ensure backward compatibility and successful validation
                foreach (var expectedKey in expectedKeys)
                {
                    if (!document.ContainsKey(expectedKey))
                    {
                        document[expectedKey] = null!;
                    }
                }
            }
            else
            {
                // Fallback to dynamic schema from first document
                var firstDoc = FindAll().FirstOrDefault();
                if (firstDoc != null)
                {
                    expectedKeys = new HashSet<string>(firstDoc.Keys);
                    expectedTypes = firstDoc.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.GetType() ?? typeof(object));
                }
            }

            if (expectedKeys != null)
            {
                // Check if keys match exactly (no fields added, no fields removed)
                if (document.Count != expectedKeys.Count || !document.Keys.All(expectedKeys.Contains))
                {
                    var docKeysStr = string.Join(", ", document.Keys);
                    var expKeysStr = string.Join(", ", expectedKeys);
                    throw new InvalidOperationException($"Document structure does not conform to the system collection '{Name}' schema. Expected keys: [{expKeysStr}], got: [{docKeysStr}]. Count expected: {expectedKeys.Count}, got: {document.Count}");
                }

                // Check type compatibility
                if (expectedTypes != null)
                {
                    foreach (var kvp in expectedTypes)
                    {
                        var key = kvp.Key;
                        var expectedType = kvp.Value;

                        if (expectedType != typeof(object) && !FlexibleFields.Contains(key))
                        {
                            if (document.TryGetValue(key, out var docVal) && docVal != null)
                            {
                                var docType = docVal.GetType();
                                if (!AreTypesCompatible(expectedType, docType))
                                {
                                    throw new InvalidOperationException($"Type mismatch for field '{key}' in system collection '{Name}'. Expected compatible with {expectedType.Name}, got {docType.Name}.");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool AreTypesCompatible(Type expectedType, Type actualType)
        {
            if (expectedType == actualType) return true;

            // Allow string and DateTime compatibility (dates can be stored/passed as strings)
            if ((expectedType == typeof(DateTime) && actualType == typeof(string)) ||
                (expectedType == typeof(string) && actualType == typeof(DateTime)))
            {
                return true;
            }

            // Check if both are numeric
            if (IsNumericType(expectedType) && IsNumericType(actualType)) return true;

            // Check if both are dictionary/object types
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(expectedType) && 
                typeof(System.Collections.IDictionary).IsAssignableFrom(actualType))
            {
                return true;
            }

            return false;
        }

        private static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}
