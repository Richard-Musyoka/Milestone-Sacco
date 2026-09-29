using System.Security.Cryptography;

namespace SaccoManagementSystem.Security;

/// <summary>PBKDF2-SHA256 password hashing. Format: v1.{iterations}.{saltB64}.{hashB64}</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iter)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    public static bool ConstantTimeEquals(string a, string b)
    {
        var x = System.Text.Encoding.UTF8.GetBytes(a);
        var y = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(x, y);
    }

    /// <summary>Returns an error message, or null when the password satisfies the policy.</summary>
    public static string? Validate(string? password, string complexity)
    {
        password ??= "";
        int min = complexity switch { "Low" => 6, "High" => 12, _ => 8 };
        if (password.Length < min) return $"Password must be at least {min} characters.";
        if (complexity is "Medium" or "High")
        {
            if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
                return "Use upper case, lower case and a number.";
        }
        if (complexity == "High" && password.All(char.IsLetterOrDigit))
            return "Add at least one symbol.";
        return null;
    }
}
