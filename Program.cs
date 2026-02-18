using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Diagnostics;
using AxarDB;
using AxarDB.Logging;

// Ensure Console Output is UTF8
Console.OutputEncoding = Encoding.UTF8;



if (args.Length > 0 && args[0] == "script")
{
    var scriptPath = args.Length > 1 ? args[1] : null;
    if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
    {
        // Parse target path from args if present (e.g. --targetpath)
        // Simple manual parse since reusing the loop below is for builder
        string? tPath = null;
        for (int i = 0; i < args.Length; i++) {
             if (args[i] == "--targetpath" && i + 1 < args.Length) tPath = args[i+1];
        }

        var db = new DatabaseEngine(tPath);
        try 
        {
            var content = File.ReadAllText(scriptPath);
            var result = db.ExecuteScript(content);
            Console.WriteLine("Result: " + (result?.ToString() ?? "null"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
    else
    {
        Console.WriteLine($"File not found or missing argument: {scriptPath}");
    }
    return;
}

// Configure Kestrel to listen on specified port or default 5000
// Parse Arguments
int port = 5000;
string? targetPath = null;

for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "-p" || args[i] == "--port") && i + 1 < args.Length && int.TryParse(args[i + 1], out int p))
    {
        port = p;
    }
    if (args[i] == "--targetpath" && i + 1 < args.Length)
    {
        targetPath = args[i + 1];
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(port);
});

var dbEngine = new DatabaseEngine(targetPath);
builder.Services.AddSingleton(dbEngine);
builder.Services.AddHostedService<AxarDB.BackgroundServices.QueueProcessor>();
dbEngine.InitializeTriggers();

var app = builder.Build();

if (args.Contains("--benchmark"))
{
    Benchmark.Run();
    return;
}


// Global Exception Handler Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Logger.LogError($"Unhandled Exception: {ex.Message} | StackTrace: {ex.StackTrace}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "An internal server error occurred." });
    }
});

// Request Logging Middleware
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var user = "anonymous";
    
    // Extract user from Basic Auth if present
    string authHeader = context.Request.Headers["Authorization"];
    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic "))
    {
        try
        {
            var encoding = Encoding.GetEncoding("iso-8859-1");
            string credentials = encoding.GetString(Convert.FromBase64String(authHeader.Substring(6)));
            string[] parts = credentials.Split(':');
            if (parts.Length > 0) user = parts[0];
        }
        catch { /* Ignore auth parsing errors here, middleware will handle it */ }
    }

    // Capture Request Body for logging
    string requestBody = "";
    if (context.Request.Path.StartsWithSegments("/query") && context.Request.Method == "POST")
    {
        context.Request.EnableBuffering();
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }
    }

    try
    {
        await next();
        sw.Stop();
        
        // Only log /query requests to avoid cluttering with health checks or root hits if desired, 
        // but the prompt says "herhangi bir istek", so I log everything.
        Logger.LogRequest(ip, user, requestBody, sw.ElapsedMilliseconds, context.Response.StatusCode < 400);
    }
    catch (Exception ex)
    {
        sw.Stop();
        Logger.LogRequest(ip, user, requestBody, sw.ElapsedMilliseconds, false, ex.Message);
        throw; // Re-throw to be caught by global exception handler
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

// Basic Authentication Middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/query") || context.Request.Path.StartsWithSegments("/collections"))
    {
        string authHeader = context.Request.Headers["Authorization"];
        if (authHeader != null && authHeader.StartsWith("Basic "))
        {
            var encoding = Encoding.GetEncoding("iso-8859-1");
            string credentials = encoding.GetString(Convert.FromBase64String(authHeader.Substring(6)));
            string[] parts = credentials.Split(':');
            if (parts.Length == 2)
            {
                string username = parts[0];
                string password = parts[1];

                if (dbEngine.Authenticate(username, password))
                {
                    await next();
                    return;
                }
            }
        }
        
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"AxarDB\"";
        context.Response.StatusCode = 401;
        return;
    }
    await next();
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/collections", () => 
{
    // Admin context for showing collections? Or just default.
    return Results.Json(dbEngine.ExecuteScript("showCollections()"));
});

app.MapGet("/docs", () => Results.Redirect("/docs.html"));

app.MapPost("/query", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var script = await reader.ReadToEndAsync();
    
    // Extract query parameters for injection safety
    var queryParams = context.Request.Query.ToDictionary(
        k => k.Key, 
        v => (object)v.Value.ToString()
    );
    
    try 
    {
        // Extract Auth User if present (already parsed in Middleware but not easily accessible here without HttpContext.Items or re-parsing)
        // We can re-parse or use Items if middleware set it. 
        // Middleware didn't set Items. Let's re-parse quickly or just trust "anonymous" if not found? 
        // Middleware logic was:
        /*
        string authHeader = context.Request.Headers["Authorization"];
        ...
        */
        string user = "anonymous";
        string authHeader = context.Request.Headers["Authorization"];
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic "))
        {
             try {
                var encoding = Encoding.GetEncoding("iso-8859-1");
                string credentials = encoding.GetString(Convert.FromBase64String(authHeader.Substring(6)));
                var parts = credentials.Split(':');
                if (parts.Length > 0) user = parts[0];
             } catch {}
        }
    
        var scriptContext = new AxarDB.ScriptContext 
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
        // Results.Problem will set status code > 400 which our middleware captures
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/views/{viewName}", async (string viewName, HttpContext context) => 
{
    // 1. Check Access
    string access = dbEngine.GetViewAccess(viewName);
    string user = "anonymous";

    if (access == "private")
    {
        // Require Auth
        string authHeader = context.Request.Headers["Authorization"];
        bool authenticated = false;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic "))
        {
            try
            {
                var encoding = Encoding.GetEncoding("iso-8859-1");
                string credentials = encoding.GetString(Convert.FromBase64String(authHeader.Substring(6)));
                string[] parts = credentials.Split(':');
                if (parts.Length == 2 && dbEngine.Authenticate(parts[0], parts[1]))
                {
                    user = parts[0];
                    authenticated = true;
                }
            }
            catch {}
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

    // 2. Prepare Params
    var queryParams = context.Request.Query.ToDictionary(k => k.Key, v => (object)v.Value.ToString());

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

app.Run();


