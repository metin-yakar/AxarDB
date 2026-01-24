using System.Dynamic;
using Jint;
using Jint.Native;

namespace AxarDB.Wrappers
{
    public class DocumentWrapper : DynamicObject
    {
        private readonly Dictionary<string, object> _document;

        public DocumentWrapper(Dictionary<string, object> document)
        {
            _document = document;
        }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, object> Data => _document;

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            if (_document.TryGetValue(binder.Name, out var value))
            {
                result = value;
                return true;
            }
            result = null;
            return true; // Return null instead of throwing for missing properties? Or false? JS usually returns undefined.
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (value != null)
                _document[binder.Name] = value;
            return true;
        }

        public object? this[string key]
        {
            get => _document.ContainsKey(key) ? _document[key] : null;
            set { if (value != null) _document[key] = value; }
        }

        public object? get(string name)
        {
            return _document.ContainsKey(name) ? _document[name] : null;
        }
    }
}
