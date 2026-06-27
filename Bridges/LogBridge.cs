using System.Dynamic;
using Jint;

namespace AxarDB.Bridges
{
    public class LogBridge : DynamicObject
    {
        private readonly string _basePath;
        private readonly Engine? _engine;
        private readonly CancellationToken _cancellationToken;

        public LogBridge(string basePath, Engine? engine = null, CancellationToken cancellationToken = default)
        {
            _basePath = basePath;
            _engine = engine;
            _cancellationToken = cancellationToken;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = new LogCollectionBridge(_basePath, binder.Name.ToLower(), _engine, _cancellationToken);
            return true;
        }
    }
}
