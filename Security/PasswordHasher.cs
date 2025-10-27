using BCrypt.Net;

namespace Smart_Roots_Server.Infrastructure.Security
{
    public static class PasswordHasher
    {
        // Work factor 12 is a good default for servers today.
        private const int WorkFactor = 12;

        public static string Hash(string password)
        {
            // Never log password. BCrypt generates its own salt per hash.
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
        }

        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return false;
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}
