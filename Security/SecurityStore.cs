using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace SaccoManagementSystem.Security;

/// <summary>All security-related persistence. Plain ADO.NET, parameterised.</summary>
public sealed class SecurityStore
{
    private readonly IDbFactory _db;
    public SecurityStore(IDbFactory db) => _db = db;

    public static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    // ---------------------------------------------------------------- users
    private const string UserCols = @"Id, Email, ISNULL(UserName, Email) AS UserName, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName,
        PhoneNumber, PasswordHash, Password, Role, IsActive, FailedLoginCount, LockoutEndUtc, TotpEnabled, TotpSecretProtected, TotpLastStep,
        OtpFallbackEnabled, PasswordChangedUtc, LastLoginUtc";

    private static AppUser MapUser(DbDataReader r) => new()
    {
        Id = r.Int("Id"),
        Email = r.Str("Email"),
        UserName = r.Str("UserName"),
        FirstName = r.Str("FirstName"),
        LastName = r.Str("LastName"),
        Phone = r.StrN("PhoneNumber"),
        PasswordHash = r.StrN("PasswordHash"),
        LegacyPassword = r.StrN("Password"),
        Role = r.Str("Role"),
        IsActive = r.Bool("IsActive"),
        FailedLoginCount = r.Int("FailedLoginCount"),
        LockoutEndUtc = r.DateN("LockoutEndUtc"),
        TotpEnabled = r.Bool("TotpEnabled"),
        TotpSecretProtected = r.StrN("TotpSecretProtected"),
        TotpLastStep = r.Long("TotpLastStep"),
        OtpFallbackEnabled = r.Bool("OtpFallbackEnabled"),
        PasswordChangedUtc = r.DateN("PasswordChangedUtc"),
        LastLoginUtc = r.DateN("LastLoginUtc"),
    };

    public async Task<AppUser?> GetUserByEmailAsync(string email)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync($"SELECT TOP 1 {UserCols} FROM Users WHERE Email = @e", MapUser, ("e", email.Trim()));
    }

    public async Task<AppUser?> GetUserAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync($"SELECT TOP 1 {UserCols} FROM Users WHERE Id = @id", MapUser, ("id", id));
    }

    public async Task<int> CountUsersAsync()
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int>("SELECT COUNT(*) FROM Users");
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Email = @e", ("e", email.Trim())) > 0;
    }

    public async Task<int> CreateUserAsync(string first, string? middle, string last, string userName, string email, string? phone, string passwordHash, string role)
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int>(@"
            INSERT INTO Users (FirstName, MiddleName, LastName, UserName, Password, PasswordHash, Email, PhoneNumber, CreatedDate, Role, PasswordChangedUtc)
            OUTPUT INSERTED.Id
            VALUES (@f, @m, @l, @u, '', @h, @e, @p, GETDATE(), @r, SYSUTCDATETIME())",
            ("f", first), ("m", middle), ("l", last), ("u", userName), ("h", passwordHash), ("e", email.Trim()), ("p", phone), ("r", role));
    }

    public async Task RegisterFailureAsync(int userId, int threshold, int lockMinutes)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"UPDATE Users SET FailedLoginCount = FailedLoginCount + 1,
            LockoutEndUtc = CASE WHEN FailedLoginCount + 1 >= @t THEN DATEADD(MINUTE, @m, SYSUTCDATETIME()) ELSE LockoutEndUtc END
            WHERE Id = @id", ("t", threshold), ("m", lockMinutes), ("id", userId));
    }

    public async Task RegisterSuccessAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET FailedLoginCount = 0, LockoutEndUtc = NULL, LastLoginUtc = SYSUTCDATETIME() WHERE Id = @id", ("id", userId));
    }

    public async Task SetPasswordAsync(int userId, string hash, int historyKeep)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET PasswordHash = @h, Password = '', PasswordChangedUtc = SYSUTCDATETIME() WHERE Id = @id", ("h", hash), ("id", userId));
        await c.ExecAsync("INSERT INTO UserPasswordHistory (UserId, PasswordHash) VALUES (@id, @h)", ("id", userId), ("h", hash));
        await c.ExecAsync(@"DELETE FROM UserPasswordHistory WHERE UserId = @id AND Id NOT IN
            (SELECT TOP (@k) Id FROM UserPasswordHistory WHERE UserId = @id ORDER BY Id DESC)", ("id", userId), ("k", Math.Max(1, historyKeep)));
    }

    public async Task<List<string>> RecentPasswordHashesAsync(int userId, int count)
    {
        if (count <= 0) return new();
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync("SELECT TOP (@k) PasswordHash FROM UserPasswordHistory WHERE UserId = @id ORDER BY Id DESC",
            r => r.Str("PasswordHash"), ("k", count), ("id", userId));
    }

    public async Task UpdateProfileAsync(int userId, string? first, string? last, string? phone)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"UPDATE Users SET FirstName = COALESCE(NULLIF(@f,''), FirstName), LastName = COALESCE(NULLIF(@l,''), LastName),
            PhoneNumber = @p WHERE Id = @id", ("f", first), ("l", last), ("p", string.IsNullOrEmpty(phone) ? null : phone), ("id", userId));
    }

    public async Task SetTotpPendingAsync(int userId, string protectedSecret)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET TotpSecretProtected = @s, TotpEnabled = 0, TotpLastStep = 0 WHERE Id = @id", ("s", protectedSecret), ("id", userId));
    }

    public async Task EnableTotpAsync(int userId, long step)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET TotpEnabled = 1, TotpLastStep = @s WHERE Id = @id", ("s", step), ("id", userId));
    }

    public async Task DisableTotpAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET TotpEnabled = 0, TotpSecretProtected = NULL, TotpLastStep = 0 WHERE Id = @id", ("id", userId));
        await c.ExecAsync("DELETE FROM UserBackupCodes WHERE UserId = @id", ("id", userId));
    }

    /// <summary>Atomically advances the last-used step. False if it was already used (replay).</summary>
    public async Task<bool> AdvanceTotpStepAsync(int userId, long step)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecAsync("UPDATE Users SET TotpLastStep = @s WHERE Id = @id AND TotpLastStep < @s", ("s", step), ("id", userId)) == 1;
    }

    public async Task SetOtpFallbackAsync(int userId, bool enabled)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET OtpFallbackEnabled = @v WHERE Id = @id", ("v", enabled), ("id", userId));
    }

    // -------------------------------------------------------- backup codes
    public async Task ReplaceBackupCodesAsync(int userId, IEnumerable<string> plainCodes)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("DELETE FROM UserBackupCodes WHERE UserId = @id", ("id", userId));
        foreach (var code in plainCodes)
            await c.ExecAsync("INSERT INTO UserBackupCodes (UserId, CodeHash) VALUES (@id, @h)", ("id", userId), ("h", Totp.HashBackup(code)));
    }

    public async Task<bool> ConsumeBackupCodeAsync(int userId, string code)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecAsync("UPDATE UserBackupCodes SET UsedUtc = SYSUTCDATETIME() WHERE UserId = @id AND CodeHash = @h AND UsedUtc IS NULL",
            ("id", userId), ("h", Totp.HashBackup(code))) == 1;
    }

    public async Task<int> BackupCodesLeftAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int>("SELECT COUNT(*) FROM UserBackupCodes WHERE UserId = @id AND UsedUtc IS NULL", ("id", userId));
    }

    // ------------------------------------------------------------ passkeys
    private static PasskeyRecord MapKey(DbDataReader r) => new()
    {
        Id = r.Int("Id"), UserId = r.Int("UserId"), CredentialId = r.Bytes("CredentialId"), PublicKeySpki = r.Bytes("PublicKeySpki"),
        Algorithm = r.Int("Algorithm"), SignCount = r.Long("SignCount"), Name = r.Str("Name")
    };

    public async Task<List<PasskeyRecord>> GetPasskeysAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync("SELECT * FROM UserPasskeys WHERE UserId = @id", MapKey, ("id", userId));
    }

    public async Task<List<PasskeyInfo>> ListPasskeysAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync("SELECT Id, Name, CreatedUtc, LastUsedUtc FROM UserPasskeys WHERE UserId = @id ORDER BY CreatedUtc DESC",
            r => new PasskeyInfo(r.Int("Id"), r.Str("Name"), r.Date("CreatedUtc"), r.DateN("LastUsedUtc")), ("id", userId));
    }

    public async Task<PasskeyRecord?> GetPasskeyByCredentialAsync(byte[] credId)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync("SELECT TOP 1 * FROM UserPasskeys WHERE CredentialId = @c", MapKey, ("c", credId));
    }

    public async Task AddPasskeyAsync(int userId, RegisteredCredential cred, string name)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"INSERT INTO UserPasskeys (UserId, CredentialId, PublicKeySpki, Algorithm, SignCount, Name)
            VALUES (@u, @cid, @pk, @alg, @sc, @n)", ("u", userId), ("cid", cred.CredentialId), ("pk", cred.PublicKeySpki),
            ("alg", cred.Algorithm), ("sc", cred.SignCount), ("n", name));
    }

    public async Task TouchPasskeyAsync(int id, long signCount)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE UserPasskeys SET SignCount = @s, LastUsedUtc = SYSUTCDATETIME() WHERE Id = @id", ("s", signCount), ("id", id));
    }

    public async Task<bool> RemovePasskeyAsync(int userId, int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecAsync("DELETE FROM UserPasskeys WHERE Id = @id AND UserId = @u", ("id", id), ("u", userId)) > 0;
    }

    // ------------------------------------------------------------- devices
    public async Task<int?> FindTrustedDeviceAsync(int userId, string tokenHash)
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int?>(@"SELECT TOP 1 Id FROM UserDevices WHERE UserId = @u AND TokenHash = @t
            AND Trusted = 1 AND TrustedUntilUtc > SYSUTCDATETIME()", ("u", userId), ("t", tokenHash));
    }

    public async Task<int> UpsertDeviceAsync(int userId, string tokenHash, ClientInfo ci, bool trust, int trustDays)
    {
        await using var c = await _db.OpenAsync();
        var id = await c.ScalarAsync<int?>("SELECT TOP 1 Id FROM UserDevices WHERE UserId = @u AND TokenHash = @t", ("u", userId), ("t", tokenHash));
        if (id.HasValue)
        {
            await c.ExecAsync(@"UPDATE UserDevices SET LastSeenUtc = SYSUTCDATETIME(), LastIp = @ip, UserAgent = @ua,
                Trusted = CASE WHEN @trust = 1 THEN 1 ELSE Trusted END,
                TrustedUntilUtc = CASE WHEN @trust = 1 THEN DATEADD(DAY, @d, SYSUTCDATETIME()) ELSE TrustedUntilUtc END
                WHERE Id = @id", ("ip", ci.Ip), ("ua", Trim(ci.UserAgent, 400)), ("trust", trust), ("d", trustDays), ("id", id.Value));
            return id.Value;
        }
        return await c.ScalarAsync<int>(@"INSERT INTO UserDevices (UserId, TokenHash, Label, UserAgent, LastIp, Trusted, TrustedUntilUtc)
            OUTPUT INSERTED.Id VALUES (@u, @t, @l, @ua, @ip, @trust, CASE WHEN @trust = 1 THEN DATEADD(DAY, @d, SYSUTCDATETIME()) ELSE NULL END)",
            ("u", userId), ("t", tokenHash), ("l", ci.Label), ("ua", Trim(ci.UserAgent, 400)), ("ip", ci.Ip), ("trust", trust), ("d", trustDays));
    }

    public async Task<List<DeviceInfo>> ListDevicesAsync(int userId, string? currentTokenHash)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT Id, Label, ISNULL(UserAgent,'') AS UserAgent, ISNULL(LastIp,'') AS LastIp, Trusted, TrustedUntilUtc, FirstSeenUtc, LastSeenUtc, TokenHash
            FROM UserDevices WHERE UserId = @u ORDER BY LastSeenUtc DESC",
            r => new DeviceInfo(r.Int("Id"), r.Str("Label"), r.Str("UserAgent"), r.Str("LastIp"),
                r.Bool("Trusted") && (r.DateN("TrustedUntilUtc") ?? DateTime.MinValue) > DateTime.UtcNow,
                r.DateN("TrustedUntilUtc"), r.Date("FirstSeenUtc"), r.Date("LastSeenUtc"),
                currentTokenHash != null && r.Str("TokenHash") == currentTokenHash), ("u", userId));
    }

    public async Task<bool> RemoveDeviceAsync(int userId, int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecAsync("DELETE FROM UserDevices WHERE Id = @id AND UserId = @u", ("id", id), ("u", userId)) > 0;
    }

    public async Task UntrustAllDevicesAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE UserDevices SET Trusted = 0, TrustedUntilUtc = NULL WHERE UserId = @u", ("u", userId));
    }

    // ------------------------------------------------------------ sessions
    public async Task<Guid> CreateSessionAsync(int userId, int? deviceId, ClientInfo ci, int minutes)
    {
        var id = Guid.NewGuid();
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"INSERT INTO UserSessions (Id, UserId, DeviceId, DeviceLabel, Ip, ExpiresUtc)
            VALUES (@id, @u, @d, @l, @ip, DATEADD(MINUTE, @m, SYSUTCDATETIME()))",
            ("id", id), ("u", userId), ("d", deviceId), ("l", ci.Label), ("ip", ci.Ip), ("m", minutes));
        return id;
    }

    /// <summary>True when the session exists, isn't revoked and is within its idle window; slides the window forward.</summary>
    public async Task<bool> ValidateAndTouchSessionAsync(Guid id, int userId, int idleMinutes)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecAsync(@"UPDATE UserSessions SET LastSeenUtc = SYSUTCDATETIME(), ExpiresUtc = DATEADD(MINUTE, @m, SYSUTCDATETIME())
            WHERE Id = @id AND UserId = @u AND RevokedUtc IS NULL AND ExpiresUtc > SYSUTCDATETIME()
              AND LastSeenUtc < DATEADD(SECOND, -30, SYSUTCDATETIME())", ("m", idleMinutes), ("id", id), ("u", userId)) == 1
            || await c.ScalarAsync<int>("SELECT COUNT(*) FROM UserSessions WHERE Id = @id AND UserId = @u AND RevokedUtc IS NULL AND ExpiresUtc > SYSUTCDATETIME()",
                ("id", id), ("u", userId)) > 0;
    }

    public async Task RevokeSessionAsync(int userId, Guid id)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE UserSessions SET RevokedUtc = SYSUTCDATETIME() WHERE Id = @id AND UserId = @u AND RevokedUtc IS NULL", ("id", id), ("u", userId));
    }

    public async Task RevokeOtherSessionsAsync(int userId, Guid keep)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE UserSessions SET RevokedUtc = SYSUTCDATETIME() WHERE UserId = @u AND Id <> @k AND RevokedUtc IS NULL", ("u", userId), ("k", keep));
    }

    public async Task RevokeAllSessionsAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE UserSessions SET RevokedUtc = SYSUTCDATETIME() WHERE UserId = @u AND RevokedUtc IS NULL", ("u", userId));
    }

    public async Task<List<SessionInfo>> ListSessionsAsync(int userId, Guid current)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT Id, DeviceLabel, ISNULL(Ip,'') AS Ip, CreatedUtc, LastSeenUtc FROM UserSessions
            WHERE UserId = @u AND RevokedUtc IS NULL AND ExpiresUtc > SYSUTCDATETIME() ORDER BY LastSeenUtc DESC",
            r => new SessionInfo(r.GuidV("Id"), r.Str("DeviceLabel"), r.Str("Ip"), r.Date("CreatedUtc"), r.Date("LastSeenUtc"), r.GuidV("Id") == current), ("u", userId));
    }

    // ------------------------------------------------------------- history
    public async Task LogAsync(int? userId, string? email, string type, bool success, ClientInfo ci, string? detail = null)
    {
        try
        {
            await using var c = await _db.OpenAsync();
            await c.ExecAsync("INSERT INTO LoginHistory (UserId, Email, EventType, Success, Ip, Device, Detail) VALUES (@u, @e, @t, @s, @ip, @d, @x)",
                ("u", userId), ("e", email), ("t", type), ("s", success), ("ip", ci.Ip), ("d", ci.Label), ("x", Trim(detail, 300)));
        }
        catch { /* history must never break sign-in */ }
    }

    public async Task<List<LoginEvent>> HistoryAsync(int userId, int take = 30)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT TOP (@n) Id, EventType, Success, ISNULL(Ip,'') AS Ip, ISNULL(Device,'') AS Device, Detail, CreatedUtc
            FROM LoginHistory WHERE UserId = @u ORDER BY Id DESC",
            r => new LoginEvent(r.Long("Id"), r.Str("EventType"), r.Bool("Success"), r.Str("Ip"), r.Str("Device"), r.StrN("Detail"), r.Date("CreatedUtc")),
            ("n", take), ("u", userId));
    }

    // ---------------------------------------------------------------- OTP
    /// <summary>Adds the delivery-tracking / admin-backup columns if they're missing (idempotent; also shipped as Database/003_email_otp.sql).</summary>
    public async Task EnsureOtpSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"
            IF COL_LENGTH('OtpChallenges','CodeProtected')  IS NULL ALTER TABLE OtpChallenges ADD CodeProtected NVARCHAR(600) NULL;
            IF COL_LENGTH('OtpChallenges','Destination')    IS NULL ALTER TABLE OtpChallenges ADD Destination NVARCHAR(200) NULL;
            IF COL_LENGTH('OtpChallenges','DeliveryStatus') IS NULL ALTER TABLE OtpChallenges ADD DeliveryStatus NVARCHAR(20) NULL;
            IF COL_LENGTH('OtpChallenges','DeliveryError')  IS NULL ALTER TABLE OtpChallenges ADD DeliveryError NVARCHAR(400) NULL;
            IF COL_LENGTH('OtpChallenges','CodePlain')      IS NULL ALTER TABLE OtpChallenges ADD CodePlain NVARCHAR(12) NULL;");
        await c.ExecAsync(@"CREATE OR ALTER VIEW vw_OtpBackup AS
            SELECT o.CreatedUtc, u.Email, o.Channel, o.CodePlain AS Code, o.DeliveryStatus, o.DeliveryError, o.ExpiresUtc
            FROM OtpChallenges o JOIN Users u ON u.Id = o.UserId
            WHERE o.ConsumedUtc IS NULL AND o.ExpiresUtc > SYSUTCDATETIME() AND o.CodePlain IS NOT NULL");
    }

    /// <summary>codeProtected is the code encrypted with ASP.NET Data Protection; only administrators can read it back, and only while it is valid.</summary>
    public async Task<Guid> CreateOtpAsync(int userId, string channel, string code, int minutes = 10, string? codeProtected = null, string? destination = null)
    {
        var id = Guid.NewGuid();
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"UPDATE OtpChallenges SET ConsumedUtc = SYSUTCDATETIME(), CodeProtected = NULL, CodePlain = NULL WHERE UserId = @u AND ConsumedUtc IS NULL", ("u", userId));
        await c.ExecAsync("UPDATE OtpChallenges SET CodeProtected = NULL, CodePlain = NULL WHERE (CodeProtected IS NOT NULL OR CodePlain IS NOT NULL) AND ExpiresUtc < SYSUTCDATETIME()");
        await c.ExecAsync(@"INSERT INTO OtpChallenges (Id, UserId, Channel, CodeHash, ExpiresUtc, CodeProtected, Destination)
                            VALUES (@id, @u, @ch, @h, DATEADD(MINUTE, @m, SYSUTCDATETIME()), @cp, @d)",
            ("id", id), ("u", userId), ("ch", channel), ("h", Sha256Hex(id + ":" + code)), ("m", minutes), ("cp", codeProtected), ("d", Trim(destination, 200)));
        return id;
    }

    /// <summary>Only used when email delivery failed and the operator enabled it: lets the code be read in SSMS (view vw_OtpBackup). Wiped when used or expired.</summary>
    public async Task SetOtpPlainAsync(Guid id, string code)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE OtpChallenges SET CodePlain = @c WHERE Id = @id", ("c", code), ("id", id));
    }

    public async Task SetOtpDeliveryAsync(Guid id, bool ok, string? error)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE OtpChallenges SET DeliveryStatus = @s, DeliveryError = @e WHERE Id = @id",
            ("s", ok ? "sent" : "failed"), ("e", Trim(error, 400)), ("id", id));
    }

    public async Task<bool> VerifyOtpAsync(Guid id, int userId, string code)
    {
        await using var c = await _db.OpenAsync();
        var attempts = await c.ScalarAsync<int?>("SELECT Attempts FROM OtpChallenges WHERE Id = @id AND UserId = @u AND ConsumedUtc IS NULL AND ExpiresUtc > SYSUTCDATETIME()", ("id", id), ("u", userId));
        if (attempts == null || attempts >= 5) return false;
        var ok = await c.ExecAsync("UPDATE OtpChallenges SET ConsumedUtc = SYSUTCDATETIME(), CodeProtected = NULL, CodePlain = NULL WHERE Id = @id AND CodeHash = @h AND ConsumedUtc IS NULL",
            ("id", id), ("h", Sha256Hex(id + ":" + (code ?? "").Trim()))) == 1;
        if (!ok) await c.ExecAsync("UPDATE OtpChallenges SET Attempts = Attempts + 1 WHERE Id = @id", ("id", id));
        return ok;
    }

    /// <summary>For flows where the browser doesn't hold the challenge id (password reset): checks the newest open challenge on a channel.</summary>
    public async Task<bool> VerifyLatestOtpAsync(int userId, string channel, string code)
    {
        Guid id;
        await using (var c = await _db.OpenAsync())
        {
            var found = await c.ScalarAsync<Guid?>("SELECT TOP 1 Id FROM OtpChallenges WHERE UserId = @u AND Channel = @ch AND ConsumedUtc IS NULL AND ExpiresUtc > SYSUTCDATETIME() ORDER BY CreatedUtc DESC",
                ("u", userId), ("ch", channel));
            if (found == null) return false;
            id = found.Value;
        }
        return await VerifyOtpAsync(id, userId, code);
    }

    public sealed record OtpBackupRow(Guid Id, int UserId, string UserName, string Email, string Channel, string? Destination, string? DeliveryStatus, string? DeliveryError, DateTime CreatedUtc, DateTime ExpiresUtc, string? CodeProtected);

    /// <summary>Codes that are still valid, newest first (administrators only).</summary>
    public async Task<List<OtpBackupRow>> ListOpenOtpsAsync()
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT o.Id, o.UserId, LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) AS UserName, u.Email, o.Channel, o.Destination,
                    o.DeliveryStatus, o.DeliveryError, o.CreatedUtc, o.ExpiresUtc, o.CodeProtected
                FROM OtpChallenges o JOIN Users u ON u.Id = o.UserId
                WHERE o.ConsumedUtc IS NULL AND o.ExpiresUtc > SYSUTCDATETIME() AND o.CodeProtected IS NOT NULL
                ORDER BY o.CreatedUtc DESC",
            r => new OtpBackupRow(r.GuidV("Id"), r.Int("UserId"), r.Str("UserName"), r.Str("Email"), r.Str("Channel"), r.StrN("Destination"),
                r.StrN("DeliveryStatus"), r.StrN("DeliveryError"), r.Date("CreatedUtc"), r.Date("ExpiresUtc"), r.StrN("CodeProtected")));
    }

    public async Task SetActiveAsync(int userId, bool active)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET IsActive = @a WHERE Id = @id", ("a", active), ("id", userId));
    }

    // -------------------------------------------------------------- policy
    public async Task<SecurityPolicy> GetPolicyAsync()
    {
        try
        {
            await using var c = await _db.OpenAsync();
            var p = await c.FirstAsync(@"SELECT TOP 1 PasswordExpiryDays, FailedAttemptsBeforeLockout, SessionTimeoutMinutes, TwoFactorAuthenticationRequired,
                PasswordComplexity, PasswordHistoryCount, EmailNotificationsEnabled, SMSNotificationsEnabled FROM SaccoSettings ORDER BY Id DESC",
                r => new SecurityPolicy
                {
                    PasswordExpiryDays = r.Int("PasswordExpiryDays"),
                    FailedAttemptsBeforeLockout = Math.Max(1, r.Int("FailedAttemptsBeforeLockout")),
                    SessionTimeoutMinutes = Math.Max(5, r.Int("SessionTimeoutMinutes")),
                    TwoFactorRequired = r.Bool("TwoFactorAuthenticationRequired"),
                    PasswordComplexity = string.IsNullOrEmpty(r.Str("PasswordComplexity")) ? "Medium" : r.Str("PasswordComplexity"),
                    PasswordHistoryCount = r.Int("PasswordHistoryCount"),
                    EmailEnabled = r.Bool("EmailNotificationsEnabled"),
                    SmsEnabled = r.Bool("SMSNotificationsEnabled"),
                });
            return p ?? new SecurityPolicy();
        }
        catch { return new SecurityPolicy(); }
    }

    private static string? Trim(string? s, int max) => s == null ? null : (s.Length <= max ? s : s[..max]);
}
