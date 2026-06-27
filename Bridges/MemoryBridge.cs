using System.Dynamic;
using Jint;

namespace AxarDB.Bridges
{
    /// <summary>
    /// Top-level bridge for the in-memory store.
    /// Exposed as "memory" in the script engine — just like "db" for persistent collections.
    /// Usage: memory.sessions.insert({...}), memory.sessions.findall().toList()
    /// </summary>
    public class MemoryBridge : DynamicObject
    {
        private readonly MemoryStore _store;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public MemoryBridge(MemoryStore store, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _store = store;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Intercepts property access like memory.sessions → returns MemoryCollectionBridge("sessions")
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = new MemoryCollectionBridge(_store, binder.Name, _engine, _cancellationToken);
            return true;
        }
    }
}
