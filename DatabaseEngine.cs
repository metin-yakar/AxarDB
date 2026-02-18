using Jint;
using System.Collections.Concurrent;
using AxarDB.Bridges;
using AxarDB.Definitions;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MySqlConnector;
using Npgsql;

namespace AxarDB
{
    public class DatabaseEngine
    {
        private ConcurrentDictionary<string, Collection> _collections = new();
        private readonly AxarDB.Storage.DiskStorage _storage;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _sharedCache;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _basePath;

        private string FormatLog(object o)
        {
            if (o == null) return "null";
            if (o is string s) return s;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.None, // Compact JSON as requested
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = new List<JsonConverter> 
                    { 
                        new Newtonsoft.Json.Converters.ExpandoObjectConverter(),
                        new Newtonsoft.Json.Converters.StringEnumConverter()
                    }
                };
                return JsonConvert.SerializeObject(o, settings);
            }
            catch
            {
                return o.ToString();
            }
        }

        private object PerformHttpRequest(string method, string url, object? data, object? headers)
        {
            try 
            {
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), url);

                if (data != null && (method == "POST" || method == "PUT"))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                }

                // Handle Headers
                if (headers != null)
                {
                    if (headers is IDictionary<string, object> headerDict)
                    {
                        foreach (var kvp in headerDict)
                        {
                            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value?.ToString());
                        }
                    }
                    else if (headers is System.Collections.IEnumerable headerList)
                    {
                        foreach (var item in headerList)
                        {
                             if (item is IDictionary<string, object> dict)
                             {
                                 foreach (var kvp in dict) 
                                     request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value?.ToString());
                             }
                        }
                    }
                }

                var response = _httpClient.SendAsync(request).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;
                
                object? responseData;
                try {
                     responseData = System.Text.Json.JsonSerializer.Deserialize<object>(responseString);
                } catch {
                     responseData = responseString;
                }

                return new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode, data = responseData };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }


        private void LogMySqlRequest(ScriptContext context, string connectionString, string query, object? parameters, long durationMs, bool success, string error = null)
        {
             try 
             {
                 var logEntry = new 
                 {
                     timestamp = DateTime.UtcNow,
                     ip = context.IpAddress,
                     user = context.User,
                     type = context.IsView ? "view_mysql" : "script_mysql",
                     view = context.IsView ? context.ViewName : null,
                     connection = connectionString, // Maybe hide password? User didn't specify. Assuming raw for now as per "standard".
                     query,
                     parameters,
                     durationMs,
                     success,
                     error
                 };
                 
                 var json = System.Text.Json.JsonSerializer.Serialize(logEntry);
                 var path = Path.Combine(_basePath, "request_logs", $"{DateTime.UtcNow:yyyy-MM-dd}_mysql.log"); // Separate or same? User said "standard log types".
                 
                 //FOR AI TESTING PURPOSES
                 // Actually Logger.LogRequest format is specific pipe-delimited. 
                 // User requested "request_log, error_log... standard log types".
                 // Let's stick to existing Logger class structure OR extend it.
                 // Given the complexity of SQL logs, JSON might be better but let's try to fit into Logger.
                 // However, "request_log" usually implies HTTP requests in this project. 
                 // The prompt says "request_log, error_log... detayları ilgili log tiplerinin standardına uygun olarak kayda düşmelidir".
                 // I will use a new method in Logger or just append to a dedicated mysql log file to avoid polluting HTTP logs 
                 // OR I simply use Logger.LogRequest if it fits? 
                 // Logger.LogRequest takes (ip, user, json, duration, success).
                 
                 AxarDB.Logging.Logger.LogRequest(context.IpAddress, context.User, $"[MySQL] {query}", durationMs, success, error);
                 
                 
                 if (!success && !string.IsNullOrEmpty(error))
                 {
                     AxarDB.Logging.Logger.LogError($"[MySQL Error] {error} | Query: {query}");
                 }
             }
             catch {}
        }

        private object MySqlRead(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try 
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                if (parameters != null)
                {
                     var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                     var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                     if (dict != null)
                     {
                         foreach(var kvp in dict)
                         {
                             command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);
                         }
                     }
                }
                
                using var reader = command.ExecuteReader();
                var results = new List<Dictionary<string, object>>();
                
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i);
                        if (val == DBNull.Value) val = null;
                        row[reader.GetName(i)] = val;
                    }
                    results.Add(row);
                }
                
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, ex.Message);
                throw new Exception($"MySQL Exec Failed: {ex.Message}");
            }
        }

        private int MySqlExec(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try 
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                if (parameters != null)
                {
                     var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                     var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                     if (dict != null)
                     {
                         foreach(var kvp in dict)
                         {
                             command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);
                         }
                     }
                }
                
                int affected = command.ExecuteNonQuery();
                
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return affected;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogMySqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, ex.Message);
                throw new Exception($"MySQL Exec Failed: {ex.Message}");
            }
        }

        private void RegisterUtils(Engine engine, ScriptContext context)
        {
            // --- Utility Functions ---
            engine.SetValue("md5", new Func<string, string>(AxarDB.Helpers.ScriptUtils.MD5));
            engine.SetValue("sha256", new Func<string, string>(AxarDB.Helpers.ScriptUtils.SHA256));
            engine.SetValue("toString", new Func<object, string>(AxarDB.Helpers.ScriptUtils.ToString));
            engine.SetValue("randomNumber", new Func<int, int, int>(AxarDB.Helpers.ScriptUtils.RandomNumber));
            engine.SetValue("randomDecimal", new Func<string, string, decimal>(AxarDB.Helpers.ScriptUtils.RandomDecimal));
            engine.SetValue("randomString", new Func<int, string>(AxarDB.Helpers.ScriptUtils.RandomString));
            engine.SetValue("toBase64", new Func<string, string>(AxarDB.Helpers.ScriptUtils.ToBase64));
            engine.SetValue("fromBase64", new Func<string, string>(AxarDB.Helpers.ScriptUtils.FromBase64));
            engine.SetValue("encrypt", new Func<string, string, string>(AxarDB.Helpers.ScriptUtils.Encrypt));
            engine.SetValue("decrypt", new Func<string, string, string>(AxarDB.Helpers.ScriptUtils.Decrypt));
            engine.SetValue("split", new Func<string, string, string[]>(AxarDB.Helpers.ScriptUtils.Split));
            engine.SetValue("toDecimal", new Func<string, decimal>(AxarDB.Helpers.ScriptUtils.ToDecimal));
            engine.SetValue("guid", new Func<string>(() => Guid.NewGuid().ToString()));
            engine.SetValue("toJson", new Func<object, string>(o => System.Text.Json.JsonSerializer.Serialize(o, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));
            
            // Join Alias Utility
            engine.SetValue("alias", new Func<object, string, AliasWrapper>((source, name) => new AliasWrapper(source, name)));

            // Deep Copy Utility
            engine.SetValue("deepcopy", new Func<object?, object?>(AxarDB.Helpers.ScriptUtils.DeepCopy));

            // MySQL Functions
            engine.SetValue("mysqlRead", new Func<string, string, object?, object>((conn, query, param) => 
                MySqlRead(conn, query, param, context)));
                
            engine.SetValue("mysqlExec", new Func<int>(() => 0)); // Placeholder override below
            
            engine.SetValue("mysqlExec", new Func<string, string, object?, int>((conn, query, param) => 
                MySqlExec(conn, query, param, context)));

            // PostgreSQL Functions
            engine.SetValue("pgsqlRead", new Func<string, string, object?, object>((conn, query, param) => 
                PgSqlRead(conn, query, param, context)));
                
            engine.SetValue("pgsqlExec", new Func<string, string, object?, int>((conn, query, param) => 
                PgSqlExec(conn, query, param, context)));


            // Webhook Function (POST)
            engine.SetValue("webhook", new Func<string, object, object?, object>((url, data, headers) => 
                PerformHttpRequest("POST", url, data, headers)));

            // HTTP Get Function
            engine.SetValue("httpGet", new Func<string, object?, object>((url, headers) => 
                PerformHttpRequest("GET", url, null, headers)));

            // Date Functions
            engine.SetValue("addMinutes", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddMinutes));
            engine.SetValue("addHours", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddHours));
            engine.SetValue("addDays", new Func<object, double, DateTime>(AxarDB.Helpers.ScriptUtils.AddDays));

            // Support for .toList() on arrays/enumerables
            engine.Execute(@"
                Object.prototype.toList = function() {
                    if (Array.isArray(this)) return this;
                    if (this && typeof this.toArray === 'function') return this.toArray();
                    // If it's a .NET List/Enumerable wrapped
                    return new System.Collections.Generic.List(this);
                };
                
                // Polyfill for Array if needed, but Object.prototype hits all. 
                // Better to be specific to Array or standard iterables if possible to avoid polluting everything.
                
                Array.prototype.toList = function() { return this; };
                
                // Alias contains to includes for String
                if (!String.prototype.contains) {
                    String.prototype.contains = String.prototype.includes;
                }
            ");
        }

        public DatabaseEngine(string? basePath = null)
        {
            _basePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);

            _storage = new AxarDB.Storage.DiskStorage(Path.Combine(_basePath, "Data"));
            
            // Dynamic Memory Limit: 70% of Total Available Memory
            var gcInfo = GC.GetGCMemoryInfo();
            long totalBytes = gcInfo.TotalAvailableMemoryBytes;
            long limit = (long)(totalBytes * 0.7);

            var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
            {
                SizeLimit = limit
            };
            _sharedCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(cacheOptions);

            // Create default system collection
            GetCollection("sysusers");
            // Add default user
            var sysusers = GetCollection("sysusers");
            // Check via storage if empty
            if (!sysusers.FindAll().Any())
            {
                sysusers.Insert(new Dictionary<string, object>
                {
                    { "username", "unlocker" },
                    { "password", "unlocker" }
                });
            }
        }

        public Collection GetCollection(string name)
        {
            return _collections.GetOrAdd(name, n => new Collection(n, _storage, _sharedCache));
        }

        public object? ExecuteScript(string script, Dictionary<string, object>? parameters = null, ScriptContext? context = null)
        {
            var ctx = context ?? ScriptContext.Default; 
            Console.WriteLine($"DEBUG: Executing script (Length: {script.Length})...");
            // ---------------------------------------------------------
            // VAULTS FEATURE INITIALIZATION
            // ---------------------------------------------------------
            GetCollection("sysvaults");

            //FOR AI TESTING PURPOSES
            // Validate and prepare script
            // 1. Vault Replacement ($key -> value from sysvaults)
            // We do this BEFORE parameters to allow vaults to define structure if needed, 
            // though usually independent. 
            // Fetch all vaults to replace placeholders.
            var vaultCol = GetCollection("sysvaults");
            // Optimization: Only fetch if script contains '$'
            if (script.Contains("$"))
            {
                // We stream all vaults. For large number of vaults this might be slow, 
                // but usually vaults are few (config).
                foreach (var vDoc in vaultCol.FindAll())
                {
                    if (vDoc.TryGetValue("key", out var k) && vDoc.TryGetValue("value", out var v))
                    {
                        string keyStr = k.ToString();
                        // Replace $key with serialized value.
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            var valStr = System.Text.Json.JsonSerializer.Serialize(v);
                            script = script.Replace("$" + keyStr, valStr);
                        }
                    }
                }
            }

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // 1. Validation: simple heuristic blacklist
                    if (param.Value is string s && !IsValidInput(s))
                    {
                        throw new InvalidOperationException($"Input parameter '{param.Key}' contains potentially malicious content.");
                    }

                    //FOR AI TESTING PURPOSES
                    // 2. Placeholder Replacement
                    // We look for @Key and replace it with JSON serialized Value
                    // This ensures strings are quoted and special chars escaped.
                    // Example: @name -> "ketty" or "ke\"tty"
                    
                    var serializedValue = System.Text.Json.JsonSerializer.Serialize(param.Value);
                    
                    // Using simple String.Replace for now as per plan
                    script = script.Replace("@" + param.Key, serializedValue);
                }
            }

            // Create a new engine for scope isolation
            var engine = new Engine(options => {
                 options.AllowClr();
            });

            // Expose console.log for CLI scripts
            engine.SetValue("console", new { log = new Action<object>(o => Console.WriteLine(FormatLog(o))) });

            // Expose 'db'
            var dbBridge = new AxarDBBridge(this, engine);
            engine.SetValue("db", dbBridge);

            // Expose 'UnlockDB' constructor: new UnlockDB("name")
            engine.SetValue("AxarDB", new Func<string, CollectionBridge>(name => {
                return new CollectionBridge(GetCollection(name), engine);
            }));

            // Expose 'showCollections'
            engine.SetValue("showCollections", new Func<List<string>>(() => {
                var list = _collections.Keys.ToList();
                // Also scan the Data folder for existing collections that might not be loaded yet
                var dataPath = Path.Combine(_basePath, "Data");
                if (Directory.Exists(dataPath))
                {
                    var dirs = Directory.GetDirectories(dataPath).Select(Path.GetFileName).Where(n => n != null).Cast<string>();
                    foreach (var dir in dirs)
                    {
                        if (!list.Contains(dir)) list.Add(dir);
                    }
                }
                return list.OrderBy(x => x).ToList();
            }));

            engine.SetValue("getIndexes", new Func<string, object>((name) => {
                var col = GetCollection(name);
                return col.Indices.Select(i => new { PropertyName = i.PropertyName, Type = i.Type }).ToList();
            }));

            // --- Utility Functions ---
            RegisterUtils(engine, ctx);
            
            // Execute
            var result = engine.Evaluate(script);
            
            // Convert result back to native object if possible
            return result.ToObject();
        }

        private bool IsValidInput(string input)
        {
            var blackList = new[] 
            { 
                "eval(", "Function(", "setTimeout(", "setInterval(", "<script", "javascript:" 
            };

            foreach (var item in blackList)
            {
                if (input.Contains(item, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        public bool Authenticate(string username, string password)
        {
            Console.WriteLine($"DEBUG: Authenticating user '{username}'...");
            var sysusers = GetCollection("sysusers");
            
            // Try matching plain password first
            var result = sysusers.FindAll(d => 
                d.ContainsKey("username") && d["username"].ToString() == username &&
                d.ContainsKey("password") && d["password"].ToString() == password
            ).Any();

            // If not found, try matching with SHA256 hash (in case the password in DB is hashed)
            if (!result)
            {
                string hashedPassword = AxarDB.Helpers.ScriptUtils.SHA256(password);
                result = sysusers.FindAll(d => 
                    d.ContainsKey("username") && d["username"].ToString() == username &&
                    d.ContainsKey("password") && d["password"].ToString() == hashedPassword
                ).Any();
            }

            Console.WriteLine($"DEBUG: Auth result for '{username}': {result}");
            return result;
        }
    
        public bool AddVault(string key, object value)
        {
            var col = GetCollection("sysvaults");
            // Check if exists
            var existing = col.FindAll().FirstOrDefault(d => d.ContainsKey("key") && d["key"]?.ToString() == key);
            
            if (existing != null)
            {
                // Update
                existing["value"] = value;
                col.Insert(existing); // Persist update
            }
            else
            {
                // Insert new
                col.Insert(new Dictionary<string, object> { 
                    { "key", key }, 
                    { "value", value },
                    { "created", DateTime.UtcNow }
                });
            }
            return true;
        }

        // ---------------------------------------------------------
        // VIEWS FEATURE
        // ---------------------------------------------------------

        private string GetViewsPath() 
        {
            var path = Path.Combine(_basePath, "Views");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string GetLogsPath() 
        {
            var path = Path.Combine(_basePath, "view_logs");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public object? ExecuteView(string viewName, Dictionary<string, object>? parameters, string clientIp, string user)
        {
            var context = new ScriptContext 
            { 
               IpAddress = clientIp, 
               User = user, 
               IsView = true, 
               ViewName = viewName 
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var consoleLogs = new List<string>();
            object? result = null;
            string? error = null;

            try
            {
                var viewPath = Path.Combine(GetViewsPath(), viewName + ".js");
                if (!File.Exists(viewPath)) throw new FileNotFoundException($"View '{viewName}' not found.");

                var script = File.ReadAllText(viewPath);
                
                //FOR AI TESTING PURPOSES
                // Access Control Check (for HTTP calls mostly, but good to enforce)
                // If called internally via db.view, we assume privileged.
                // If called via HTTP, Program.cs handles Auth. 
                // But we need to know if it's public/private for Program.cs logic.
                // We'll expose a method GetViewAccess(viewName) for that.

                // Execute script using standard method but with Console injection
                
                // We reuse ExecuteScript logic but need to inject 'console'
                // Refactoring ExecuteScript to take an action for engine config would be cleaner, 
                // but let's duplicate/inline slightly for minimal disruption or use a protected override.
                // Actually, let's just instantiate engine here or modify ExecuteScript to support Action<Engine>.
                
                // We'll implement a custom execution here to support console capture + logging specific to views.
                
                // 1. Prepare Script (Vaults + Params) - Reuse logic?
                // We can't easily reuse ExecuteScript private logic without refactor. 
                // Let's refactor `PrepareScript` out of `ExecuteScript`.
                
                // For now, I will inline the prep logic to ensure it works correctly for Views specific requirements.
                
                // 1. Vaults
                if (script.Contains("$"))
                {
                    var vaultCol = GetCollection("sysvaults");
                    foreach (var vDoc in vaultCol.FindAll())
                    {
                        if (vDoc.TryGetValue("key", out var k) && vDoc.TryGetValue("value", out var v))
                        {
                             if (k != null) script = script.Replace("$" + k.ToString(), System.Text.Json.JsonSerializer.Serialize(v));
                        }
                    }
                }

                // 2. Params
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        if (param.Value is string s && !IsValidInput(s)) throw new InvalidOperationException($"Malicious input in '{param.Key}'");
                        script = script.Replace("@" + param.Key, System.Text.Json.JsonSerializer.Serialize(param.Value));
                    }
                }

                var engine = new Engine(options => options.AllowClr());
                
                // Bridges
                var dbBridge = new AxarDBBridge(this, engine);
                engine.SetValue("db", dbBridge);
                engine.SetValue("AxarDB", new Func<string, CollectionBridge>(name => new CollectionBridge(GetCollection(name), engine)));
                engine.SetValue("showCollections", new Func<List<string>>(() => _collections.Keys.ToList())); // Simplified for view
                RegisterUtils(engine, context);
                // Ideally ExecuteScript should be refactored. 
                // Let's just add the Console capture here.
                
                engine.SetValue("console", new { log = new Action<object>(o => consoleLogs.Add(FormatLog(o))) });

                // Execute
                var evalResult = engine.Evaluate(script);
                result = evalResult.ToObject();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                throw;
            }
            finally
            {
                sw.Stop();
                // Logging
                var logEntry = new 
                {
                    clientIp,
                    user,
                    timestamp = DateTime.UtcNow,
                    durationMs = sw.ElapsedMilliseconds,
                    error,
                    console = consoleLogs,
                    result = error != null ? null : result // Don't log full result if massive? Maybe option.
                };
                
                var logJson = System.Text.Json.JsonSerializer.Serialize(logEntry, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(GetLogsPath(), $"{viewName}_{DateTime.UtcNow.Ticks}.json"), logJson);
            }
            
            return result;
        }

        public string GetViewAccess(string viewName)
        {
            var viewPath = Path.Combine(GetViewsPath(), viewName + ".js");
            if (!File.Exists(viewPath)) return "private"; 
            
            // Read first few lines
            foreach(var line in File.ReadLines(viewPath).Take(5))
            {
                if (line.Contains("@access public")) return "public";
            }
            return "private";
        }

        public void SaveView(string name, string content)
        {
            File.WriteAllText(Path.Combine(GetViewsPath(), name + ".js"), content);
        }

        public void DeleteView(string name)
        {
            var path = Path.Combine(GetViewsPath(), name + ".js");
            if (File.Exists(path)) File.Delete(path);
        }


        public List<string> ListViews()
        {
            var path = GetViewsPath();
            return Directory.GetFiles(path, "*.js").Select(Path.GetFileNameWithoutExtension).Cast<string>().ToList();
        }

        public string? GetViewContent(string name)
        {
            var path = Path.Combine(GetViewsPath(), name + ".js");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // ---------------------------------------------------------
        // TRIGGERS FEATURE
        // ---------------------------------------------------------

        private FileSystemWatcher? _triggerWatcher;

        private string GetTriggersPath() 
        {
            var path = Path.Combine(_basePath, "Triggers");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string GetTriggerLogsPath() 
        {
            var path = Path.Combine(_basePath, "trigger_logs");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public void InitializeTriggers()
        {
            if (_triggerWatcher != null) return;

            
            var dataPath = Path.Combine(_basePath, "Data");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

            _triggerWatcher = new FileSystemWatcher(dataPath);
            _triggerWatcher.IncludeSubdirectories = true;
            _triggerWatcher.Filter = "*.json"; // Only listen to data changes
            // Watch for changes, creation, deletion
            _triggerWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;

            _triggerWatcher.Changed += (s, e) => HandleFileEvent(e.FullPath, "changed");
            _triggerWatcher.Created += (s, e) => HandleFileEvent(e.FullPath, "created");
            _triggerWatcher.Deleted += (s, e) => HandleFileEvent(e.FullPath, "deleted");
            
            _triggerWatcher.EnableRaisingEvents = true;
        }

        private void HandleFileEvent(string fullPath, string evtType)
        {
            Task.Run(() => 
            {
                try 
                {
                    // 1. Identify Collection and Document
                    // Path: Data/CollectionName/doc.json OR Data/CollectionName/_id_wrapper if shards (but current impl is simple)
                    // Current DiskStorage: Data/CollectionName/{guid}.json
                    
                    var relative = Path.GetRelativePath(Path.Combine(_basePath, "Data"), fullPath);
                    var parts = relative.Split(Path.DirectorySeparatorChar);
                    
                    if (parts.Length < 2) return; // Not inside a collection
                    
                    var collectionName = parts[0];
                    var docIdRaw = Path.GetFileNameWithoutExtension(parts[1]);

                    // Notify Collection for Cache Invalidation
                    try
                    {
                        var collection = GetCollection(collectionName);
                        collection.OnExternalChange(docIdRaw, evtType);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying collection change: {ex.Message}");
                    }
                    
                    // 2. Find matching triggers
                    var triggers = Directory.GetFiles(GetTriggersPath(), "*.js");
                    
                    foreach(var triggerPath in triggers)
                    {
                        var content = File.ReadAllText(triggerPath);
                        // Parse header: // @target collectionName
                        if (content.Contains($"@target {collectionName}") || content.Contains("@target *"))
                        {
                            ExecuteTrigger(Path.GetFileNameWithoutExtension(triggerPath), content, evtType, collectionName, docIdRaw);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTriggerError("System", ex.Message);
                }
            });
        }

        private void ExecuteTrigger(string triggerName, string script, string type, string col, string docId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var consoleLogs = new List<string>();
            string? error = null;

            try
            {
                var engine = new Engine(options => options.AllowClr());
                
                // Minimal Bridge for Triggers - full db access? Yes.
                var dbBridge = new AxarDBBridge(this, engine);
                engine.SetValue("db", dbBridge);
                RegisterUtils(engine, ScriptContext.Default); // Triggers run as system currently
                // Re-add console
                engine.SetValue("console", new { log = new Action<object>(o => consoleLogs.Add(FormatLog(o))) });

                // Event Object
                engine.SetValue("event", new 
                {
                    type,
                    collection = col,
                    documentId = docId,
                    timestamp = DateTime.UtcNow
                });

                engine.Evaluate(script);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                consoleLogs.Add("Error: " + ex.Message);
            }
            finally
            {
                sw.Stop();
                LogTriggerExecution(triggerName, sw.ElapsedMilliseconds, consoleLogs, error);
            }
        }

        private void LogTriggerExecution(string triggerName, long duration, List<string> logs, string? error)
        {
            try 
            {
                var logEntry = new 
                {
                    trigger = triggerName,
                    timestamp = DateTime.UtcNow,
                    durationMs = duration,
                    error,
                    console = logs
                };
                
                var line = System.Text.Json.JsonSerializer.Serialize(logEntry);
                var filename = $"{DateTime.UtcNow:yyyy-MM-dd}.log";
                var path = Path.Combine(GetTriggerLogsPath(), filename);
                
                // Simple file append with lock to ensure thread safety
                lock(_logLock) 
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            } 
            catch {}
        }

        private void LogTriggerError(string context, string msg)
        {
            LogTriggerExecution(context, 0, new List<string> { msg }, "System Error");
        }

        private static readonly object _logLock = new object();


    
        public string? GetTriggerContent(string name)
        {
            var path = Path.Combine(GetTriggersPath(), name + ".js");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void SaveTrigger(string name, string targetCollection, string content)
        {
            if (!content.Contains("@target"))
            {
                content = $"// @target {targetCollection}\n" + content;
            }
            File.WriteAllText(Path.Combine(GetTriggersPath(), name + ".js"), content);
        }
        
        public void SaveTrigger(string name, string content) => SaveTrigger(name, "*", content);

        public void DeleteTrigger(string name)
        {
            var path = Path.Combine(GetTriggersPath(), name + ".js");
            if (File.Exists(path)) File.Delete(path);
        }

        public List<string> ListTriggers()
        {
            return Directory.GetFiles(GetTriggersPath(), "*.js").Select(Path.GetFileNameWithoutExtension).Cast<string>().ToList();
        }

        public object PgSqlRead(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? error = null;
            bool success = false;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(query, connection);
                
                if (parameters != null)
                {
                    var paramJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(paramJson);
                    
                    if (paramDict != null)
                    {
                        foreach (var kvp in paramDict)
                        {
                            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                        }
                    }
                }
                
                using var reader = command.ExecuteReader();
                var results = new List<Dictionary<string, object>>();
                
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i);
                        if (val == DBNull.Value) val = null;
                        row[reader.GetName(i)] = val;
                    }
                    results.Add(row);
                }
                
                sw.Stop();
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                error = ex.Message;
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, error);
                throw new Exception($"PostgreSQL Read Failed: {ex.Message}");
            }
        }

        public int PgSqlExec(string connectionString, string query, object? parameters, ScriptContext context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? error = null;
            bool success = false;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(query, connection);
                
                if (parameters != null)
                {
                    var paramJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                    var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(paramJson);
                    
                    if (paramDict != null)
                    {
                        foreach (var kvp in paramDict)
                        {
                            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                        }
                    }
                }
                
                int affected = command.ExecuteNonQuery();
                
                sw.Stop();
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, true);
                
                return affected;
            }
            catch (Exception ex)
            {
                sw.Stop();
                error = ex.Message;
                LogPgSqlRequest(context, connectionString, query, parameters, sw.ElapsedMilliseconds, false, error);
                throw new Exception($"PostgreSQL Exec Failed: {ex.Message}");
            }
        }

        private void LogPgSqlRequest(ScriptContext context, string connectionString, string query, object? parameters, long durationMs, bool success, string? error = null)
        {
             try
             {
                 // Use AxarDB.Logging.Logger to avoid ambiguity
                 AxarDB.Logging.Logger.LogRequest(context.IpAddress, context.User, $"[PostgreSQL] {query}", durationMs, success, error ?? "");
                 
                 if (!success && !string.IsNullOrEmpty(error))
                 {
                     AxarDB.Logging.Logger.LogError($"[PostgreSQL Error] {error} | Query: {query}");
                 }
             }
             catch {}
        }
    }
}
