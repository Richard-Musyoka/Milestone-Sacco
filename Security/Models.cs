namespace SaccoManagementSystem.Security;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string UserName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? LegacyPassword { get; set; }
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public bool TotpEnabled { get; set; }
    public string? TotpSecretProtected { get; set; }
    public long TotpLastStep { get; set; }
    public bool OtpFallbackEnabled { get; set; }
    public DateTime? PasswordChangedUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }

    public string FullName => string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : Email;
    public bool IsLocked => LockoutEndUtc.HasValue && LockoutEndUtc.Value > DateTime.UtcNow;
}

public sealed class SecurityPolicy
{
    public int PasswordExpiryDays { get; set; } = 90;
    public int FailedAttemptsBeforeLockout { get; set; } = 5;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public bool TwoFactorRequired { get; set; }
    public string PasswordComplexity { get; set; } = "Medium";
    public int PasswordHistoryCount { get; set; } = 5;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = true;
}

public sealed record PasskeyInfo(int Id, string Name, DateTime CreatedUtc, DateTime? LastUsedUtc);
public sealed record DeviceInfo(int Id, string Label, string UserAgent, string Ip, bool Trusted, DateTime? TrustedUntilUtc, DateTime FirstSeenUtc, DateTime LastSeenUtc, bool IsCurrent);
public sealed record SessionInfo(Guid Id, string DeviceLabel, string Ip, DateTime CreatedUtc, DateTime LastSeenUtc, bool IsCurrent);
public sealed record LoginEvent(long Id, string EventType, bool Success, string Ip, string Device, string? Detail, DateTime CreatedUtc);

public sealed class PasskeyRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKeySpki { get; set; } = Array.Empty<byte>();
    public int Algorithm { get; set; }
    public long SignCount { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Information about the caller's browser, used for device recognition and history.</summary>
public sealed record ClientInfo(string Ip, string UserAgent)
{
    public string Label => DeviceLabels.From(UserAgent);
}

public static class DeviceLabels
{
    public static string From(string? ua)
    {
        ua ??= "";
        string browser =
            ua.Contains("Edg/") ? "Edge" :
            ua.Contains("OPR/") || ua.Contains("Opera") ? "Opera" :
            ua.Contains("Firefox/") ? "Firefox" :
            ua.Contains("Chrome/") ? "Chrome" :
            ua.Contains("Safari/") ? "Safari" : "Browser";
        string os =
            ua.Contains("Windows") ? "Windows" :
            ua.Contains("Android") ? "Android" :
            ua.Contains("iPhone") || ua.Contains("iPad") ? "iOS" :
            ua.Contains("Mac OS") ? "macOS" :
            ua.Contains("Linux") ? "Linux" : "Unknown OS";
        return $"{browser} on {os}";
    }
}

public static class Mask
{
    public static string Email(string? e)
    {
        if (string.IsNullOrEmpty(e) || !e.Contains('@')) return "";
        var parts = e.Split('@');
        var local = parts[0];
        var shown = local.Length <= 2 ? local[..1] : local[..2];
        return shown + new string('•', Math.Max(2, local.Length - shown.Length)) + "@" + parts[1];
    }

    public static string Phone(string? p)
    {
        if (string.IsNullOrEmpty(p)) return "";
        var digits = new string(p.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "••••";
        return "••• ••• " + digits[^3..];
    }
}
