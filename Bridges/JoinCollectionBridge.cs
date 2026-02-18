using AxarDB.Definitions;
using Jint;
using Jint.Native;
using AxarDB.Wrappers;

namespace AxarDB.Bridges
{
    public class JoinCollectionBridge
    { 
       private readonly IEnumerable<object> _joinedData;
       private readonly Engine? _engine;

       public JoinCollectionBridge(IEnumerable<object> joinedData, Engine? engine = null)
       {
            _joinedData = joinedData;
            _engine = engine;
       }

       public JoinCollectionBridge where(JsValue predicate)
       {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined()) return this;

            var filtered = _joinedData.Where(d => 
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try 
                    {
                        var result = _engine.Invoke(predicate, new object[] { d });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            });

            return new JoinCollectionBridge(filtered, _engine);
       }

       public JoinCollectionBridge select(JsValue selector)
       {
            if (selector == null || selector.IsNull() || selector.IsUndefined()) return this;

            var mapped = _joinedData.Select(d => 
            {
                if (_engine == null) return d;
                lock (_engine)
                {
                    try 
                    {
                        var result = _engine.Invoke(selector, new object[] { d });
                        return result.ToObject();
                    }
                    catch { return d; }
                }
            });

            return new JoinCollectionBridge(mapped, _engine);
       }

       public List<object> toList()
       {
           return _joinedData.ToList();
       }

       public List<object> ToList() => toList();

       public ResultSet findall(JsValue? predicate = null)
       {
            // Keeping for backward compatibility if needed, but chaining is now preferred.
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                 return new ResultSet(_joinedData.OfType<Dictionary<string, object>>()); 
            }
            
            var filtered = _joinedData.Where(d => 
            {
                if (_engine == null) return true;
                lock (_engine)
                {
                    try 
                    {
                        var result = _engine.Invoke(predicate, new object[] { d });
                        return result.AsBoolean();
                    }
                    catch { return false; }
                }
            });

            return new ResultSet(filtered.OfType<Dictionary<string, object>>());
       }
    }
}
