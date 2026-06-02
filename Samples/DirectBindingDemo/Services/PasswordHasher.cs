using System.Security.Cryptography;
using System.Text;

namespace DirectBindingDemo.Services;

/// <summary>
/// Simple password hashing service using SHA256.
/// NOTE: In production, use a proper password hashing algorithm like BCrypt or Argon2.
/// </summary>
public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }
}
