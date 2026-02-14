using System.Collections.Generic;

namespace AxarDB.Wrappers
{
    public class AxarList : List<object>
    {
        public AxarList() : base() { }
        public AxarList(IEnumerable<object> collection) : base(collection) { }

        public AxarList ToList() => this;
        public AxarList toList() => this;
    }
}
