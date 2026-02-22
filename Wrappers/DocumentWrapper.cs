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
                result = Unwrap(value);
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
            get => _document.ContainsKey(key) ? Unwrap(_document[key]) : null;
            set { if (value != null) _document[key] = value; }
        }

        public object? get(string name)
        {
            return _document.ContainsKey(name) ? Unwrap(_document[name]) : null;
        }

        private object? Unwrap(object? value)
        {
            if (value is System.Text.Json.JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.True: return true;
                    case System.Text.Json.JsonValueKind.False: return false;
                    case System.Text.Json.JsonValueKind.Number:
                        if (element.TryGetInt32(out var i)) return i;
                        if (element.TryGetInt64(out var l)) return l;
                        if (element.TryGetDouble(out var d)) return d;
                        return element.ToString();
                    case System.Text.Json.JsonValueKind.String: return element.GetString();
                    case System.Text.Json.JsonValueKind.Null: return null;
                    case System.Text.Json.JsonValueKind.Array:
                        // Recursively unwrap arrays? 
                        // For now let's just return list of objects if needed, or keep as is?
                        // Scripts might iterate. Jint handles IEnumerable.
                        return element.EnumerateArray().Select(x => Unwrap(x)).ToArray();
                    case System.Text.Json.JsonValueKind.Object:
                         // Convert to Dictionary for nested usage?
                         var dict = new Dictionary<string, object>();
                         foreach(var prop in element.EnumerateObject())
                         {
                             dict[prop.Name] = Unwrap(prop.Value)!;
                         }
                         return new DocumentWrapper(dict);
                    default: return element.ToString();
                }
            }
            return value;
        }
    }
}
