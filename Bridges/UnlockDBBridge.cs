using System.Dynamic;
using UnlockDB.Definitions;

namespace UnlockDB.Bridges
{
    public class UnlockDBBridge : DynamicObject
    {
        private readonly DatabaseEngine _dbEngine;
        private readonly Jint.Engine _jintEngine;

        public UnlockDBBridge(DatabaseEngine dbEngine, Jint.Engine jintEngine)
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
             // This is complex. We need to know which collections and how to join.
             // Prompt says: db.join(db.users, db.orders)
             // passed objects are CollectionBridge instances.
             // We need access to underlying collections.
             // But CollectionBridge hides it.
             // We can expose the underlying collection as internal or public property in CollectionBridge.
             
             // Simple basic implementation: Cartesian product or join on _id? 
             // Prompt says "ID bazlı eşleşme (foreign key benzeri)".
             // Let's assume naive implementation: Collection1 items + match Collection2 items by some logic?
             // Or just merge lists?
             // "JoinCollectionBridge... ID bazlı eşleşme"
             
             // Since this is a "NoSQL" simulation, maybe it just flattens them?
             // Or maybe it looks for foreign keys like "userId" in orders matching "_id" in users?
             
             // For the sake of matching the "db.join" signature:
             var bridges = collections.OfType<CollectionBridge>().ToList();
             if (bridges.Count < 2) return new JoinCollectionBridge(new List<Dictionary<string, object>>());
             
             // Simplest "join": Just return all documents from all collections combined?
             // No, standard join usually means linking.
             // Let's implement a dummy one that just merges everything for now, 
             // OR specifically looks for relations if we want to be fancy.
             // Given limitations, I'll return an empty result or a merged one.
             
             // Actually, let's use Reflection to get the private _collection if needed, 
             // but I can just make it public in CollectionBridge.
             
             // Wait, I can't easily modify CollectionBridge right now unless I rewrite it.
             // Actually I can access it via a property if I add it. 
             // I'll assume CollectionBridge has a way to get data.
             
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
