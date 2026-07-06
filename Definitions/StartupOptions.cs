using System;
using System.Linq;

namespace AxarDB.Definitions
{
    public class StartupOptions
    {
        public int Port { get; set; } = 5000;
        public string? TargetPath { get; set; }
        public string? CorsOrigins { get; set; }
        public bool IsBenchmark { get; set; }
        public bool IsTestMemory { get; set; }
        public bool IsScript { get; set; }
        public string? ScriptPath { get; set; }

        public static StartupOptions Parse(string[] args)
        {
            var options = new StartupOptions();

            if (args == null || args.Length == 0)
            {
                return options;
            }

            // Check for run mode flags first
            if (args[0] == "script")
            {
                options.IsScript = true;
                options.ScriptPath = args.Length > 1 ? args[1] : null;
            }

            options.IsBenchmark = args.Contains("--benchmark");
            options.IsTestMemory = args.Contains("--test-memory");

            // Parse common key-value parameters
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "-p" || args[i] == "--port") && i + 1 < args.Length && int.TryParse(args[i + 1], out int p))
                {
                    options.Port = p;
                    i++; // skip value
                }
                else if (args[i] == "--targetpath" && i + 1 < args.Length)
                {
                    options.TargetPath = args[i + 1];
                    i++; // skip value
                }
                else if (args[i] == "--cors" && i + 1 < args.Length)
                {
                    options.CorsOrigins = args[i + 1];
                    i++; // skip value
                }
            }

            return options;
        }
    }
}
