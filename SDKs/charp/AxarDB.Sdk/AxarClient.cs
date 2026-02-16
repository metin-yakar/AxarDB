using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Dynamic;

using System.Collections.Generic;
using System.Collections;

namespace AxarDB.Sdk
{
    public class AxarClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private readonly ILogger<AxarClient> _logger;
        private readonly AxarRateLimiter _rateLimiter;
        private readonly IMemoryCache _cache; // Kept for disposal if we own it
        private readonly JsonSerializerSettings _jsonSettings;

        public AxarClient(string baseUrl, string username, string password, ILogger<AxarClient> logger = null, IMemoryCache cache = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            // Increase buffer size for large responses if needed, though GetAsync defaults are usually fine.
            // _httpClient.MaxResponseContentBufferSize = 2147483647; // 2GB
            
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            
            _logger = logger;
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
            _rateLimiter = new AxarRateLimiter(_cache, _logger);

            _jsonSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                MaxDepth = null, // Allow deep structures
                Converters = new List<JsonConverter> { new ExpandoObjectConverter(), new StringEnumConverter() }
            };
        }

        public void ConfigureRateLimit(string type, int maxRequests)
        {
            _rateLimiter.SetLimit(type, maxRequests);
        }

        public AxarQueryBuilder<T> Collection<T>(string collectionName)
        {
            return new AxarQueryBuilder<T>(this, collectionName);
        }

        /// <summary>
        /// Checks rate limit before executing query.
        /// </summary>
        /// <param name="limitKey">Unique key (e.g. IP, SessionID)</param>
        /// <param name="limitDuration">Duration string (e.g. "1h", "10m")</param>
        /// <param name="limitType">Type of limit (e.g. "ip", "session")</param>
        /// <param name="limitCondition">Optional condition string</param>
        public async Task<T> QueryWithRateLimitAsync<T>(string script, object parameters, string limitKey, string limitDuration, string limitType, string limitCondition = null)
        {
            if (_rateLimiter.CheckLimit(limitKey, limitDuration, limitType, limitCondition))
            {
                _rateLimiter.LogRestriction(limitKey, limitDuration, limitType, limitCondition);
                // Return default or throw? Throwing is safer to stop execution.
                throw new Exception($"Rate limit exceeded for {limitType} on {limitKey}.");
            }
            
            return await QueryAsync<T>(script, parameters);
        }

        public async Task<T> QueryAsync<T>(string script, object parameters = null)
        {
            var url = $"{_baseUrl}/query";
            if (parameters != null)
            {
                var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
                
                if (parameters is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        queryString[entry.Key.ToString()] = entry.Value?.ToString();
                    }
                }
                else
                {
                    var props = parameters.GetType().GetProperties();
                    foreach (var prop in props)
                    {
                        queryString[prop.Name] = prop.GetValue(parameters)?.ToString();
                    }
                }
                
                url += "?" + queryString.ToString();
            }

            var content = new StringContent(script, Encoding.UTF8, "text/plain");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"AxarDB Error ({response.StatusCode}): {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return default;
            
            try 
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
            catch (JsonException)
            {
                // If the result is not JSON (e.g. primitive type or just string), try to convert it
                try 
                {
                     return (T)Convert.ChangeType(json, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
        }

        public async Task ExecuteAsync(string script, object parameters = null)
        {
            await QueryAsync<object>(script, parameters);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        // Helper methods for common operations

        public async Task<T> InsertAsync<T>(string collection, T document) where T : AxarBaseModel
        {
             if (string.IsNullOrEmpty(document.Id) || document.Id == Guid.Empty.ToString())
             {
                 document.Id = Guid.NewGuid().ToString();
             }
             var json = JsonConvert.SerializeObject(document, _jsonSettings);
             var script = $"db.{collection}.insert({json})";
             return await QueryAsync<T>(script);
        }

        public async Task<object> InsertAsync(string collection, object document)
        {
             var json = JsonConvert.SerializeObject(document, _jsonSettings);
             var script = $"db.{collection}.insert({json})";
             return await QueryAsync<object>(script);
        }

        public async Task<T[]> FindAllAsync<T>(string collection, string predicate = null) where T : AxarBaseModel
        {
            var script = $"db.{collection}.findall({(predicate != null ? predicate : "")}).toList()";
            return await QueryAsync<T[]>(script);
        }
        
        public async Task<T> FindAsync<T>(string collection, string predicate) where T : AxarBaseModel
        {
            var script = $"db.{collection}.find({predicate})";
            return await QueryAsync<T>(script);
        }

        public async Task UpdateAsync(string collection, string predicate, object updateData)
        {
            var json = JsonConvert.SerializeObject(updateData, _jsonSettings);
            var script = $"db.{collection}.update({predicate}, {json})";
            await ExecuteAsync(script);
        }

        public async Task DeleteAsync(string collection, string predicate)
        {
            var script = $"db.{collection}.findall({predicate}).delete()";
            await ExecuteAsync(script);
        }
        public async Task CreateViewAsync(string name, string script)
        {
            // Escape the script to be safe inside a string literal if needed, but here we pass parameters securely
            // db.saveView("name", `script`)
            // Since we use parameterized queries, we can pass script as a parameter?
            // "db.saveView(@name, @script)" might work if server supports it.
            // Let's assume standard parameterization works for saveView arguments.
            await ExecuteAsync("db.saveView(@name, @script)", new { name, script });
        }

        public async Task<T> CallViewAsync<T>(string name, object parameters = null)
        {
            var url = $"{_baseUrl}/views/{name}";
            
            if (parameters != null)
            {
                var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
                
                if (parameters is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        queryString[entry.Key.ToString()] = entry.Value?.ToString();
                    }
                }
                else
                {
                    var props = parameters.GetType().GetProperties();
                    foreach (var prop in props)
                    {
                        queryString[prop.Name] = prop.GetValue(parameters)?.ToString();
                    }
                }
                
                url += "?" + queryString.ToString();
            }

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"Failed to call view '{name}'. Status: {response.StatusCode}.\n" +
                               $"Expected Usage: GET /views/{{name}}?param1=value1\n" +
                               $"Actual URL: {url}\n" +
                               $"Server Response: {content}";
                
                _logger?.LogError(errorMsg);
                throw new Exception($"AxarDB View Error: {errorMsg}");
            }

            if (string.IsNullOrWhiteSpace(content))
                return default;
            
            try 
            {
                // Configured _jsonSettings includes ExpandoObjectConverter and MaxDepth = null
                return JsonConvert.DeserializeObject<T>(content, _jsonSettings);
            }
            catch (JsonException)
            {
                try 
                {
                     return (T)Convert.ChangeType(content, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
        }

        public async Task CreateTriggerAsync(string name, string collection, string script)
        {
            await ExecuteAsync("db.saveTrigger(@name, @collection, @script)", new { name, collection, script });
        }

        public async Task AddVaultAsync(string key, string value)
        {
            await ExecuteAsync("addVault(@key, @value)", new { key, value });
        }

        public async Task CreateIndexAsync(string collection, string selector, bool descending = false)
        {
            // Selector is a function string like "x => x.email"
            // db.users.index(x => x.email)
            // We cannot easily parameterize the *function* itself in the current protocol if it expects a raw function.
            // But we can construct the string safely if selector is just the function body.
            // If selector is "x => x.email", we can interpolate.
            // "db.collection.index(selector)"
            // CAUTION: Selector is code.
            // If we assume selector is provided by developer in code, it's trusted?
            // SDK user provides it string literal.
            // "db.users.index(x => x.email)"
            // Let's parameterize the collection name if possible? No, collection is usually part of syntax `db.collection`.
            // So we build: $"db.{collection}.index({selector})"
            // If descending: $"db.{collection}.index({selector}, 'DESC')"
            
            var script = $"db.{collection}.index({selector})";
            if (descending)
            {
                script = $"db.{collection}.index({selector}, \"DESC\")";
            }
            await ExecuteAsync(script);
        }

        public async Task CreateUserAsync(string username, string password)
        {
            // db.sysusers.insert({ username: "...", password: sha256("...") })
            // We can do this via parameters
            await ExecuteAsync("db.sysusers.insert({ username: @username, password: sha256(@password) })", new { username, password });
        }

        public async Task<T> JoinAsync<T>(string collection1, string collection2, string whereCondition) where T : AxarBaseModel
        {
            // db.join(db.users, db.orders).where(x => x.userId == x.customerId).toList()
            // We construct the script.
            // condition is "x => x.userId == x.customerId"
            var script = $"db.join(db.{collection1}, db.{collection2}).where({whereCondition}).toList()";
            return await QueryAsync<T>(script);
        }

        public async Task<string[]> ShowCollectionsAsync()
        {
            // db.getCollections()
            return await QueryAsync<string[]>("db.getCollections()");
        }

        public async Task<string> RandomStringAsync(int length)
        {
            return await QueryAsync<string>("random(@length)", new { length });
        }
    }
}
