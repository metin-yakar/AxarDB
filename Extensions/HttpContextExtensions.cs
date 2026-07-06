using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace AxarDB.Extensions
{
    public static class HttpContextExtensions
    {
        public static bool TryGetBasicCredentials(
            this HttpContext context, 
            [NotNullWhen(true)] out string? username, 
            [NotNullWhen(true)] out string? password)
        {
            username = null;
            password = null;

            string? authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var encodedCredentials = authHeader.Substring(6).Trim();
                var credentialsBytes = Convert.FromBase64String(encodedCredentials);
                var encoding = Encoding.GetEncoding("iso-8859-1");
                var credentials = encoding.GetString(credentialsBytes);
                
                var separatorIndex = credentials.IndexOf(':');
                if (separatorIndex >= 0)
                {
                    username = credentials.Substring(0, separatorIndex);
                    password = credentials.Substring(separatorIndex + 1);
                    return true;
                }
            }
            catch
            {
                // Ignore base64 decoding or split exceptions
            }

            return false;
        }
    }
}
