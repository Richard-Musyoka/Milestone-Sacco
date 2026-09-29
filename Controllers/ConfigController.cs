using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Services;

namespace SaccoManagementSystem.Controllers;

[ApiController]
[Route("api")]
public class ConfigController : ControllerBase
{
    private readonly ConfigStore _store;
    public ConfigController(ConfigStore store) => _store = store;

    /// <summary>Organisation configuration (branding, regional format, appearance defaults, sign-in options). Any signed-in user may read it.</summary>
    [HttpGet("config")]
    public IActionResult Get() => Ok(_store.Current);

    /// <summary>Administrators only (enforced by the API guard).</summary>
    [HttpPut("config")]
    public async Task<IActionResult> Put([FromBody] OrgConfig cfg)
    {
        var (saved, error) = await _store.SaveAsync(cfg, User.Identity?.Name);
        if (error != null) return BadRequest(new { success = false, message = error });
        return Ok(saved);
    }

    /// <summary>Public bits the sign-in page needs (no secrets).</summary>
    [HttpGet("auth/branding")]
    public IActionResult Branding()
    {
        var c = _store.Current;
        return Ok(new { name = Sacco_Management_System.Shared.Brand.Name, tagline = Sacco_Management_System.Shared.Brand.Tagline, logo = c.LogoDataUrl, color = c.BrandColor });
    }
}
