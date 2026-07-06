using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using AxarDB.Logging;

namespace AxarDB.Middleware
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled Exception: {ex.Message} | StackTrace: {ex.StackTrace}");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "An internal server error occurred." });
            }
        }
    }
}
