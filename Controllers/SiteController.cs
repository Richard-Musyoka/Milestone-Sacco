using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Services;

namespace SaccoManagementSystem.Controllers;

/// <summary>Website content and enquiries. Managers and administrators only (enforced by the API guard).</summary>
[ApiController]
[Route("api/site")]
public class SiteController : ControllerBase
{
    private readonly SiteStore _site;
    public SiteController(SiteStore site) => _site = site;

    [HttpGet]
    public IActionResult Get() => Ok(_site.Current);

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SiteContent content)
    {
        var (saved, error) = await _site.SaveAsync(content, User.Identity?.Name);
        if (error != null) return BadRequest(new { success = false, message = error });
        return Ok(saved);
    }

    [HttpGet("enquiries")]
    public async Task<IActionResult> Enquiries([FromQuery] string? status) => Ok(await _site.EnquiriesAsync(status));

    public sealed record StatusBody(string Status);

    [HttpPost("enquiries/{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] StatusBody body)
        => await _site.SetEnquiryStatusAsync(id, body.Status, User.Identity?.Name) ? Ok(new { success = true }) : BadRequest(new { success = false, message = "Unknown enquiry or status." });
}
