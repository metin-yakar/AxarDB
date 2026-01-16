using Jint;
using System.Collections.Concurrent;
using UnlockDB.Bridges;
using UnlockDB.Definitions;

namespace UnlockDB
{
    public class DatabaseEngine
    {
        private ConcurrentDictionary<string, Collection> _collections = new();
        private readonly UnlockDB.Storage.DiskStorage _storage;

        public DatabaseEngine()
        {
            _storage = new UnlockDB.Storage.DiskStorage("Data");
            
            // Create default system collection
            GetCollection("sysusers");
            // Add default user
            var sysusers = GetCollection("sysusers");
            // Check via storage if empty
            if (!sysusers.FindAll().Any())
            {
                sysusers.Insert(new Dictionary<string, object>
                {
                    { "username", "unlocker" },
                    { "password", "unlocker" }
                });
            }
        }

        public Collection GetCollection(string name)
        {
            return _collections.GetOrAdd(name, n => new Collection(n, _storage));
        }

        public object? ExecuteScript(string script, Dictionary<string, object>? parameters = null)
        {
            // Create a new engine for scope isolation
            var engine = new Engine(options => {
                 options.AllowClr();
            });

            // Set global parameters to prevent injection
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    engine.SetValue(param.Key, param.Value);
                }
            }

            // Expose 'db'
            var dbBridge = new UnlockDBBridge(this, engine);
            engine.SetValue("db", dbBridge);

            // Expose 'UnlockDB' constructor: new UnlockDB("name")
            engine.SetValue("UnlockDB", new Func<string, CollectionBridge>(name => {
                return new CollectionBridge(GetCollection(name), engine);
            }));

            // Expose 'showCollections'
            engine.SetValue("showCollections", new Func<List<string>>(() => {
                var list = _collections.Keys.ToList();
                // Also scan the Data folder for existing collections that might not be loaded yet
                var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (Directory.Exists(dataPath))
                {
                    var dirs = Directory.GetDirectories(dataPath).Select(Path.GetFileName).Where(n => n != null).Cast<string>();
                    foreach (var dir in dirs)
                    {
                        if (!list.Contains(dir)) list.Add(dir);
                    }
                }
                return list.OrderBy(x => x).ToList();
            }));

            engine.SetValue("getIndexes", new Func<string, object>((name) => {
                var col = GetCollection(name);
                return col.Indices.Select(i => new { PropertyName = i.PropertyName, Type = i.Type }).ToList();
            }));

            // Execute
            var result = engine.Evaluate(script);
            
            // Convert result back to native object if possible
            return result.ToObject();
        }

        public bool Authenticate(string username, string password)
        {
            // Simple check against hardcoded for now as per prompt or sysusers
            var sysusers = GetCollection("sysusers");
            return sysusers.FindAll(d => 
                d.ContainsKey("username") && d["username"].ToString() == username &&
                d.ContainsKey("password") && d["password"].ToString() == password
            ).Any();
        }
    }
}
