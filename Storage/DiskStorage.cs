using System.Collections.Concurrent;
using System.Text.Json;

namespace UnlockDB.Storage
{
    public class DiskStorage
    {
        private readonly string _basePath;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

        public DiskStorage(string basePath)
        {
            _basePath = basePath;
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public void EnsureCollection(string collectionName)
        {
            var path = Path.Combine(_basePath, collectionName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void SaveDocument(string collectionName, Dictionary<string, object> document)
        {
            if (!document.ContainsKey("_id")) return;
            string id = document["_id"].ToString()!;
            
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            var json = JsonSerializer.Serialize(document, _jsonOptions);
            File.WriteAllText(path, json);
        }

        public Dictionary<string, object>? LoadDocument(string collectionName, string id)
        {
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }

        public void DeleteDocument(string collectionName, string id)
        {
            var path = Path.Combine(_basePath, collectionName, $"{id}.json");
            if (File.Exists(path))
                File.Delete(path);
        }

        // Lazy loading of documents
        public IEnumerable<Dictionary<string, object>> StreamDocuments(string collectionName)
        {
            var path = Path.Combine(_basePath, collectionName);
            if (!Directory.Exists(path)) yield break;

            foreach (var file in Directory.EnumerateFiles(path, "*.json"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("idx_")) continue;

                Dictionary<string, object>? doc = null;
                try 
                {
                    // Basic read - optimization: could use FileStream + System.Text.Json.DeserializeAsync
                    var json = File.ReadAllText(file);
                    doc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }
                catch { /* corrupted file? skip */ }

                if (doc != null) yield return doc;
            }
        }
        
        public IEnumerable<string> GetAllDocumentIds(string collectionName)
        {
             var path = Path.Combine(_basePath, collectionName);
             if (!Directory.Exists(path)) yield break;
             
             foreach(var file in Directory.EnumerateFiles(path, "*.json"))
             {
                 yield return Path.GetFileNameWithoutExtension(file);
             }
        }
    }
}
