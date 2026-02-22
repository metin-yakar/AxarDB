using System.Collections.Generic;

namespace AxarDB.Wrappers
{
    public class AxarList : List<object>
    {
        public AxarList() : base() { }
        public AxarList(IEnumerable<object> collection) : base(collection) { }

        public AxarList ToList() => this;
        public AxarList toList() => this;

        public int count(Func<object, bool>? predicate = null)
        {
            if (predicate == null) return this.Count;
            return this.Count(predicate);
        }

        public AxarList distinct(Func<object, object>? selector = null)
        {
            if (selector == null) return new AxarList(this.Distinct());
            return new AxarList(this.Select(selector).Distinct());
        }
    }
}
