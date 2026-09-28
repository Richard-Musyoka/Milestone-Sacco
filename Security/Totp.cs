using System.Security.Cryptography;
using System.Text;

namespace SaccoManagementSystem.Security;

/// <summary>RFC 6238 TOTP (SHA1, 6 digits, 30s) with base32 helpers and replay protection via last accepted step.</summary>
public static class Totp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Base32Decode(string s)
    {
        s = s.Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in s)
        {
            int v = Alphabet.IndexOf(c);
            if (v < 0) throw new FormatException("Invalid base32.");
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }

    public static long CurrentStep(DateTime? utcNow = null)
        => new DateTimeOffset(utcNow ?? DateTime.UtcNow).ToUnixTimeSeconds() / 30;

    public static string Compute(byte[] key, long step)
    {
        var msg = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(msg);
        using var hmac = new HMACSHA1(key);
        var h = hmac.ComputeHash(msg);
        int o = h[^1] & 0x0F;
        int code = ((h[o] & 0x7F) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3];
        return (code % 1_000_000).ToString("D6");
    }

    /// <summary>Validates a code within ±1 step. Returns the matched step (must be greater than lastStep) or null.</summary>
    public static long? Validate(string secretBase32, string? code, long lastStep, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Replace(" ", "");
        if (code.Length != 6 || !code.All(char.IsDigit)) return null;
        byte[] key;
        try { key = Base32Decode(secretBase32); } catch { return null; }
        var now = CurrentStep(utcNow);
        for (long s = now - 1; s <= now + 1; s++)
        {
            if (s <= lastStep) continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(key, s)), Encoding.ASCII.GetBytes(code)))
                return s;
        }
        return null;
    }

    public static string OtpAuthUri(string issuer, string account, string secret)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";

    /// <summary>Ten human-friendly backup codes (xxxxx-xxxxx).</summary>
    public static List<string> NewBackupCodes(int count = 10)
    {
        const string chars = "abcdefghjkmnpqrstuvwxyz23456789";
        var list = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var b = RandomNumberGenerator.GetBytes(10);
            var s = new string(b.Select(x => chars[x % chars.Length]).ToArray());
            list.Add(s[..5] + "-" + s[5..]);
        }
        return list;
    }

    public static string NormalizeBackup(string code) => code.Trim().ToLowerInvariant().Replace(" ", "");

    public static string HashBackup(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeBackup(code))));
}
