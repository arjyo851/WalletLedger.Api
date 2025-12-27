using System.Security.Cryptography;

namespace WalletLedger.Api.Auth
{
    public static class RefreshTokenGenerator
    {
        public static string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        public static string Hash(string token)
        {
            using var sha = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(token);
            var hashBytes = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
