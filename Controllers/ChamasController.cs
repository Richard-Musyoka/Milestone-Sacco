using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Services;

namespace SaccoManagementSystem.Controllers;

[ApiController]
[Route("api/chamas")]
public class ChamasController : ControllerBase
{
    private readonly ChamaStore _store;
    public ChamasController(ChamaStore store) => _store = store;
    private IActionResult Result(string? err) => err == null ? Ok(new { success = true }) : BadRequest(new { success = false, message = err });

    [HttpGet] public async Task<IActionResult> List() => Ok(await _store.ListAsync());

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] Chama chama)
    {
        var (id, err) = await _store.SaveAsync(chama, User.Identity?.Name);
        return err != null ? BadRequest(new { success = false, message = err }) : Ok(new { success = true, id });
    }

    public sealed record MemberBody(int MemberId, string Role);
    [HttpPost("{id:int}/members")] public async Task<IActionResult> AddMember(int id, [FromBody] MemberBody b) => Result(await _store.AddMemberAsync(id, b.MemberId, b.Role));
    [HttpPost("{id:int}/members/{memberId:int}/remove")] public async Task<IActionResult> Remove(int id, int memberId) { var msg = await _store.RemoveMemberAsync(id, memberId); return Ok(new { success = true, message = msg }); }
    public sealed record RoleBody(string Role);
    [HttpPost("{id:int}/member-rows/{rowId:int}/role")] public async Task<IActionResult> Role(int id, int rowId, [FromBody] RoleBody b) { await _store.SetRoleAsync(rowId, b.Role); return Ok(new { success = true }); }
    [HttpPost("{id:int}/order")] public async Task<IActionResult> Order(int id, [FromBody] List<int> memberIds) { await _store.SetOrderAsync(id, memberIds); var ch = await _store.GetAsync(id); if (ch?.Rotates == true && ch.Members >= 2) return Result(await _store.GenerateScheduleAsync(id)); return Result(null); }
    [HttpPost("{id:int}/schedule")] public async Task<IActionResult> Schedule(int id) => Result(await _store.GenerateScheduleAsync(id));
    [HttpPost("{id:int}/shuffle")] public async Task<IActionResult> Shuffle(int id) => Result(await _store.ShuffleAsync(id));
    public sealed record PayoutBody(string Reference);
    [HttpPost("{id:int}/payout")] public async Task<IActionResult> Payout(int id, [FromBody] PayoutBody b) => Result(await _store.PayOutAsync(id, b.Reference, User.Identity?.Name));
    public sealed record QuickBody(int MemberId, int Cycle, decimal Amount);
    [HttpPost("{id:int}/contributions")] public async Task<IActionResult> Quick(int id, [FromBody] QuickBody b) { if (b.Amount <= 0) return Result("Enter an amount."); await _store.QuickContributionAsync(id, b.MemberId, b.Cycle, b.Amount, User.Identity?.Name); return Result(null); }
}
