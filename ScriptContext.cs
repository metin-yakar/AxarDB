namespace AxarDB
{
    public struct ScriptContext
    {
        public string IpAddress { get; set; }
        public string User { get; set; }
        public bool IsView { get; set; }
        public string ViewName { get; set; }

        public static ScriptContext Default => new ScriptContext 
        { 
            IpAddress = "localhost", 
            User = "system", 
            IsView = false, 
            ViewName = "" 
        };
    }
}
