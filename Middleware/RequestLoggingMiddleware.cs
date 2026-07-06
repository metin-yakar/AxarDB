using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AxarDB.Logging;
using AxarDB.Extensions;

namespace AxarDB.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var user = "anonymous";

            // Extract user from Basic Auth if present using HttpContextExtensions
            if (context.TryGetBasicCredentials(out string? username, out _))
            {
                user = username;
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
                await _next(context);
                sw.Stop();

                Logger.LogRequest(ip, user, requestBody, sw.ElapsedMilliseconds, context.Response.StatusCode < 400);
                AxarDB.Metrics.MetricsCollector.Instance.RecordRequest(
                    ip,
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    context.Request.ContentLength ?? 0,
                    context.Response.ContentLength ?? 0
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.LogRequest(ip, user, requestBody, sw.ElapsedMilliseconds, false, ex.Message);
                AxarDB.Metrics.MetricsCollector.Instance.RecordRequest(
                    ip,
                    context.Request.Method,
                    context.Request.Path,
                    500,
                    sw.ElapsedMilliseconds,
                    context.Request.ContentLength ?? 0,
                    0
                );
                throw;
            }
        }
    }
}
