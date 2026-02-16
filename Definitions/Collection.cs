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
            _storage.EnsureCollection(Name);
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
                    // Add to Cache
                    // Size = Bytes estimate.
                    long size = 1000 + (doc.Count * 100); 
                    
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

        public void Insert(Dictionary<string, object> document)
        {
            if (!document.ContainsKey("_id"))
            {
                document["_id"] = Guid.NewGuid().ToString();
            }
            string id = document["_id"].ToString()!;
            
            // 1. Disk Persist
            _storage.SaveDocument(Name, document);

            // 2. Update Index
            lock (_indexLock) { _primaryIndex.Add(id); }

                // Update/Invalidate Cache
            // We set it directly to keep it fresh
             var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1000 + (document.Count * 100),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            _cache.Set(GetCacheKey(id), document, entryOptions);

            // 4. Update Secondary Indexes
            foreach (var index in Indices)
            {
                index.IndexDocument(document);
                // Save index periodically or on change (Debounce logic implied for production, direct here)
                index.Save(Path.Combine("Data", Name)); 
            }
        }

        public IEnumerable<Dictionary<string, object>> FindAll()
        {
            string[] ids;
            lock (_indexLock) { ids = _primaryIndex.ToArray(); }
            
            // ConcurrentBag is safe
            var results = new ConcurrentBag<Dictionary<string, object>>();
            
            Parallel.ForEach(ids, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, id => 
            {
                var doc = GetDocument(id);
                if (doc != null) results.Add(doc);
            });
            
            return results;
        }

        public IEnumerable<Dictionary<string, object>> FindAll(Func<Dictionary<string, object>, bool> predicate, AxarDB.Query.QueryOptimizer.AnalysisResult? analysis = null)
        {
             // Optimization: If analyzed and index available, use it
             if (analysis != null && analysis.Value.Prop != null)
             {
                 var idx = Indices.FirstOrDefault(i => i.PropertyName == analysis.Value.Prop);
                 if (idx != null)
                 {
                     var ids = idx.Search(analysis.Value.Val!, analysis.Value.Op);
                     var indexedResults = new ConcurrentBag<Dictionary<string, object>>();
                     
                     Parallel.ForEach(ids, id => 
                     {
                         var doc = GetDocument(id);
                         if (doc != null && predicate(doc)) indexedResults.Add(doc);
                     });
                     return indexedResults;
                 }
             }

             // Full Scan with Parallelism
             string[] allIds;
             lock (_indexLock) { allIds = _primaryIndex.ToArray(); }

             var results = new ConcurrentBag<Dictionary<string, object>>();
             
             Parallel.ForEach(allIds, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, id => 
             {
                 var doc = GetDocument(id);
                 if (doc != null && predicate(doc))
                 {
                     results.Add(doc);
                 }
             });
             
             return results;
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

        public void Update(Func<Dictionary<string, object>, bool> predicate, Dictionary<string, object> updateFields)
        {
            // 1. Identify matches (Parallel Scan)
            var matches = FindAll(predicate);
            
            // 2. Perform Updates
            foreach (var doc in matches)
            {
                 // Update fields
                 foreach (var kvp in updateFields)
                 {
                     doc[kvp.Key] = kvp.Value;
                 }
                 // Save
                 Insert(doc); // Re-inserts/Updates cache & disk
            }
        }

        public void Delete(Func<Dictionary<string, object>, bool> predicate)
        {
            var matches = FindAll(predicate);
            
            foreach (var doc in matches)
            {
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
    }
}
