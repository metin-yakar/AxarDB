using Jint;
using Jint.Native;
using AxarDB.Definitions;
using AxarDB.Wrappers;

namespace AxarDB.Bridges
{
    public class CollectionBridge
    {
        public readonly Collection _collection;
        private readonly Engine? _engine;

        public CollectionBridge(Collection collection, Engine? engine = null)
        {
            _collection = collection;
            _engine = engine;
        }

        public ResultSet findall(Jint.Native.JsValue? predicate = null)
        {
            if (predicate == null || predicate.IsNull() || predicate.IsUndefined())
            {
                return new ResultSet(_collection.FindAll(), _collection);
            }
            
            // Analyze Predicate
            AxarDB.Query.QueryOptimizer.AnalysisResult? analysis = null;
            var optimized = AxarDB.Query.QueryOptimizer.AnalyzePredicate(predicate);
            if (optimized != null)
            {
                analysis = new AxarDB.Query.QueryOptimizer.AnalysisResult 
                { 
                    Prop = optimized.Value.prop!, 
                    Val = optimized.Value.val!, 
                    Op = optimized.Value.op 
                };
            }

            Func<Dictionary<string, object>, bool> csPredicate = (d) => 
            {
                 // We execute the JS function using the Engine.
                 // We need to pass the document as argument.
                 if (_engine == null) return true; // Fallback?

                 lock (_engine)
                 {
                     try
                     {
                        var result = _engine.Invoke(predicate, new object[] { new DocumentWrapper(d) });
                        return result.AsBoolean();
                     }
                     catch { return false; }
                 }
            };

            var results = _collection.FindAll(csPredicate, analysis);
            return new ResultSet(results, _collection);
        }

        public DocumentWrapper? find(Func<object, bool> predicate)
        {
             var doc = _collection.FindAll(d => predicate(new DocumentWrapper(d))).FirstOrDefault();
             return doc != null ? new DocumentWrapper(doc) : null;
        }

        public void update(Jint.Native.JsValue predicate, object updateFields)
        {
             var resultSet = findall(predicate);
             resultSet.update(updateFields);
        }

        public AxarList select(Func<object, object> selector)
        {
             var list = _collection.FindAll().Select(d => selector(new DocumentWrapper(d)));
             return new AxarList(list);
        }

        public void insert(object docObj)
        {
            var dict = ConvertToDictionary(docObj);
            if (dict != null)
            {
                _collection.Insert(dict);
            }
        }
        
        public void index(Func<object, object> propertySelector, string type)
        {
             var tracer = new IndexTracer();
             try 
             {
                 propertySelector(tracer);
             } 
             catch { }
             
             if (!string.IsNullOrEmpty(tracer.TracedProperty))
             {
                 _collection.CreateIndex(tracer.TracedProperty, type);
             }
        }

        public void index(string propertyName, string type)
        {
            _collection.CreateIndex(propertyName, type);
        }

        public ResultSet contains(Func<object, bool> predicate)
        {
            var results = _collection.FindAll(d => predicate(new CaseInsensitiveDocumentWrapper(d)));
            return new ResultSet(results, _collection);
        }

        public bool exists(Func<object, bool> predicate)
        {
            return _collection.FindAll(d => predicate(new DocumentWrapper(d))).Any();
        }

        private Dictionary<string, object>? ConvertToDictionary(object? obj)
        {
            if (obj == null) return null;
            if (obj is Dictionary<string, object> dict) return dict;
            if (obj is IDictionary<string, object> idict) return new Dictionary<string, object>(idict);
            if (obj is System.Dynamic.ExpandoObject expando)
            {
                return new Dictionary<string, object>(expando);
            }
            return null;
        }
    }
}
