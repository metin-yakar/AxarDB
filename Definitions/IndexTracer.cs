using System.Dynamic;

namespace AxarDB.Definitions
{
    public class IndexTracer : DynamicObject
    {
        public string? TracedProperty { get; private set; }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            TracedProperty = binder.Name;
            result = this; // Return self to allow chaining if needed, though usually just one level
            return true;
        }
    }
}
