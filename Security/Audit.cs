using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace SaccoManagementSystem.Security;

/// <summary>
/// Lets code running outside the Blazor circuit's DI scope (such as the HttpClient handler that calls our own API)
/// find out who is signed in. Pattern from Microsoft's "access Blazor services from a different DI scope" guidance.
/// </summary>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> Current = new();
    public IServiceProvider? Services { get => Current.Value; set => Current.Value = value; }
}

public sealed class ServicesAccessorCircuitHandler : CircuitHandler
{
    private readonly IServiceProvider _services;
    private readonly CircuitServicesAccessor _accessor;
    public ServicesAccessorCircuitHandler(IServiceProvider services, CircuitServicesAccessor accessor) { _services = services; _accessor = accessor; }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            _accessor.Services = _services;
            try { await next(context); }
            finally { _accessor.Services = null; }
        };
}

public static class ActingUser
{
    public const string IdHeader = "X-Acting-User";
    public const string NameHeader = "X-Acting-Name";

    /// <summary>Best-effort: who is signed in on the Blazor circuit making this call. Never throws.</summary>
    public static async Task<(int? Id, string? Name)> FromCircuitAsync(CircuitServicesAccessor accessor)
    {
        try
        {
            var provider = accessor.Services?.GetService<AuthenticationStateProvider>();
            if (provider == null) return (null, null);
            var user = (await provider.GetAuthenticationStateAsync()).User;
            if (user.Identity?.IsAuthenticated != true) return (null, null);
            return (AuthService.UserId(user), user.FindFirstValue(ClaimTypes.Name));
        }
        catch { return (null, null); }
    }

    public static string Encode(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    public static string? Decode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return null; }
    }
}

/// <summary>Records every state-changing API call (who, what, where, outcome) — never request bodies.</summary>
public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDbFactory _db;
    private readonly ILogger<AuditMiddleware> _log;

    public AuditMiddleware(RequestDelegate next, IDbFactory db, ILogger<AuditMiddleware> log) { _next = next; _db = db; _log = log; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path;
        bool mutating = path.StartsWithSegments("/api")
            && !path.StartsWithSegments("/api/auth")       // sign-in events are already in LoginHistory
            && !path.StartsWithSegments("/api/audit")
            && (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method) || HttpMethods.IsDelete(ctx.Request.Method) || HttpMethods.IsPatch(ctx.Request.Method));

        await _next(ctx);
        if (!mutating) return;

        try
        {
            var segs = path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray(); // drop "api"
            var entity = segs.Length > 0 ? segs[0] : "api";
            string? record = segs.Skip(1).FirstOrDefault(s => s.All(char.IsDigit) || Guid.TryParse(s, out _));
            string? verb = segs.Skip(1).LastOrDefault(s => !(s.All(char.IsDigit) || Guid.TryParse(s, out _)));
            var action = (verb ?? ctx.Request.Method switch { "POST" => "create", "PUT" => "update", "DELETE" => "delete", _ => "change" }).ToLowerInvariant();

            var user = ctx.User;
            int? uid = user.Identity?.IsAuthenticated == true ? AuthService.UserId(user) : null;
            var name = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.Name) : null;

            await using var c = await _db.OpenAsync();
            await c.ExecAsync(@"INSERT INTO AuditLog (UserId, UserName, Method, Path, Entity, Action, RecordId, StatusCode, Ip)
                                VALUES (@u, @n, @m, @p, @e, @a, @r, @s, @ip)",
                ("u", uid), ("n", name), ("m", ctx.Request.Method), ("p", Trim(path.Value, 300)), ("e", Trim(entity, 60)), ("a", Trim(action, 80)),
                ("r", Trim(record, 60)), ("s", ctx.Response.StatusCode), ("ip", ctx.Connection.RemoteIpAddress?.ToString()));
        }
        catch (Exception ex) { _log.LogWarning(ex, "Audit write failed"); } // auditing must never break the request
    }

    private static string? Trim(string? s, int n) => s == null ? null : (s.Length <= n ? s : s[..n]);
}
