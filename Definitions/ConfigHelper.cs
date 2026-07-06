using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace AxarDB.Definitions
{
    public static class ConfigHelper
    {
        public static DatabaseSettings LoadSettings(string[] args)
        {
            var switchMappings = new Dictionary<string, string>
            {
                { "--memory-limit", "DatabaseSettings:MemoryLimitPercentage" },
                { "--bulk-cache-limit", "DatabaseSettings:BulkStoreMaxCacheBytes" },
                { "--max-recursion", "DatabaseSettings:MaxRecursionDepth" },
                { "--query-timeout", "DatabaseSettings:QueryTimeoutMinutes" },
                { "--queue-poll-seconds", "DatabaseSettings:QueuePollIntervalSeconds" }
            };

            // Filter args to only pass the configured switches and their values
            var filteredArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (switchMappings.ContainsKey(args[i]))
                {
                    filteredArgs.Add(args[i]);
                    if (i + 1 < args.Length)
                    {
                        filteredArgs.Add(args[i + 1]);
                        i++; // skip value
                    }
                }
                else if (args[i].StartsWith("--DatabaseSettings:", StringComparison.OrdinalIgnoreCase))
                {
                    filteredArgs.Add(args[i]);
                }
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(filteredArgs.ToArray(), switchMappings);

            var config = builder.Build();
            var settings = new DatabaseSettings();
            config.GetSection("DatabaseSettings").Bind(settings);
            return settings;
        }
    }
}
