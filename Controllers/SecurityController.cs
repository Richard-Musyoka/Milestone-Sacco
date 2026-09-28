using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Controllers
{
    /// <summary>Signed-in user's own security settings (Security Center).</summary>
    [ApiController]
    [Route("api/security")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly SecurityStore _store;

        public SecurityController(AuthService auth, SecurityStore store)
        {
            _auth = auth;
            _store = store;
        }

        public sealed record PasswordDto(string Current, string Next);
        public sealed record CodeDto(string Code);
        public sealed record PasswordOnlyDto(string Password);
        public sealed record ToggleDto(bool Enabled);
        public sealed record ProfileDto(string? FirstName, string? LastName, string? Phone);

        private async Task<AppUser?> Me() => AuthService.UserId(User) is int id ? await _store.GetUserAsync(id) : null;

        private IActionResult Respond(AuthResult r)
            => r.Ok ? Ok(new { success = true, data = r.Data })
                    : (r.Status == "invalid" ? StatusCode(403, new { success = false, message = r.Message, status = r.Status })
                                             : BadRequest(new { success = false, message = r.Message, status = r.Status }));

        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            var policy = await _store.GetPolicyAsync();
            var sid = AuthService.SessionId(User);
            var token = Request.Cookies[AuthDefaults.DeviceCookie];
            var devHash = string.IsNullOrEmpty(token) ? null : SecurityStore.Sha256Hex(token);

            var passkeys = await _store.ListPasskeysAsync(u.Id);
            var devices = await _store.ListDevicesAsync(u.Id, devHash);
            var sessions = await _store.ListSessionsAsync(u.Id, sid);
            var history = await _store.HistoryAsync(u.Id, 25);

            int score = 40;
            if (u.TotpEnabled) score += 30;
            if (passkeys.Count > 0) score += 20;
            if (u.PasswordChangedUtc.HasValue && u.PasswordChangedUtc.Value > DateTime.UtcNow.AddDays(-policy.PasswordExpiryDays)) score += 10;

            return Ok(new
            {
                user = new { u.Id, u.FirstName, u.LastName, u.Email, u.Phone, u.Role, emailMasked = Mask.Email(u.Email), phoneMasked = Mask.Phone(u.Phone) },
                totpEnabled = u.TotpEnabled,
                otpFallbackEnabled = u.OtpFallbackEnabled,
                backupCodesLeft = u.TotpEnabled ? await _store.BackupCodesLeftAsync(u.Id) : 0,
                passwordChangedUtc = u.PasswordChangedUtc,
                score = Math.Min(score, 100),
                policy = new { policy.TwoFactorRequired, policy.SessionTimeoutMinutes, policy.PasswordComplexity, policy.PasswordExpiryDays, policy.EmailEnabled, policy.SmsEnabled },
                passkeys,
                devices,
                sessions,
                history
            });
        }

        [HttpPost("profile")]
        public async Task<IActionResult> Profile([FromBody] ProfileDto dto)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            await _store.UpdateProfileAsync(u.Id, dto.FirstName?.Trim(), dto.LastName?.Trim(), dto.Phone?.Trim());
            return Ok(new { success = true });
        }

        [HttpPost("password")]
        public async Task<IActionResult> Password([FromBody] PasswordDto dto)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            return Respond(await _auth.ChangePasswordAsync(HttpContext, u, dto.Current, dto.Next));
        }

        // ---- authenticator app
        [HttpPost("totp/begin")]
        public async Task<IActionResult> TotpBegin()
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.TotpBeginAsync(u));
        }

        [HttpPost("totp/confirm")]
        public async Task<IActionResult> TotpConfirm([FromBody] CodeDto dto)
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.TotpConfirmAsync(HttpContext, u, dto.Code));
        }

        [HttpPost("totp/disable")]
        public async Task<IActionResult> TotpDisable([FromBody] PasswordOnlyDto dto)
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.TotpDisableAsync(HttpContext, u, dto.Password));
        }

        [HttpPost("backup-codes/regenerate")]
        public async Task<IActionResult> Regenerate([FromBody] PasswordOnlyDto dto)
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.RegenerateBackupCodesAsync(HttpContext, u, dto.Password));
        }

        // ---- passkeys
        [HttpPost("passkeys/options")]
        public async Task<IActionResult> PasskeyOptions()
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.PasskeyRegisterOptionsAsync(HttpContext, u));
        }

        [HttpPost("passkeys/verify")]
        public async Task<IActionResult> PasskeyVerify([FromBody] AuthService.AttestationDto dto)
        {
            var u = await Me();
            return u == null ? Unauthorized() : Respond(await _auth.PasskeyRegisterVerifyAsync(HttpContext, u, dto));
        }

        [HttpDelete("passkeys/{id:int}")]
        public async Task<IActionResult> PasskeyRemove(int id)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            var policy = await _store.GetPolicyAsync();
            var keys = await _store.GetPasskeysAsync(u.Id);
            if (policy.TwoFactorRequired && !u.TotpEnabled && keys.Count <= 1)
                return BadRequest(new { success = false, message = "Your organisation requires two-step verification. Set up an authenticator app first." });
            await _store.RemovePasskeyAsync(u.Id, id);
            await _store.LogAsync(u.Id, u.Email, "passkey-removed", true, AuthService.Client(HttpContext));
            return Ok(new { success = true });
        }

        // ---- fallback
        [HttpPost("otp-fallback")]
        public async Task<IActionResult> OtpFallback([FromBody] ToggleDto dto)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            if (dto.Enabled && string.IsNullOrWhiteSpace(u.Phone) && string.IsNullOrWhiteSpace(u.Email))
                return BadRequest(new { success = false, message = "Add a phone number or email first." });
            await _store.SetOtpFallbackAsync(u.Id, dto.Enabled);
            return Ok(new { success = true });
        }

        // ---- devices & sessions
        [HttpDelete("devices/{id:int}")]
        public async Task<IActionResult> DeviceRemove(int id)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            await _store.RemoveDeviceAsync(u.Id, id);
            return Ok(new { success = true });
        }

        [HttpPost("devices/untrust-all")]
        public async Task<IActionResult> UntrustAll()
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            await _store.UntrustAllDevicesAsync(u.Id);
            return Ok(new { success = true });
        }

        [HttpDelete("sessions/{id:guid}")]
        public async Task<IActionResult> SessionRevoke(Guid id)
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            await _store.RevokeSessionAsync(u.Id, id);
            _auth.ForgetSession(id);
            return Ok(new { success = true, self = id == AuthService.SessionId(User) });
        }

        [HttpPost("sessions/revoke-others")]
        public async Task<IActionResult> RevokeOthers()
        {
            var u = await Me();
            if (u == null) return Unauthorized();
            var keep = AuthService.SessionId(User);
            foreach (var s in await _store.ListSessionsAsync(u.Id, keep)) if (!s.IsCurrent) _auth.ForgetSession(s.Id);
            await _store.RevokeOtherSessionsAsync(u.Id, keep);
            return Ok(new { success = true });
        }
    }
}
