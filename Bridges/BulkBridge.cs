using System.Dynamic;
using Jint;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Top-level bridge for JSONL bulk collections.
    /// Exposed as "bulk" in the script engine — like "db" and "memory".
    /// Usage: bulk.countries.findall(), bulk.postalcodes.insert([...])
    /// Special: bulk.reload() reloads all collections from disk.
    /// </summary>
    public class BulkBridge : DynamicObject
    {
        private readonly BulkStore _store;
        private readonly Engine? _engine;

        public BulkBridge(BulkStore store, Engine? engine = null)
        {
            _store = store;
            _engine = engine;
        }

        /// <summary>
        /// Intercepts property access: bulk.countries → BulkCollectionBridge("countries")
        /// Special: bulk.reload → action to reload all collections
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            if (binder.Name == "reload")
            {
                result = new Action(() => _store.ReloadAll());
                return true;
            }

            result = new BulkCollectionBridge(_store, binder.Name, _engine);
            return true;
        }

        /// <summary>
        /// Allow: bulk.reload() as method call
        /// </summary>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            if (binder.Name == "reload")
            {
                if (args?.Length > 0 && args[0] is string collName)
                    _store.Reload(collName);
                else
                    _store.ReloadAll();
                result = null;
                return true;
            }

            result = null;
            return false;
        }
    }
}
