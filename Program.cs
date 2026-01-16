using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Diagnostics;
using UnlockDB;
using UnlockDB.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000);
});

var app = builder.Build();

if (args.Contains("--benchmark"))
{
    Benchmark.Run();
    return;
}

var dbEngine = new DatabaseEngine();

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
        
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"UnlockDB\"";
        context.Response.StatusCode = 401;
        return;
    }
    await next();
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/collections", () => 
{
    return Results.Json(dbEngine.ExecuteScript("showCollections()", null));
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
        var result = dbEngine.ExecuteScript(script, queryParams);
        return Results.Json(result);
    }
    catch (Exception ex)
    {
        // Results.Problem will set status code > 400 which our middleware captures
        return Results.Problem(ex.Message);
    }
});

app.Run();


