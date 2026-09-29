using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace SaccoManagementSystem.Security;

public static class AuthDefaults
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string SessionClaim = "sid";
    public const string AmrClaim = "amr";
    public const string PasswordExpiredClaim = "pwd_expired";
    public const string EnrollClaim = "mfa_enroll";
    public const string DeviceCookie = ".Tajiri.Device";
    public const int TrustDays = 30;
    public const int LockMinutes = 15;
}

public sealed record AuthResult(bool Ok, string Status, string? Message = null, object? Data = null)
{
    public static AuthResult Success(object? data = null) => new(true, "ok", null, data);
    public static AuthResult Fail(string message, string status = "error") => new(false, status, message);
}

public interface IOtpSender
{
    Task SendAsync(string channel, string destination, string code);
}

/// <summary>Development sender: writes the code to the log. Replace with your SMTP / SMS gateway in production.</summary>
public sealed class LoggingOtpSender : IOtpSender
{
    private readonly ILogger<LoggingOtpSender> _log;
    public LoggingOtpSender(ILogger<LoggingOtpSender> log) => _log = log;
    public Task SendAsync(string channel, string destination, string code)
    {
        _log.LogWarning("OTP via {Channel} to {Destination}: {Code}  (LoggingOtpSender – plug in a real gateway)", channel, destination, code);
        return Task.CompletedTask;
    }
}

public sealed class AuthService
{
    private readonly SecurityStore _store;
    private readonly IDataProtector _totpProtector;
    private readonly ITimeLimitedDataProtector _mfaProtector;
    private readonly IMemoryCache _cache;
    private readonly IOtpSender _otp;
    private readonly IConfiguration _cfg;

    public AuthService(SecurityStore store, IDataProtectionProvider dp, IMemoryCache cache, IOtpSender otp, IConfiguration cfg)
    {
        _store = store;
        _totpProtector = dp.CreateProtector("Tajiri.Totp.v1");
        _mfaProtector = dp.CreateProtector("Tajiri.MfaChallenge.v1").ToTimeLimitedDataProtector();
        _cache = cache;
        _otp = otp;
        _cfg = cfg;
    }

    public string IssuerName => _cfg["Brand:Name"] ?? "Tajiri Sacco";

    // ================================================================ helpers
    public static ClientInfo Client(HttpContext ctx)
        => new(ctx.Connection.RemoteIpAddress?.ToString() ?? "", ctx.Request.Headers.UserAgent.ToString());

    public static int? UserId(ClaimsPrincipal p) => int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : null;
    public static Guid SessionId(ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue(AuthDefaults.SessionClaim), out var g) ? g : Guid.Empty;

    private static (string Origin, string RpId) Rp(HttpContext ctx)
        => ($"{ctx.Request.Scheme}://{ctx.Request.Host}", ctx.Request.Host.Host);

    private string? DeviceToken(HttpContext ctx) => ctx.Request.Cookies[AuthDefaults.DeviceCookie];
    private static string? DeviceHash(string? token) => string.IsNullOrEmpty(token) ? null : SecurityStore.Sha256Hex(token);

    private sealed record MfaChallenge(int UserId, bool Remember, bool ViaPassword);

    private string ProtectChallenge(MfaChallenge c) => _mfaProtector.Protect(JsonSerializer.Serialize(c), TimeSpan.FromMinutes(10));

    private MfaChallenge? ReadChallenge(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try { return JsonSerializer.Deserialize<MfaChallenge>(_mfaProtector.Unprotect(token)); }
        catch { return null; }
    }

    // ================================================================= login
    public async Task<AuthResult> LoginAsync(HttpContext ctx, string email, string password, bool remember)
    {
        var ci = Client(ctx);
        var policy = await _store.GetPolicyAsync();
        var user = await _store.GetUserByEmailAsync(email ?? "");

        if (user == null)
        {
            PasswordHasher.Hash(password ?? ""); // equalise timing
            await _store.LogAsync(null, email, "password", false, ci, "Unknown account");
            return AuthResult.Fail("Incorrect email or password.", "invalid");
        }
        if (!user.IsActive)
        {
            await _store.LogAsync(user.Id, user.Email, "password", false, ci, "Account disabled");
            return AuthResult.Fail("This account is disabled. Contact your administrator.", "disabled");
        }
        if (user.IsLocked)
        {
            await _store.LogAsync(user.Id, user.Email, "password", false, ci, "Locked out");
            var mins = Math.Max(1, (int)Math.Ceiling((user.LockoutEndUtc!.Value - DateTime.UtcNow).TotalMinutes));
            return AuthResult.Fail($"Too many attempts. Try again in {mins} minute{(mins == 1 ? "" : "s")}.", "locked");
        }

        bool ok = false;
        bool upgrade = false;
        if (!string.IsNullOrEmpty(user.PasswordHash)) ok = PasswordHasher.Verify(password ?? "", user.PasswordHash);
        else if (!string.IsNullOrEmpty(user.LegacyPassword) && PasswordHasher.ConstantTimeEquals(user.LegacyPassword, password ?? "")) { ok = true; upgrade = true; }

        if (!ok)
        {
            await _store.RegisterFailureAsync(user.Id, policy.FailedAttemptsBeforeLockout, AuthDefaults.LockMinutes);
            await _store.LogAsync(user.Id, user.Email, "password", false, ci, "Wrong password");
            return AuthResult.Fail("Incorrect email or password.", "invalid");
        }

        if (upgrade) await _store.SetPasswordAsync(user.Id, PasswordHasher.Hash(password!), policy.PasswordHistoryCount);

        // second factor?
        var passkeys = await _store.GetPasskeysAsync(user.Id);
        bool hasFactor = user.TotpEnabled || passkeys.Count > 0 || user.OtpFallbackEnabled;

        if (hasFactor)
        {
            var trusted = DeviceHash(DeviceToken(ctx)) is { } h && await _store.FindTrustedDeviceAsync(user.Id, h) != null;
            if (!trusted)
            {
                var methods = new List<string>();
                if (user.TotpEnabled) { methods.Add("totp"); methods.Add("backup"); }
                if (passkeys.Count > 0) methods.Add("passkey");
                if (user.OtpFallbackEnabled || user.TotpEnabled || passkeys.Count > 0)
                {
                    if (policy.EmailEnabled && !string.IsNullOrEmpty(user.Email)) methods.Add("email");
                    if (policy.SmsEnabled && !string.IsNullOrEmpty(user.Phone)) methods.Add("sms");
                }
                return new AuthResult(true, "mfa", null, new
                {
                    token = ProtectChallenge(new MfaChallenge(user.Id, remember, true)),
                    methods = methods.Distinct(),
                    emailHint = Mask.Email(user.Email),
                    phoneHint = Mask.Phone(user.Phone),
                });
            }
            return await FinishSignInAsync(ctx, user, "pwd+trusted", remember, false, policy);
        }

        return await FinishSignInAsync(ctx, user, "pwd", remember, false, policy);
    }

    // ============================================================ MFA: TOTP
    public async Task<AuthResult> VerifyTotpAsync(HttpContext ctx, string token, string code, bool trustDevice)
    {
        var ch = ReadChallenge(token);
        if (ch == null) return AuthResult.Fail("Your sign-in expired. Start again.", "expired");
        var ci = Client(ctx);
        var user = await _store.GetUserAsync(ch.UserId);
        if (user == null || !user.IsActive || !user.TotpEnabled || user.TotpSecretProtected == null) return AuthResult.Fail("Verification failed.", "invalid");
        if (user.IsLocked) return AuthResult.Fail("Account temporarily locked.", "locked");
        var policy = await _store.GetPolicyAsync();

        string secret;
        try { secret = _totpProtector.Unprotect(user.TotpSecretProtected); } catch { return AuthResult.Fail("Verification failed.", "invalid"); }

        var step = Totp.Validate(secret, code, user.TotpLastStep);
        if (step == null || !await _store.AdvanceTotpStepAsync(user.Id, step.Value))
        {
            await _store.RegisterFailureAsync(user.Id, policy.FailedAttemptsBeforeLockout, AuthDefaults.LockMinutes);
            await _store.LogAsync(user.Id, user.Email, "totp", false, ci, "Bad code");
            return AuthResult.Fail("That code isn't right. Check your authenticator app and try again.", "invalid");
        }
        return await FinishSignInAsync(ctx, user, "totp", ch.Remember, trustDevice, policy);
    }

    public async Task<AuthResult> VerifyBackupAsync(HttpContext ctx, string token, string code, bool trustDevice)
    {
        var ch = ReadChallenge(token);
        if (ch == null) return AuthResult.Fail("Your sign-in expired. Start again.", "expired");
        var user = await _store.GetUserAsync(ch.UserId);
        if (user == null || !user.IsActive || user.IsLocked) return AuthResult.Fail("Verification failed.", "invalid");
        var policy = await _store.GetPolicyAsync();
        if (!await _store.ConsumeBackupCodeAsync(user.Id, code ?? ""))
        {
            await _store.RegisterFailureAsync(user.Id, policy.FailedAttemptsBeforeLockout, AuthDefaults.LockMinutes);
            await _store.LogAsync(user.Id, user.Email, "backup-code", false, Client(ctx), "Bad code");
            return AuthResult.Fail("That backup code isn't valid or was already used.", "invalid");
        }
        await _store.LogAsync(user.Id, user.Email, "backup-code", true, Client(ctx), "Backup code used");
        return await FinishSignInAsync(ctx, user, "backup", ch.Remember, trustDevice, policy);
    }

    // ========================================================= MFA: email/SMS
    public async Task<AuthResult> SendOtpAsync(HttpContext ctx, string token, string channel)
    {
        var ch = ReadChallenge(token);
        if (ch == null) return AuthResult.Fail("Your sign-in expired. Start again.", "expired");
        var user = await _store.GetUserAsync(ch.UserId);
        if (user == null || !user.IsActive) return AuthResult.Fail("Could not send a code.", "invalid");
        channel = channel == "sms" ? "sms" : "email";
        var dest = channel == "sms" ? user.Phone : user.Email;
        if (string.IsNullOrEmpty(dest)) return AuthResult.Fail($"No {(channel == "sms" ? "phone number" : "email")} on file.", "invalid");

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var id = await _store.CreateOtpAsync(user.Id, channel, code);
        await _otp.SendAsync(channel, dest, code);
        await _store.LogAsync(user.Id, user.Email, "otp-sent", true, Client(ctx), channel);
        return AuthResult.Success(new { otpId = id, sentTo = channel == "sms" ? Mask.Phone(dest) : Mask.Email(dest) });
    }

    public async Task<AuthResult> VerifyOtpAsync(HttpContext ctx, string token, Guid otpId, string code, bool trustDevice)
    {
        var ch = ReadChallenge(token);
        if (ch == null) return AuthResult.Fail("Your sign-in expired. Start again.", "expired");
        var user = await _store.GetUserAsync(ch.UserId);
        if (user == null || !user.IsActive || user.IsLocked) return AuthResult.Fail("Verification failed.", "invalid");
        var policy = await _store.GetPolicyAsync();
        if (!await _store.VerifyOtpAsync(otpId, user.Id, code ?? ""))
        {
            await _store.LogAsync(user.Id, user.Email, "otp", false, Client(ctx), "Bad code");
            return AuthResult.Fail("That code isn't right or has expired.", "invalid");
        }
        return await FinishSignInAsync(ctx, user, "otp", ch.Remember, trustDevice, policy);
    }

    // ============================================================== Passkeys
    private sealed record PendingWebAuthn(string Challenge, int? UserId, string Purpose);

    private (string Id, string Challenge) NewPending(int? userId, string purpose)
    {
        var id = Guid.NewGuid().ToString("N");
        var challenge = WebAuthn.NewChallenge();
        _cache.Set("wa:" + id, new PendingWebAuthn(challenge, userId, purpose), TimeSpan.FromMinutes(5));
        return (id, challenge);
    }

    private PendingWebAuthn? TakePending(string id, string purpose)
    {
        if (!_cache.TryGetValue("wa:" + id, out PendingWebAuthn? p) || p == null || p.Purpose != purpose) return null;
        _cache.Remove("wa:" + id);
        return p;
    }

    /// <summary>Options for signing in. With an MFA token the request is limited to that user's keys; without one it's usernameless.</summary>
    public async Task<AuthResult> PasskeyLoginOptionsAsync(HttpContext ctx, string? mfaToken)
    {
        int? uid = null;
        var allow = new List<object>();
        if (!string.IsNullOrEmpty(mfaToken))
        {
            var ch = ReadChallenge(mfaToken);
            if (ch == null) return AuthResult.Fail("Your sign-in expired. Start again.", "expired");
            uid = ch.UserId;
            foreach (var k in await _store.GetPasskeysAsync(uid.Value))
                allow.Add(new { type = "public-key", id = WebAuthn.B64Url(k.CredentialId) });
        }
        var (id, challenge) = NewPending(uid, "login");
        return AuthResult.Success(new
        {
            challengeId = id,
            publicKey = new { challenge, rpId = Rp(ctx).RpId, timeout = 60000, userVerification = "required", allowCredentials = allow }
        });
    }

    public sealed record AssertionDto(string ChallengeId, string Id, string ClientDataJSON, string AuthenticatorData, string Signature, string? MfaToken, bool Remember, bool TrustDevice);

    public async Task<AuthResult> PasskeyLoginVerifyAsync(HttpContext ctx, AssertionDto dto)
    {
        var pending = TakePending(dto.ChallengeId, "login");
        if (pending == null) return AuthResult.Fail("That request expired. Try again.", "expired");
        var ci = Client(ctx);
        try
        {
            var key = await _store.GetPasskeyByCredentialAsync(WebAuthn.FromB64Url(dto.Id));
            if (key == null) return AuthResult.Fail("This passkey isn't registered here.", "invalid");
            if (pending.UserId.HasValue && pending.UserId.Value != key.UserId) return AuthResult.Fail("Passkey belongs to a different account.", "invalid");

            var user = await _store.GetUserAsync(key.UserId);
            if (user == null || !user.IsActive) return AuthResult.Fail("Account unavailable.", "disabled");
            if (user.IsLocked) return AuthResult.Fail("Account temporarily locked.", "locked");

            var (origin, rpId) = Rp(ctx);
            var counter = WebAuthn.VerifyAssertion(key, WebAuthn.FromB64Url(dto.ClientDataJSON), WebAuthn.FromB64Url(dto.AuthenticatorData),
                WebAuthn.FromB64Url(dto.Signature), pending.Challenge, origin, rpId);
            await _store.TouchPasskeyAsync(key.Id, counter);

            bool remember = dto.Remember;
            if (!string.IsNullOrEmpty(dto.MfaToken)) remember = ReadChallenge(dto.MfaToken)?.Remember ?? remember;
            var policy = await _store.GetPolicyAsync();
            return await FinishSignInAsync(ctx, user, "passkey", remember, dto.TrustDevice, policy);
        }
        catch (Exception ex) when (ex is SecurityException or FormatException or JsonException or KeyNotFoundException or System.Security.Cryptography.CryptographicException)
        {
            await _store.LogAsync(null, null, "passkey", false, ci, ex.Message);
            return AuthResult.Fail("Passkey check failed.", "invalid");
        }
    }

    public async Task<AuthResult> PasskeyRegisterOptionsAsync(HttpContext ctx, AppUser user)
    {
        var (id, challenge) = NewPending(user.Id, "register");
        var existing = (await _store.GetPasskeysAsync(user.Id)).Select(k => new { type = "public-key", id = WebAuthn.B64Url(k.CredentialId) }).ToList();
        return AuthResult.Success(new
        {
            challengeId = id,
            publicKey = new
            {
                challenge,
                rp = new { name = IssuerName, id = Rp(ctx).RpId },
                user = new { id = WebAuthn.B64Url(BitConverter.GetBytes(user.Id)), name = user.Email, displayName = user.FullName },
                pubKeyCredParams = new[] { new { type = "public-key", alg = -7 }, new { type = "public-key", alg = -257 } },
                timeout = 60000,
                attestation = "none",
                excludeCredentials = existing,
                authenticatorSelection = new { residentKey = "preferred", userVerification = "required" }
            }
        });
    }

    public sealed record AttestationDto(string ChallengeId, string ClientDataJSON, string AttestationObject, string? Name);

    public async Task<AuthResult> PasskeyRegisterVerifyAsync(HttpContext ctx, AppUser user, AttestationDto dto)
    {
        var pending = TakePending(dto.ChallengeId, "register");
        if (pending == null || pending.UserId != user.Id) return AuthResult.Fail("That request expired. Try again.", "expired");
        try
        {
            var (origin, rpId) = Rp(ctx);
            var cred = WebAuthn.VerifyRegistration(WebAuthn.FromB64Url(dto.ClientDataJSON), WebAuthn.FromB64Url(dto.AttestationObject), pending.Challenge, origin, rpId);
            var name = string.IsNullOrWhiteSpace(dto.Name) ? DeviceLabels.From(ctx.Request.Headers.UserAgent) : dto.Name!.Trim();
            await _store.AddPasskeyAsync(user.Id, cred, name.Length > 100 ? name[..100] : name);
            await _store.LogAsync(user.Id, user.Email, "passkey-added", true, Client(ctx), name);
            return AuthResult.Success();
        }
        catch (Exception ex) when (ex is SecurityException or FormatException or KeyNotFoundException or System.Security.Cryptography.CryptographicException or Microsoft.Data.SqlClient.SqlException or JsonException)
        {
            return AuthResult.Fail("Couldn't register that passkey: " + (ex is Microsoft.Data.SqlClient.SqlException ? "it may already be registered." : ex.Message));
        }
    }

    // ==================================================== TOTP enrolment
    public async Task<AuthResult> TotpBeginAsync(AppUser user)
    {
        var secret = Totp.NewSecret();
        await _store.SetTotpPendingAsync(user.Id, _totpProtector.Protect(secret));
        return AuthResult.Success(new
        {
            secret,
            groupedSecret = string.Join(' ', Enumerable.Range(0, (secret.Length + 3) / 4).Select(i => secret.Substring(i * 4, Math.Min(4, secret.Length - i * 4)))),
            uri = Totp.OtpAuthUri(IssuerName, user.Email, secret)
        });
    }

    public async Task<AuthResult> TotpConfirmAsync(HttpContext ctx, AppUser user, string code)
    {
        var fresh = await _store.GetUserAsync(user.Id);
        if (fresh?.TotpSecretProtected == null) return AuthResult.Fail("Start setup again.");
        string secret;
        try { secret = _totpProtector.Unprotect(fresh.TotpSecretProtected); } catch { return AuthResult.Fail("Start setup again."); }
        var step = Totp.Validate(secret, code, 0);
        if (step == null) return AuthResult.Fail("That code doesn't match. Make sure your phone's clock is automatic and try again.", "invalid");
        await _store.EnableTotpAsync(user.Id, step.Value);
        var codes = Totp.NewBackupCodes();
        await _store.ReplaceBackupCodesAsync(user.Id, codes);
        await _store.LogAsync(user.Id, user.Email, "totp-enabled", true, Client(ctx));
        return AuthResult.Success(new { backupCodes = codes });
    }

    public async Task<AuthResult> RegenerateBackupCodesAsync(HttpContext ctx, AppUser user, string password)
    {
        if (!PasswordHasher.Verify(password ?? "", user.PasswordHash)) return AuthResult.Fail("Password is incorrect.", "invalid");
        var codes = Totp.NewBackupCodes();
        await _store.ReplaceBackupCodesAsync(user.Id, codes);
        await _store.LogAsync(user.Id, user.Email, "backup-codes-regenerated", true, Client(ctx));
        return AuthResult.Success(new { backupCodes = codes });
    }

    public async Task<AuthResult> TotpDisableAsync(HttpContext ctx, AppUser user, string password)
    {
        if (!PasswordHasher.Verify(password ?? "", user.PasswordHash)) return AuthResult.Fail("Password is incorrect.", "invalid");
        var policy = await _store.GetPolicyAsync();
        var keys = await _store.GetPasskeysAsync(user.Id);
        if (policy.TwoFactorRequired && keys.Count == 0) return AuthResult.Fail("Your organisation requires two-step verification. Add a passkey first.", "policy");
        await _store.DisableTotpAsync(user.Id);
        await _store.LogAsync(user.Id, user.Email, "totp-disabled", true, Client(ctx));
        return AuthResult.Success();
    }

    // ================================================== password change
    public async Task<AuthResult> ChangePasswordAsync(HttpContext ctx, AppUser user, string current, string next)
    {
        if (!PasswordHasher.Verify(current ?? "", user.PasswordHash)) return AuthResult.Fail("Current password is incorrect.", "invalid");
        var policy = await _store.GetPolicyAsync();
        var err = PasswordHasher.Validate(next, policy.PasswordComplexity);
        if (err != null) return AuthResult.Fail(err, "weak");
        foreach (var old in await _store.RecentPasswordHashesAsync(user.Id, policy.PasswordHistoryCount))
            if (PasswordHasher.Verify(next, old)) return AuthResult.Fail($"Choose a password you haven't used in your last {policy.PasswordHistoryCount}.", "reused");
        await _store.SetPasswordAsync(user.Id, PasswordHasher.Hash(next), Math.Max(policy.PasswordHistoryCount, 1));
        await _store.RevokeOtherSessionsAsync(user.Id, SessionId(ctx.User));
        await _store.LogAsync(user.Id, user.Email, "password-changed", true, Client(ctx));
        return AuthResult.Success();
    }

    // ================================================== registration
    public async Task<AuthResult> RegisterAsync(HttpContext ctx, string first, string? middle, string last, string? userName, string email, string? phone, string password)
    {
        var count = await _store.CountUsersAsync();
        bool allowed = count == 0 || _cfg.GetValue<bool>("Security:AllowSelfRegistration");
        if (!allowed) return AuthResult.Fail("Registration is closed. Ask an administrator to create your account.", "closed");
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(email))
            return AuthResult.Fail("Please fill all required fields.");
        var policy = await _store.GetPolicyAsync();
        var err = PasswordHasher.Validate(password, policy.PasswordComplexity);
        if (err != null) return AuthResult.Fail(err, "weak");
        if (await _store.EmailExistsAsync(email)) return AuthResult.Fail("An account with this email already exists.", "exists");
        var role = count == 0 ? "Admin" : "Staff";
        await _store.CreateUserAsync(first.Trim(), middle, last.Trim(), string.IsNullOrWhiteSpace(userName) ? email : userName!, email, phone, PasswordHasher.Hash(password), role);
        await _store.LogAsync(null, email, "registered", true, Client(ctx), role);
        return AuthResult.Success(new { role });
    }

    // ==================================================== sign-in finish
    private async Task<AuthResult> FinishSignInAsync(HttpContext ctx, AppUser user, string amr, bool remember, bool trustDevice, SecurityPolicy policy)
    {
        var ci = Client(ctx);

        // device recognition
        var token = DeviceToken(ctx);
        if (string.IsNullOrEmpty(token) || token.Length < 20)
        {
            token = WebAuthn.B64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            ctx.Response.Cookies.Append(AuthDefaults.DeviceCookie, token, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Path = "/"
            });
        }
        var deviceId = await _store.UpsertDeviceAsync(user.Id, SecurityStore.Sha256Hex(token), ci, trustDevice, AuthDefaults.TrustDays);
        var sid = await _store.CreateSessionAsync(user.Id, deviceId, ci, policy.SessionTimeoutMinutes);

        bool hasFactor = user.TotpEnabled || (await _store.GetPasskeysAsync(user.Id)).Count > 0;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AuthDefaults.SessionClaim, sid.ToString()),
            new(AuthDefaults.AmrClaim, amr),
        };
        bool expired = policy.PasswordExpiryDays > 0 && user.PasswordChangedUtc.HasValue
                       && user.PasswordChangedUtc.Value.AddDays(policy.PasswordExpiryDays) < DateTime.UtcNow;
        if (expired) claims.Add(new Claim(AuthDefaults.PasswordExpiredClaim, "1"));
        bool mustEnroll = policy.TwoFactorRequired && !hasFactor;
        if (mustEnroll) claims.Add(new Claim(AuthDefaults.EnrollClaim, "1"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthDefaults.Scheme));
        await ctx.SignInAsync(AuthDefaults.Scheme, principal, new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(14) : null,
        });

        await _store.RegisterSuccessAsync(user.Id);
        await _store.LogAsync(user.Id, user.Email, "sign-in", true, ci, amr);
        return AuthResult.Success(new
        {
            user = new { id = user.Id, name = user.FullName, email = user.Email, role = user.Role },
            passwordExpired = expired,
            mustEnrollMfa = mustEnroll
        });
    }

    public async Task SignOutAsync(HttpContext ctx)
    {
        var uid = UserId(ctx.User);
        var sid = SessionId(ctx.User);
        if (uid.HasValue && sid != Guid.Empty)
        {
            await _store.RevokeSessionAsync(uid.Value, sid);
            await _store.LogAsync(uid, ctx.User.FindFirstValue(ClaimTypes.Email), "sign-out", true, Client(ctx));
        }
        await ctx.SignOutAsync(AuthDefaults.Scheme);
    }

    // ====================================================== session check
    /// <summary>Used by cookie validation and the Blazor circuit revalidation. Cached briefly to spare the database.</summary>
    public async Task<bool> IsSessionValidAsync(ClaimsPrincipal principal)
    {
        var uid = UserId(principal);
        var sid = SessionId(principal);
        if (uid == null || sid == Guid.Empty) return false;
        var key = "sess:" + sid;
        if (_cache.TryGetValue(key, out bool cached)) return cached;
        var policy = await _store.GetPolicyAsync();
        var ok = await _store.ValidateAndTouchSessionAsync(sid, uid.Value, policy.SessionTimeoutMinutes);
        _cache.Set(key, ok, TimeSpan.FromSeconds(ok ? 20 : 5));
        return ok;
    }

    public void ForgetSession(Guid sid) => _cache.Remove("sess:" + sid);
}
