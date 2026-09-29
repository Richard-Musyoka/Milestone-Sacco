using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IDbFactory _db;
        public AuditController(IDbFactory db) => _db = db;

        public sealed record AuditRow(string Source, long Id, DateTime When, string User, string Entity, string Action, string? RecordId, string? Detail, bool Success, string? Ip);

        /// <summary>Business changes (AuditLog) and sign-in/security events (LoginHistory) in one timeline.</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? source, [FromQuery] string? entity,
            [FromQuery] string? user, [FromQuery] string? status, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int size = 25)
        {
            page = Math.Max(1, page); size = Math.Clamp(size, 5, 200);
            await using var c = await _db.OpenAsync();

            const string union = @"
                SELECT 'Activity' AS Source, Id, CreatedUtc, ISNULL(UserName, 'System') AS UserName, ISNULL(Entity,'') AS Entity, ISNULL(Action,'') AS Action, RecordId,
                       Method + ' ' + Path AS Detail, CASE WHEN StatusCode < 400 THEN 1 ELSE 0 END AS Success, Ip
                FROM AuditLog
                UNION ALL
                SELECT 'Security', Id, CreatedUtc, ISNULL(Email, 'Unknown'), 'auth', EventType, NULL, Detail, Success, Ip
                FROM LoginHistory";

            var where = new List<string>(); var p = new List<(string, object?)>();
            if (from.HasValue) { where.Add("CreatedUtc >= @from"); p.Add(("from", from.Value.Date.ToUniversalTime())); }
            if (to.HasValue) { where.Add("CreatedUtc < @to"); p.Add(("to", to.Value.Date.AddDays(1).ToUniversalTime())); }
            if (!string.IsNullOrEmpty(source)) { where.Add("Source = @source"); p.Add(("source", source)); }
            if (!string.IsNullOrEmpty(entity)) { where.Add("Entity = @entity"); p.Add(("entity", entity)); }
            if (!string.IsNullOrEmpty(user)) { where.Add("UserName = @user"); p.Add(("user", user)); }
            if (status == "ok") where.Add("Success = 1"); else if (status == "failed") where.Add("Success = 0");
            if (!string.IsNullOrWhiteSpace(q))
            {
                where.Add("(UserName LIKE @q OR Action LIKE @q OR Entity LIKE @q OR RecordId LIKE @q OR Detail LIKE @q OR Ip LIKE @q)");
                p.Add(("q", "%" + q.Trim() + "%"));
            }
            var w = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            var args = p.ToArray();

            var total = await c.ScalarAsync<int>($"SELECT COUNT(*) FROM ({union}) t {w}", args);
            var rows = await c.QueryAsync($"SELECT * FROM ({union}) t {w} ORDER BY CreatedUtc DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
                r => new AuditRow(r.Str("Source"), r.Long("Id"), r.Date("CreatedUtc"), r.Str("UserName"), r.Str("Entity"), r.Str("Action"), r.StrN("RecordId"), r.StrN("Detail"), r.Bool("Success"), r.StrN("Ip")),
                args.Concat(new (string, object?)[] { ("skip", (page - 1) * size), ("take", size) }).ToArray());

            var since = DateTime.UtcNow.AddHours(-24);
            var stats = new
            {
                Last24h = await c.ScalarAsync<int>($"SELECT COUNT(*) FROM ({union}) t WHERE CreatedUtc >= @s", ("s", since)),
                Failed24h = await c.ScalarAsync<int>($"SELECT COUNT(*) FROM ({union}) t WHERE CreatedUtc >= @s AND Success = 0", ("s", since)),
                Users24h = await c.ScalarAsync<int>($"SELECT COUNT(DISTINCT UserName) FROM ({union}) t WHERE CreatedUtc >= @s", ("s", since)),
                Changes24h = await c.ScalarAsync<int>("SELECT COUNT(*) FROM AuditLog WHERE CreatedUtc >= @s", ("s", since)),
            };
            var entities = await c.QueryAsync("SELECT DISTINCT Entity FROM AuditLog WHERE Entity IS NOT NULL ORDER BY Entity", r => r.Str("Entity"));
            var users = await c.QueryAsync($"SELECT DISTINCT UserName FROM ({union}) t WHERE UserName IS NOT NULL ORDER BY UserName", r => r.Str("UserName"));

            return Ok(new { total, page, size, items = rows, stats, entities, users });
        }
    }
}
