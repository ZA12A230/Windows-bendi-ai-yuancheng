using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalAIStudio.Services
{
    public class SecurityService
    {
        #region Singleton
        private static readonly Lazy<SecurityService> _instance = new Lazy<SecurityService>(() => new SecurityService());
        public static SecurityService Instance => _instance.Value;
        #endregion

        private readonly string _configPath;
        private AppConfig _config;

        #region DPAPI加密

        public string EncryptWithDPAPI(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI加密失败: {ex.Message}");
                return string.Empty;
            }
        }

        public string DecryptWithDPAPI(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI解密失败: {ex.Message}");
                return string.Empty;
            }
        }

        #endregion

        #region BCrypt密码哈希

        private const int BCryptWorkFactor = 12;

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            return BCrypt.Net.BCrypt.HashPassword(password, BCryptWorkFactor);
        }

        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Token生成与验证

        public string GenerateToken(int length = 32)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] tokenBytes = new byte[length];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            }
        }

        private readonly string _tokenSecret;
        private readonly TimeSpan _tokenExpiry = TimeSpan.FromHours(24);

        public string GenerateAccessToken(string username)
        {
            var payload = $"{username}:{DateTime.UtcNow.Ticks}:{GenerateToken()}";
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = ComputeHMACSHA256(payloadBytes, Encoding.UTF8.GetBytes(_tokenSecret));
            var signature = Convert.ToBase64String(signatureBytes).Replace("+", "-").Replace("/", "_");

            return $"{Convert.ToBase64String(payloadBytes).Replace("+", "-").Replace("/", "_")}.{signature}";
        }

        public bool ValidateAccessToken(string token, out string? username)
        {
            username = null;

            if (string.IsNullOrEmpty(token))
                return false;

            var parts = token.Split('.');
            if (parts.Length != 2)
                return false;

            try
            {
                var payloadJson = parts[0].Replace("-", "+").Replace("_", "/");
                var payloadBytes = Convert.FromBase64String(payloadJson);
                var payload = Encoding.UTF8.GetString(payloadBytes);
                var payloadParts = payload.Split(':');

                if (payloadParts.Length < 2)
                    return false;

                username = payloadParts[0];
                var timestamp = long.Parse(payloadParts[1]);

                var expectedSignature = Convert.ToBase64String(
                    ComputeHMACSHA256(payloadBytes, Encoding.UTF8.GetBytes(_tokenSecret))
                ).Replace("+", "-").Replace("/", "_");

                if (parts[1] != expectedSignature)
                    return false;

                var tokenTime = DateTime.FromBinary(timestamp);
                if (DateTime.UtcNow - tokenTime > _tokenExpiry)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private byte[] ComputeHMACSHA256(byte[] data, byte[] key)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        #endregion

        #region 配置存储

        public SecurityService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            _tokenSecret = EncryptWithDPAPI(Environment.MachineName + Environment.UserName);
            _config = LoadConfig();
        }

        public AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var encryptedJson = File.ReadAllText(_configPath);
                    var decryptedJson = DecryptWithDPAPI(encryptedJson);
                    return JsonSerializer.Deserialize<AppConfig>(decryptedJson) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置加载失败: {ex.Message}");
            }
            return new AppConfig();
        }

        public void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                var encryptedJson = EncryptWithDPAPI(json);
                File.WriteAllText(_configPath, encryptedJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置保存失败: {ex.Message}");
            }
        }

        public void SetPassword(string username, string password)
        {
            _config.Username = username;
            _config.PasswordHash = HashPassword(password);
            _config.AccessToken = GenerateAccessToken(username);
            SaveConfig();
        }

        public bool ValidateCredentials(string username, string password)
        {
            if (_config.Username != username)
                return false;

            return VerifyPassword(password, _config.PasswordHash);
        }

        public bool ValidateToken(string token)
        {
            return ValidateAccessToken(token, out _);
        }

        public void SetFrpToken(string token)
        {
            _config.FrpcToken = EncryptWithDPAPI(token);
            SaveConfig();
        }

        public string? GetFrpToken()
        {
            return string.IsNullOrEmpty(_config.FrpcToken) ? null : DecryptWithDPAPI(_config.FrpcToken);
        }

        #endregion
    }

    #region 配置模型

    public class AppConfig
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string FrpcToken { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    #endregion
}

namespace BCrypt.Net
{
    public static class BCrypt
    {
        private const int DefaultWorkFactor = 12;

        public static string HashPassword(string password, int workFactor = DefaultWorkFactor)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var saltB64 = Convert.ToBase64String(salt);
            return $"$2a${workFactor}${saltB64.Replace("+", ".").Substring(0, 22)}${ComputeHash(password, salt)}";
        }

        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                var parts = hash.Split('$');
                if (parts.Length != 4)
                    return false;

                int workFactor = int.Parse(parts[1].Substring(2));
                string salt = $"$2a${workFactor}${parts[2]}$";
                string computedHash = ComputeHash(password, Convert.FromBase64String(salt.Replace(".", "+") + "=="));

                return timingSafeEqual(computedHash, parts[3]);
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeHash(string password, byte[] salt)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] combined = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, combined, salt.Length, passwordBytes.Length);

            byte[] hash = combined;
            for (int i = 0; i < 16; i++)
            {
                byte[] temp = new byte[combined.Length];
                Buffer.BlockCopy(combined, 0, temp, 0, combined.Length);

                for (int j = 0; j < 64; j++)
                {
                    if ((j & 1) == 0)
                    {
                        temp = XORBytes(temp, passwordBytes);
                    }
                    else
                    {
                        temp = XORBytes(temp, salt);
                    }
                }

                hash = XORBytes(hash, temp);
            }

            return Convert.ToBase64String(hash).Replace("+", ".").Substring(0, 31);
        }

        private static byte[] XORBytes(byte[] a, byte[] b)
        {
            byte[] result = new byte[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)((i < a.Length ? a[i] : 0) ^ (i < b.Length ? b[i] : 0));
            }
            return result;
        }

        private static bool timingSafeEqual(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);

            byte result = 0;
            for (int i = 0; i < aBytes.Length; i++)
            {
                result |= (byte)(aBytes[i] ^ bBytes[i]);
            }
            return result == 0;
        }
    }
}
