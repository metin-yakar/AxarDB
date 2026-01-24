using System.Text.Json;

namespace AxarDB.Logging
{
    public static class Logger
    {
        private static readonly string RequestLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "request_logs");
        private static readonly string ErrorLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_logs");
        private static readonly object _lock = new object();

        static Logger()
        {
            if (!Directory.Exists(RequestLogsPath)) Directory.CreateDirectory(RequestLogsPath);
            if (!Directory.Exists(ErrorLogsPath)) Directory.CreateDirectory(ErrorLogsPath);
        }

        public static void LogRequest(string ip, string user, string requestJson, long durationMs, bool success, string errorMessage = "")
        {
            try
            {
                var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                var filePath = Path.Combine(RequestLogsPath, fileName);
                
                // [zaman] - [istemci ip adresi] - [db kullanıcısı] - [request json tek satırda trimlenmiş] - [istek ile cevap arasında geçen süre milisaniye (ms) olarak] - [sonuç başarılı veya başarısız ise kısa bir hata sebebi açıklaması]
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var status = success ? "Success" : $"Failed: {errorMessage}";
                var cleanedJson = requestJson?.Replace("\r", "").Replace("\n", "").Trim() ?? "";
                
                var logLine = $"[{timestamp}] - [{ip}] - [{user}] - [{cleanedJson}] - [{durationMs}ms] - [{status}]";
                
                lock (_lock)
                {
                    File.AppendAllLines(filePath, new[] { logLine });
                }
            }
            catch
            {
                // Fallback to console if file logging fails to avoid crashing
                Console.WriteLine("CRITICAL: Failed to write request log.");
            }
        }

        public static void LogError(string message)
        {
            try
            {
                var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                var filePath = Path.Combine(ErrorLogsPath, fileName);
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                
                lock (_lock)
                {
                    File.AppendAllLines(filePath, new[] { logLine });
                }
            }
            catch
            {
                // Fallback to console if file logging fails to avoid crashing
                Console.WriteLine("CRITICAL: Failed to write error log.");
            }
        }
    }
}
