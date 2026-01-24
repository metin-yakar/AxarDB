using AxarDB.Wrappers;

namespace AxarDB
{
    public class ResultSet
    {
        private readonly IEnumerable<Dictionary<string, object>> _source;
        private readonly AxarDB.Definitions.Collection? _collection; // Reference to update/delete

        public ResultSet(IEnumerable<Dictionary<string, object>> source, AxarDB.Definitions.Collection? collection = null)
        {
            _source = source;
            _collection = collection;
        }

        public List<DocumentWrapper> ToList()
        {
            return _source.Select(d => new DocumentWrapper(d)).ToList();
        }

        public DocumentWrapper? first()
        {
            var doc = _source.FirstOrDefault();
            return doc != null ? new DocumentWrapper(doc) : null;
        }

        public ResultSet take(int count)
        {
            return new ResultSet(_source.Take(count), _collection);
        }

        public List<object> select(Func<object, object> selector)
        {
            return _source.Select(d => selector(new DocumentWrapper(d))).ToList();
        }

        public void update(object updateFields)
        {
            if (_collection == null || updateFields == null) return;

            // Handle Jint's object conversion
            Dictionary<string, object>? fields = null;
            if (updateFields is Dictionary<string, object> d) fields = d;
            else if (updateFields is IDictionary<string, object> id) fields = new Dictionary<string, object>(id);
            else if (updateFields is System.Dynamic.ExpandoObject ex) fields = new Dictionary<string, object>(ex);
            
            if (fields == null) return;

            foreach(var doc in _source)
            {
                foreach(var kv in fields)
                {
                    doc[kv.Key] = kv.Value;
                }
                _collection.Insert(doc); // Save & index
            }
        }

        public void delete()
        {
            if (_collection == null) return;
            
            // Collect IDs to delete
            var idsToDelete = _source
                .Select(d => d.TryGetValue("_id", out var id) ? id.ToString() : null)
                .Where(x => x != null)
                .Cast<string>()
                .ToList();
            
            if (idsToDelete.Count == 0) return;

            // Efficient and safe deletion predicate
            _collection.Delete(d => d.TryGetValue("_id", out var id) && idsToDelete.Contains(id.ToString()!));
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
            foreach (var doc in _source)
            {
                action(new DocumentWrapper(doc));
            }
        }

        public int Count()
        {
            return _source.Count();
        }
    }
}
