using AxarDB.Definitions;

namespace AxarDB.Bridges
{
    public class JoinCollectionBridge
    { 
       private IEnumerable<Dictionary<string, object>> _joinedData;

       public JoinCollectionBridge(IEnumerable<Dictionary<string, object>> joinedData)
       {
            _joinedData = joinedData;
       }

       public ResultSet findall(Func<object, bool>? predicate = null)
       {
            if (predicate == null)
            {
                 return new ResultSet(_joinedData); 
            }
            // Filter
            // This requires converting to wrapper to pass to JS predicate
            var filtered = _joinedData.Where(d => 
            {
                // Note: Predicate comes from Jint, it expects a dynamic object or wrapper.
                // However, bridging Func<object, bool> from Jint is tricky directly on Dictionary.
                // We'll see how CollectionBridge does it.
                // Assuming the predicate is a delegate that takes a DocumentWrapper.
                return predicate(new Wrappers.DocumentWrapper(d));
            });

            return new ResultSet(filtered);
       }
    }
}
