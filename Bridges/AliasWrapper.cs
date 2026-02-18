namespace AxarDB.Bridges
{
    public class AliasWrapper
    {
        public object Source { get; set; }
        public string Name { get; set; }

        public AliasWrapper(object source, string name)
        {
            Source = source;
            Name = name;
        }
    }
}
