using System.Security.Cryptography;
using System.Text;

namespace AxarDB.Helpers
{
    public static class ScriptUtils
    {
        public static string MD5(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static string SHA256(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static string ToString(object obj)
        {
            return obj?.ToString() ?? "";
        }

        public static int RandomNumber(int min, int max)
        {
            return Random.Shared.Next(min, max);
        }

        public static decimal RandomDecimal(string minStr, string maxStr)
        {
            if (!decimal.TryParse(minStr, out var min) || !decimal.TryParse(maxStr, out var max))
                return 0;
            
            // Decimal random generation using double approximation for simplicity within range
            // For higher precision needs, a custom implementation would be required but this fits general use
            var randomDouble = Random.Shared.NextDouble();
            return min + (decimal)randomDouble * (max - min);
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }

        public static string ToBase64(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        public static string FromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            try {
                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            } catch { return ""; }
        }

        public static string Encrypt(string text, string salt)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(salt)) return "";
            // Simple XOR + Base64 for demonstration "encryption" as per common lightweight requirements
            // For strong security, AES should be used, but keeping it simple/portable as utility
            // If user meant strong encryption we would use AES. 
            // Given "salt" parameter, let's do a basic AES-like or salted hash? 
            // The prompt asks for "encrypt" into "encryptedString". Let's use AES with salt derived Key/IV.
            
            try 
            {
                using var aes = Aes.Create();
                var saltBytes = Encoding.UTF8.GetBytes(salt);
                // Derive key and IV from salt (insecure for prod but fits "salt" param usage without separate pass)
                // Using SHA256 of salt to get 32 bytes key, and MD5 of salt for 16 bytes IV
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var md5 = System.Security.Cryptography.MD5.Create();
                
                aes.Key = sha256.ComputeHash(saltBytes);
                aes.IV = md5.ComputeHash(saltBytes);

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(text);
                }
                return Convert.ToBase64String(ms.ToArray());
            } 
            catch { return ""; }
        }

        public static string Decrypt(string encryptedBase64, string salt)
        {
            if (string.IsNullOrEmpty(encryptedBase64) || string.IsNullOrEmpty(salt)) return "";
            try 
            {
                using var aes = Aes.Create();
                var saltBytes = Encoding.UTF8.GetBytes(salt);
                
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var md5 = System.Security.Cryptography.MD5.Create();
                
                aes.Key = sha256.ComputeHash(saltBytes);
                aes.IV = md5.ComputeHash(saltBytes);

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(Convert.FromBase64String(encryptedBase64));
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            } 
            catch { return ""; }
        }

        public static string[] Split(string text, string separator)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Split(new[] { separator }, StringSplitOptions.None);
        }

        public static decimal ToDecimal(string val)
        {
            if (decimal.TryParse(val, out var result)) return result;
            return 0;
        }
    }
}
