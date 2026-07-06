using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using AxarDB.Core;
using AxarDB.Definitions;
using AxarDB.Extensions;

namespace AxarDB.Extensions
{
    public static class EndpointExtensions
    {
        public static void MapDatabaseEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/collections", (DatabaseEngine dbEngine) => 
            {
                return Results.Json(dbEngine.ExecuteScript("showCollections()"));
            });

            app.MapDelete("/collections/{name}", (string name, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteCollection(name);
                return Results.Ok(new { success = true });
            });

            app.MapPost("/query", async (HttpContext context, DatabaseEngine dbEngine) =>
            {
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                var script = await reader.ReadToEndAsync();
                
                var queryParams = context.Request.Query.ToDictionary(
                    k => k.Key, 
                    v => (object)(v.Value.FirstOrDefault() ?? string.Empty)
                );
                
                try 
                {
                    string user = "anonymous";
                    if (context.TryGetBasicCredentials(out string? username, out _))
                    {
                        user = username;
                    }
                
                    var scriptContext = new ScriptContext 
                    { 
                        IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        User = user,
                        IsView = false
                    };

                    var result = dbEngine.ExecuteScript(script, queryParams, scriptContext, context.RequestAborted);
                    return Results.Json(result);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapGet("/views/{viewName}", async (string viewName, HttpContext context, DatabaseEngine dbEngine) => 
            {
                string access = dbEngine.GetViewAccess(viewName);
                string user = "anonymous";

                if (access == "private")
                {
                    bool authenticated = false;
                    if (context.TryGetBasicCredentials(out string? username, out string? password))
                    {
                        if (dbEngine.Authenticate(username, password))
                        {
                            user = username;
                            authenticated = true;
                        }
                    }

                    if (!authenticated)
                    {
                        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"AxarDB Views\"";
                        return Results.Unauthorized();
                    }
                }
                else
                {
                    user = "public_user";
                }

                var queryParams = context.Request.Query.ToDictionary(
                    k => k.Key, 
                    v => (object)(v.Value.FirstOrDefault() ?? string.Empty)
                );

                try
                {
                    var result = dbEngine.ExecuteView(viewName, queryParams, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", user, context.RequestAborted);
                    return Results.Json(result);
                }
                catch (FileNotFoundException)
                {
                    return Results.NotFound(new { error = $"View '{viewName}' not found" });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapDelete("/views/{viewName}", (string viewName, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteView(viewName);
                return Results.Ok(new { success = true });
            });

            app.MapDelete("/triggers/{triggerName}", (string triggerName, DatabaseEngine dbEngine) => 
            {
                dbEngine.DeleteTrigger(triggerName);
                return Results.Ok(new { success = true });
            });

            app.MapGet("/memory/list", (DatabaseEngine dbEngine) =>
            {
                var list = dbEngine.MemoryStore.GetCollectionNames()
                    .Select(name => new
                    {
                        name = name,
                        count = dbEngine.MemoryStore.GetRecordCount(name)
                    })
                    .ToList();
                return Results.Json(list);
            });

            app.MapGet("/bulk/list", (DatabaseEngine dbEngine) =>
            {
                var list = dbEngine.BulkStore.ListCollections()
                    .Select(name => {
                        var path = Path.Combine(dbEngine.BasePath, "Bulk", $"{name}.jsonl");
                        var info = new FileInfo(path);
                        return new
                        {
                            name = name,
                            file = $"{name}.jsonl",
                            recordCount = dbEngine.BulkStore.GetDocuments(name).Count(),
                            sizeKB = info.Exists ? info.Length / 1024.0 : 0
                        };
                    })
                    .ToList();
                return Results.Json(list);
            });

            app.MapGet("/metrics", (DatabaseEngine dbEngine) =>
            {
                var snapshot = AxarDB.Metrics.MetricsCollector.Instance.GetSnapshot(Path.Combine(dbEngine.BasePath, "Data"));
                return Results.Json(snapshot);
            });
        }
    }
}
