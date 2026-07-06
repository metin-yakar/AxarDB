using Microsoft.AspNetCore.Builder;

namespace AxarDB.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        }

        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestLoggingMiddleware>();
        }

        public static IApplicationBuilder UseBasicAuthentication(this IApplicationBuilder app)
        {
            return app.UseMiddleware<BasicAuthenticationMiddleware>();
        }
    }
}
