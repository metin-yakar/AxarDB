using System.Collections.Concurrent;
using UnlockDB.Storage;

namespace UnlockDB.Definitions
{
    public class Collection
    {
        public string Name { get; private set; }
        public List<IndexDefinition> Indices { get; set; } = new();
        private readonly DiskStorage _storage;

        public Collection(string name, DiskStorage storage)
        {
            Name = name;
            _storage = storage;
            _storage.EnsureCollection(Name);
            
            // Load existing indexes
            // Assuming "Data" base path for simplicity consistent with other parts
            var path = Path.Combine("Data", Name);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "idx_*.json"))
                {
                    var idx = IndexDefinition.Load(file);
                    if (idx != null)
                    {
                        Indices.Add(idx);
                    }
                }
            }
        }

        public void Insert(Dictionary<string, object> document)
        {
            if (!document.ContainsKey("_id"))
            {
                document["_id"] = Guid.NewGuid().ToString();
            }
            
            _storage.SaveDocument(Name, document);

            // Update Indexes
            foreach (var index in Indices)
            {
                index.IndexDocument(document);
                // In real app, maybe debounce save or save async
                index.Save(Path.Combine("Data", Name)); // Saving index structure
            }
        }

        public IEnumerable<Dictionary<string, object>> FindAll()
        {
            return _storage.StreamDocuments(Name);
        }

        public IEnumerable<Dictionary<string, object>> FindAll(Func<Dictionary<string, object>, bool> predicate, UnlockDB.Query.QueryOptimizer.AnalysisResult? analysis = null)
        {
             // If analyzed and index available, use it
             // We need the query string from Jint layer to analyze, 
             // but CollectionBridge calls this with a compiled delegate.
             // We'll overload FindAll to accept the raw predicate wrapper or handle analysis upstream.
             // For now, assume 'analysis' is passed if available.
             
             // Optimization: If analysis.prop has index
             if (analysis != null && analysis.Value.Prop != null)
             {
                 var idx = Indices.FirstOrDefault(i => i.PropertyName == analysis.Value.Prop);
                 if (idx != null)
                 {
                     var ids = idx.Search(analysis.Value.Val!, analysis.Value.Op);
                     foreach(var id in ids)
                     {
                         var doc = _storage.LoadDocument(Name, id);
                         if (doc != null && predicate(doc)) // Double check predicate to be safe
                             yield return doc;
                     }
                     yield break;
                 }
             }

             // Fallback to full scan
             foreach(var doc in _storage.StreamDocuments(Name))
             {
                 if (predicate(doc)) yield return doc;
             }
        }

        public void CreateIndex(string propertyName, string type)
        {
            var index = new IndexDefinition(propertyName, type);
            // Re-index existing data
            foreach (var doc in _storage.StreamDocuments(Name))
            {
                index.IndexDocument(doc);
            }
            Indices.Add(index);
            index.Save(Path.Combine("Data", Name));
        }

        public void Update(Func<Dictionary<string, object>, bool> predicate, Dictionary<string, object> updateFields)
        {
            var targets = FindAll(predicate).ToList(); // Must materialize to avoid modify-during-enumerate issues
            foreach (var doc in targets)
            {
                foreach (var kvp in updateFields)
                {
                    doc[kvp.Key] = kvp.Value;
                }
                Insert(doc); // Re-save (Overwrites because ID matches)
            }
        }

        public void Delete(Func<Dictionary<string, object>, bool> predicate)
        {
            var targets = FindAll(predicate).ToList();
            foreach (var doc in targets)
            {
                if (doc.TryGetValue("_id", out var id))
                {
                    _storage.DeleteDocument(Name, id.ToString()!);
                }
            }
            // Note: Indices need cleanup, relying on rebuild or smart remove later.
        }
    }
}

