using System.Security.Cryptography;
using System.Text;

namespace RegistrationDemo.Services;

/// <summary>
/// Simple password hashing service for demo purposes.
/// NOTE: In production, use a proper password hashing library like BCrypt or ASP.NET Core Identity.
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// Hashes a password using SHA256 (demo only - use BCrypt in production!).
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        var computedHash = HashPassword(password);
        return computedHash == hash;
    }
}
