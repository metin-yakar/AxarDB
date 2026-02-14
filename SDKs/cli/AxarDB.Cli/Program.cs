using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AxarDB.Sdk;

namespace AxarDB.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var options = ParseArguments(args);

                if (options.ShowHelp)
                {
                    ShowHelp();
                    return;
                }

                EnsureAuth(options);

                if (options.ShowCollections)
                {
                    using var showClient = new AxarClient(options.Host, options.User, options.Password);
                    var collections = await showClient.ShowCollectionsAsync();
                    Console.WriteLine(JsonSerializer.Serialize(collections, new JsonSerializerOptions { WriteIndented = true }));
                    return;
                }

                if (!string.IsNullOrEmpty(options.InsertCollection) && !string.IsNullOrEmpty(options.InsertData))
                {
                    using var insertClient = new AxarClient(options.Host, options.User, options.Password);
                    // Use dynamic/object to allow arbitrary JSON
                    // We need to deserialize the input string to an object to pass to InsertAsync, or just pass it if it accepts string?
                    // SDK InsertAsync takes T document. 
                    // Let's use JsonElement.
                    try 
                    {
                        var doc = JsonSerializer.Deserialize<JsonElement>(options.InsertData);
                        var insertResult = await insertClient.InsertAsync(options.InsertCollection, (object)doc);
                        Console.WriteLine(JsonSerializer.Serialize(insertResult, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return;
                }

                if (!string.IsNullOrEmpty(options.SelectCollection) && !string.IsNullOrEmpty(options.SelectSelector))
                {
                    using var selectClient = new AxarClient(options.Host, options.User, options.Password);
                    // SelectAsync returns List<TResult>. We don't know TResult, use object.
                    var selectResult = await selectClient.Collection<object>(options.SelectCollection)
                                             .SelectAsync<object>(options.SelectSelector);
                    Console.WriteLine(JsonSerializer.Serialize(selectResult, new JsonSerializerOptions { WriteIndented = true }));
                    return;
                }

                string script = null;
                if (!string.IsNullOrEmpty(options.ScriptFile))
                {
                    if (!File.Exists(options.ScriptFile))
                    {
                        Console.WriteLine($"Error: Script file not found: {options.ScriptFile}");
                        return;
                    }
                    script = File.ReadAllText(options.ScriptFile);
                }
                else if (!string.IsNullOrEmpty(options.InlineScript))
                {
                    script = options.InlineScript;
                }
                else
                {
                    Console.WriteLine("Error: No script provided. Use -f <file> or -s <script>.");
                    ShowHelp();
                    return;
                }

                using var client = new AxarClient(options.Host, options.User, options.Password);
                
                // We use object to get raw JSON result (deserialized to JsonElement/Node effectively by System.Text.Json)
                // AxarClient QueryAsync<object> returns dynamic/object which is usually a JsonElement in System.Text.Json
                var queryResult = await client.QueryAsync<object>(script);

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var jsonOutput = JsonSerializer.Serialize(queryResult, jsonOptions);

                if (!string.IsNullOrEmpty(options.OutputFile))
                {
                    File.WriteAllText(options.OutputFile, jsonOutput);
                    Console.WriteLine($"Result written to {options.OutputFile}");
                }
                else
                {
                    Console.WriteLine(jsonOutput);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void EnsureAuth(Options options)
        {
            if (string.IsNullOrEmpty(options.Host))
            {
                Console.Write("Host URL (default: http://localhost:5000): ");
                var input = Console.ReadLine();
                options.Host = string.IsNullOrWhiteSpace(input) ? "http://localhost:5000" : input;
            }

            if (string.IsNullOrEmpty(options.User))
            {
                Console.Write("Username: ");
                options.User = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(options.Password))
            {
                Console.Write("Password: ");
                options.Password = ReadPassword();
                Console.WriteLine();
            }
        }

        static string ReadPassword()
        {
            string pass = "";
            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass = pass.Substring(0, (pass.Length - 1));
                    Console.Write("\b \b");
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
            } while (true);
            return pass;
        }

        static Options ParseArguments(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--host":
                    case "-h":
                        if (i + 1 < args.Length) options.Host = args[++i];
                        break;
                    case "--user":
                    case "-u":
                        if (i + 1 < args.Length) options.User = args[++i];
                        break;
                    case "--pass":
                    case "-p":
                        if (i + 1 < args.Length) options.Password = args[++i];
                        break;
                    case "--file":
                    case "-f":
                        if (i + 1 < args.Length) options.ScriptFile = args[++i];
                        break;
                    case "--out":
                    case "-o":
                        if (i + 1 < args.Length) options.OutputFile = args[++i];
                        break;
                     case "--script":
                    case "-s":
                        if (i + 1 < args.Length) options.InlineScript = args[++i];
                        break;
                    case "--show-collections":
                        options.ShowCollections = true;
                        break;
                    case "--insert":
                        if (i + 2 < args.Length)
                        {
                            options.InsertCollection = args[++i];
                            options.InsertData = args[++i];
                        }
                        else
                        {
                            Console.WriteLine("Error: --insert requires <collection> <json_data>");
                            return new Options { ShowHelp = true };
                        }
                        break;
                    case "--select":
                        if (i + 2 < args.Length)
                        {
                            options.SelectCollection = args[++i];
                            options.SelectSelector = args[++i];
                        }
                        else
                        {
                            Console.WriteLine("Error: --select requires <collection> <selector>");
                            return new Options { ShowHelp = true };
                        }
                        break;
                    case "--help":
                        options.ShowHelp = true;
                        break;
                }
            }
            return options;
        }

        static void ShowHelp()
        {
            Console.WriteLine("AxarDB CLI Tool");
            Console.WriteLine("Usage: AxarDB.Cli [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --host <url>       AxarDB Server URL (default: http://localhost:5000)");
            Console.WriteLine("  -u, --user <user>      Username");
            Console.WriteLine("  -p, --pass <pass>      Password");
            Console.WriteLine("  -f, --file <path>      Path to script file");
            Console.WriteLine("  -s, --script <code >   Inline script code");
            Console.WriteLine("  -o, --out <path>       Output file for results");
            Console.WriteLine("  --show-collections     Show all collections");
            Console.WriteLine("  --insert <col> <json>  Insert document into collection");
            Console.WriteLine("  --select <col> <sel>   Select/Project from collection (e.g. \"x => x.name\")");
            Console.WriteLine("  --help                 Show this help message");
            Console.WriteLine();
            Console.WriteLine("If host, user, or password are not provided, interactive mode starts.");
        }

        class Options
        {
            public string Host { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string ScriptFile { get; set; }
            public string InlineScript { get; set; }
            public string OutputFile { get; set; }
            public bool ShowHelp { get; set; }
            public bool ShowCollections { get; set; }
            public string InsertCollection { get; set; }
            public string InsertData { get; set; }
            public string SelectCollection { get; set; }
            public string SelectSelector { get; set; }
        }
    }
}
