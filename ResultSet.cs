using AxarDB.Wrappers;
using System.Collections;

namespace AxarDB
{
    public class ResultSet : IEnumerable<DocumentWrapper>
    {
        private readonly List<DocumentWrapper> _results;
        private readonly AxarDB.Definitions.Collection? _collection; 

        public ResultSet(IEnumerable<Dictionary<string, object>> source, AxarDB.Definitions.Collection? collection = null) 
        {
            _results = source.Select(d => new DocumentWrapper(d)).ToList();
            _collection = collection;
        }

        public IEnumerator<DocumentWrapper> GetEnumerator() => _results.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _results.GetEnumerator();

        public List<DocumentWrapper> ToList() => _results;
        public List<DocumentWrapper> toList() => _results;

        public DocumentWrapper? first()
        {
            return _results.FirstOrDefault();
        }

        public ResultSet take(int count)
        {
            var taken = _results.Take(count).Select(w => w.Data);
            return new ResultSet(taken, _collection);
        }

        public AxarList select(Func<object, object> selector)
        {
            var list = _results.Select(d => selector(d));
            return new AxarList(list);
        }

        public void update(object updateFields)
        {
            if (_collection == null || updateFields == null) return;
            
            Dictionary<string, object>? fields = null;
            if (updateFields is Dictionary<string, object> d) fields = d;
            else if (updateFields is IDictionary<string, object> id) fields = new Dictionary<string, object>(id);
            else if (updateFields is System.Dynamic.ExpandoObject ex) fields = new Dictionary<string, object>(ex);
            
            if (fields == null) return;

            foreach(var wrapper in _results)
            {
                var doc = wrapper.Data;
                foreach(var kv in fields)
                {
                    doc[kv.Key] = kv.Value;
                }
                _collection.Insert(doc);
            }
        }

        public void delete()
        {
            if (_collection == null) return;
            
            var idsToDelete = _results
                .Select(d => d.Data.TryGetValue("_id", out var id) ? id.ToString() : null)
                .Where(x => x != null)
                .Cast<string>()
                .ToList();
            
            if (idsToDelete.Count == 0) return;

            _collection.Delete(d => d.TryGetValue("_id", out var id) && idsToDelete.Contains(id.ToString()!));
            
            _results.Clear();
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
           foreach (var item in _results) action(item);
        }

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return _results.Count;
            return _results.Count(w => predicate(w.Data));
        }

        public AxarList distinct(Func<object, object>? selector = null)
        {
            if (selector == null) return new AxarList(_results.Select(w => (object)w.Data).Distinct());
            return new AxarList(_results.Select(w => selector(w.Data)).Distinct());
        }
    }
}
