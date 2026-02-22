using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AxarDB.Helpers
{
    public class LlmClient
    {
        private readonly string _url;
        private readonly string _token;
        private readonly List<object> _messages;
        private static readonly HttpClient _httpClient = new HttpClient();

        public LlmClient(string url, string token)
        {
            _url = url;
            _token = token;
            _messages = new List<object>();
        }

        public void addSysMsg(string msg)
        {
            _messages.Add(new { role = "system", content = msg });
        }

        public object? msg(string userMsg, object? requestData = null, object? expectedResponseModel = null)
        {
            // 1. Add User Message
            _messages.Add(new { role = "user", content = userMsg });

            // 2. Prepare Request Body
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            
            // Deserialize requestData to a Dictionary to merge fields
            var requestBody = new Dictionary<string, object>();
            
            if (requestData != null)
            {
                string jsonPart = JsonSerializer.Serialize(requestData);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPart);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        requestBody[kvp.Key] = kvp.Value;
                    }
                }
            }

            // user passes params like "model", "temperature" in requestData
            // We append "messages"
            requestBody["messages"] = _messages;

            // 3. Execute Request
            var requestJson = JsonSerializer.Serialize(requestBody, options);
            var request = new HttpRequestMessage(HttpMethod.Post, _url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                // Synchronous execution as Jint is synchronous
                var response = _httpClient.Send(request);
                var responseString = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[LlmClient] Error: {response.StatusCode} - {responseString}");
                    return new { status = false, message = $"HTTP {(int)response.StatusCode}: {responseString}" };
                }

                // 4. Handle Response
                
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                
                string? content = null;

                // Attempt to extract content from OpenAI format
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElem))
                    {
                        content = contentElem.GetString();
                    }
                }

                // Fallback if not OpenAI format or content not found
                if (content == null) 
                {
                    content = responseString; 
                }

                // If expectedResponseModel is string (JSON schema/example), try to parse content as JSON
                if (expectedResponseModel != null)
                {
                    try 
                    {
                        // Clean markdown code blocks if present (common issue with LLMs)
                        var cleanContent = content.Trim();
                        if (cleanContent.StartsWith("```json")) 
                        {
                            cleanContent = cleanContent.Substring(7);
                            if (cleanContent.EndsWith("```")) cleanContent = cleanContent.Substring(0, cleanContent.Length - 3);
                        }
                        else if (cleanContent.StartsWith("```"))
                        {
                            cleanContent = cleanContent.Substring(3);
                            if (cleanContent.EndsWith("```")) cleanContent = cleanContent.Substring(0, cleanContent.Length - 3);
                        }

                        // Parse to generic object/dictionary
                        var resultObj = JsonSerializer.Deserialize<object>(cleanContent.Trim());
                        return resultObj;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LlmClient] Failed to parse expected model: {ex.Message}. Returning raw content.");
                        return content; 
                    }
                }

                return content;
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[LlmClient] Exception: {ex.Message}");
                 return new { status = false, message = ex.Message };
            }
        }
    }
}
