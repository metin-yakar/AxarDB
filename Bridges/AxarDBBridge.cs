using System.Dynamic;
using AxarDB.Definitions;

namespace AxarDB.Bridges
{
    public class AxarDBBridge : DynamicObject
    {
        private readonly DatabaseEngine _dbEngine;
        private readonly Jint.Engine _jintEngine;

        public AxarDBBridge(DatabaseEngine dbEngine, Jint.Engine jintEngine)
        {
            _dbEngine = dbEngine;
            _jintEngine = jintEngine;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            // db.users -> returns CollectionBridge for "users"
            var collection = _dbEngine.GetCollection(binder.Name);
            result = new CollectionBridge(collection, _jintEngine);
            return true;
        }
        
        // Static join method simulation (instance method on db)
        public JoinCollectionBridge join(params object[] collections)
        {
             // For the sake of matching the "db.join" signature:
             var bridges = collections.OfType<CollectionBridge>().ToList();
             if (bridges.Count < 2) return new JoinCollectionBridge(new List<Dictionary<string, object>>());
             
             // Let's update CollectionBridge first to expose Data? Or just use findall().
             var allDocs = new List<Dictionary<string, object>>();
             
             foreach(var b in bridges)
             {
                 // We access the underlying collection directly to aggregate data.
                 // This implements a simple Union of all collections passed to join.
                 allDocs.AddRange(b._collection.FindAll());
             }

             return new JoinCollectionBridge(allDocs);
        }

        public bool addVault(string key, object value)
        {
            return _dbEngine.AddVault(key, value);
        }

        // View Methods
        public object? view(string name, object? parameters = null)
        {
            // Convert parameters to Dictionary<string, object>
            // Jint usually passes JsObject, we need to convert.
            Dictionary<string, object>? paramsDict = null;
            if (parameters is System.Collections.Generic.IDictionary<string, object> dict)
            {
                paramsDict = new Dictionary<string, object>(dict);
            }
            else if (parameters is System.Dynamic.ExpandoObject exp)
            {
                paramsDict = new Dictionary<string, object>(exp);
            }
            // If parameters is null or incompatible, we pass null.
            
            return _dbEngine.ExecuteView(name, paramsDict, "internal", "system");
        }

        public void saveView(string name, string content) => _dbEngine.SaveView(name, content);
        public void deleteView(string name) => _dbEngine.DeleteView(name);
        public List<string> listViews() => _dbEngine.ListViews();
        public string? getView(string name) => _dbEngine.GetViewContent(name);

        // Trigger Methods
        public void saveTrigger(string name, string target, string content) => _dbEngine.SaveTrigger(name, target, content);
        public void deleteTrigger(string name) => _dbEngine.DeleteTrigger(name);
        public List<string> listTriggers() => _dbEngine.ListTriggers();
        public string? getTrigger(string name) => _dbEngine.GetTriggerContent(name);
    }
}
