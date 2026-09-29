using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Controllers
{
    /// <summary>Anonymous sign-in endpoints. The browser calls these directly (fetch) so the auth cookie lands in the browser.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly SecurityStore _store;
        private readonly IConfiguration _cfg;
        private readonly SaccoManagementSystem.Services.ConfigStore _conf;

        public AuthController(AuthService auth, SecurityStore store, IConfiguration cfg, SaccoManagementSystem.Services.ConfigStore conf)
        {
            _auth = auth;
            _store = store;
            _cfg = cfg;
            _conf = conf;
        }

        public sealed record LoginDto([Required] string Email, [Required] string Password, bool RememberMe);
        public sealed record TokenCodeDto(string Token, string Code, bool TrustDevice);
        public sealed record OtpSendDto(string Token, string Channel);
        public sealed record OtpVerifyDto(string Token, Guid OtpId, string Code, bool TrustDevice);
        public sealed record PasskeyOptionsDto(string? MfaToken);
        public sealed record ForgotDto(string Email);
        public sealed record MemberStartDto(string Identifier, bool RememberMe);
        public sealed record ResetDto(string Email, string Code, string NewPassword);
        public sealed record RegisterDto(string FirstName, string? MiddleName, string LastName, string? UserName, string Email, string? PhoneNumber, string Password);

        private IActionResult Respond(AuthResult r)
        {
            if (r.Ok) return Ok(new { success = true, status = r.Status, message = r.Message, data = r.Data });
            var body = new { success = false, status = r.Status, message = r.Message };
            return r.Status switch
            {
                "invalid" => Unauthorized(body),
                "expired" => Unauthorized(body),
                "locked" => StatusCode(423, body),
                "disabled" => StatusCode(403, body),
                "closed" => StatusCode(403, body),
                "exists" => Conflict(body),
                "throttled" => StatusCode(429, body),
                _ => BadRequest(body),
            };
        }

        [HttpGet("config")]
        public async Task<IActionResult> Config()
        {
            var first = await _store.CountUsersAsync() == 0;
            var open = first;   // after the first administrator, accounts are created in Users & roles
            var policy = await _store.GetPolicyAsync();
            return Ok(new { allowRegistration = open, firstUser = first, passwordComplexity = policy.PasswordComplexity, brand = Sacco_Management_System.Shared.Brand.Name });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto) => Respond(await _auth.LoginAsync(HttpContext, dto.Email, dto.Password, dto.RememberMe));

        /// <summary>Member portal: member number / ID / phone, then a one-time code through mfa/otp/send + mfa/otp/verify.</summary>
        [HttpPost("member/start")]
        public async Task<IActionResult> MemberStart([FromBody] MemberStartDto dto) => Respond(await _auth.MemberStartAsync(HttpContext, dto.Identifier, dto.RememberMe));

        [HttpPost("mfa/totp")]
        public async Task<IActionResult> Totp([FromBody] TokenCodeDto dto) => Respond(await _auth.VerifyTotpAsync(HttpContext, dto.Token, dto.Code, dto.TrustDevice));

        [HttpPost("mfa/backup")]
        public async Task<IActionResult> Backup([FromBody] TokenCodeDto dto) => Respond(await _auth.VerifyBackupAsync(HttpContext, dto.Token, dto.Code, dto.TrustDevice));

        [HttpPost("mfa/otp/send")]
        public async Task<IActionResult> OtpSend([FromBody] OtpSendDto dto) => Respond(await _auth.SendOtpAsync(HttpContext, dto.Token, dto.Channel));

        [HttpPost("mfa/otp/verify")]
        public async Task<IActionResult> OtpVerify([FromBody] OtpVerifyDto dto) => Respond(await _auth.VerifyOtpAsync(HttpContext, dto.Token, dto.OtpId, dto.Code, dto.TrustDevice));

        [HttpPost("passkey/options")]
        public async Task<IActionResult> PasskeyOptions([FromBody] PasskeyOptionsDto dto) => Respond(await _auth.PasskeyLoginOptionsAsync(HttpContext, dto?.MfaToken));

        [HttpPost("passkey/verify")]
        public async Task<IActionResult> PasskeyVerify([FromBody] AuthService.AssertionDto dto) => Respond(await _auth.PasskeyLoginVerifyAsync(HttpContext, dto));

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
            => Respond(await _auth.RegisterAsync(HttpContext, dto.FirstName, dto.MiddleName, dto.LastName, dto.UserName, dto.Email, dto.PhoneNumber, dto.Password));

        [HttpPost("forgot")]
        public async Task<IActionResult> Forgot([FromBody] ForgotDto dto) => Respond(await _auth.ForgotPasswordAsync(HttpContext, dto.Email));

        [HttpPost("reset")]
        public async Task<IActionResult> Reset([FromBody] ResetDto dto) => Respond(await _auth.ResetPasswordAsync(HttpContext, dto.Email, dto.Code, dto.NewPassword));

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _auth.SignOutAsync(HttpContext);
            return Ok(new { success = true });
        }
    }
}
