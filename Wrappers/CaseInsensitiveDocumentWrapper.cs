using System.Dynamic;

namespace AxarDB.Wrappers
{
    public class CaseInsensitiveDocumentWrapper : DynamicObject
    {
        private readonly Dictionary<string, object> _document;

        public CaseInsensitiveDocumentWrapper(Dictionary<string, object> document)
        {
            _document = document;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            if (_document.TryGetValue(binder.Name, out var val))
            {
                if (val is string strVal)
                {
                    result = strVal.ToLowerInvariant();
                    return true;
                }
                result = val;
                return true;
            }
            result = null; // or basic null
            return true;
        }
    }
}
