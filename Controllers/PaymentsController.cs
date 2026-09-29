using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Security;
using SaccoManagementSystem.Services;

namespace SaccoManagementSystem.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentStore _store;
    private readonly IDbFactory _db;
    public PaymentsController(PaymentStore store, IDbFactory db) { _store = store; _db = db; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? channel, [FromQuery] string? purpose, [FromQuery] int? memberId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _store.ListAsync(q, channel, purpose, memberId, from, to));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary() => Ok(await _store.SummaryAsync());

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] PaymentInput input)
    {
        decimal price = 100;
        try { await using var c = await _db.OpenAsync(); price = await c.ScalarAsync<decimal?>("SELECT TOP 1 SharePrice FROM SaccoSettings ORDER BY Id DESC") ?? 100; } catch { }
        var (saved, error) = await _store.RecordAsync(input, User.Identity?.Name, price);
        return error != null ? BadRequest(new { success = false, message = error }) : Ok(saved);
    }

    public sealed record ReverseBody(string Reason);

    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, [FromBody] ReverseBody body)
    {
        var err = await _store.ReverseAsync(id, body.Reason, User.Identity?.Name);
        return err != null ? BadRequest(new { success = false, message = err }) : Ok(new { success = true });
    }
}
