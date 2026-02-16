using AxarDB.Wrappers;

namespace AxarDB
{
    public class ResultSet : List<DocumentWrapper>
    {
        private readonly AxarDB.Definitions.Collection? _collection; // Reference to update/delete

        public ResultSet(IEnumerable<Dictionary<string, object>> source, AxarDB.Definitions.Collection? collection = null) 
            : base(source.Select(d => new DocumentWrapper(d)))
        {
            _collection = collection;
        }

        public List<DocumentWrapper> ToList() => this;
        public List<DocumentWrapper> toList() => this;

        public DocumentWrapper? first()
        {
            return this.FirstOrDefault();
        }

        public ResultSet take(int count)
        {
            // Create a new ResultSet from the taken items
            var taken = this.Take(count).Select(w => w.Data);
            return new ResultSet(taken, _collection);
        }

        public AxarList select(Func<object, object> selector)
        {
            var list = this.Select(d => selector(d));
            return new AxarList(list);
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

            foreach(var wrapper in this)
            {
                var doc = wrapper.Data;
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
            var idsToDelete = this
                .Select(d => d.Data.TryGetValue("_id", out var id) ? id.ToString() : null)
                .Where(x => x != null)
                .Cast<string>()
                .ToList();
            
            if (idsToDelete.Count == 0) return;

            // Efficient and safe deletion predicate
            _collection.Delete(d => d.TryGetValue("_id", out var id) && idsToDelete.Contains(id.ToString()!));
            
            this.Clear();
        }

        public void @foreach(Action<DocumentWrapper> action)
        {
           foreach (var item in this) action(item);
        }

        
        public new int Count()
        {
            return base.Count;
        }
    }
}
