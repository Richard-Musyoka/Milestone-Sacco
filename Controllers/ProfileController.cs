using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Security;
using SaccoManagementSystem.Services;

namespace SaccoManagementSystem.Controllers;

/// <summary>The signed-in person's own extended profile.</summary>
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly ProfileStore _store;
    public ProfileController(ProfileStore store) => _store = store;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = AuthService.UserId(User);
        if (id == null) return Unauthorized();
        return Ok(await _store.GetAsync(id.Value) ?? new UserProfile { UserId = id.Value });
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] UserProfile p)
    {
        var id = AuthService.UserId(User);
        if (id == null) return Unauthorized();
        p.UserId = id.Value;                                  // never trust the body for whose profile this is
        var err = await _store.SaveAsync(p);
        return err == null ? Ok(new { success = true }) : BadRequest(new { success = false, message = err });
    }
}
