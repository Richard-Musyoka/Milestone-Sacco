using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;

namespace SaccoManagementSystem.Security;

/// <summary>
/// Random per-process secret. The server-side HttpClient ("ApiClient") used by Razor pages attaches it so
/// existing pages keep working while the data APIs are closed to anonymous callers.
/// Phase 2 replaces this loopback with a service layer.
/// </summary>
public sealed class InternalApiKey
{
    public string Value { get; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public const string Header = "X-Internal-Key";
}

public sealed class InternalKeyHandler : DelegatingHandler
{
    private readonly InternalApiKey _key;
    private readonly HashSet<string> _hosts;

    public InternalKeyHandler(InternalApiKey key, IConfiguration cfg)
    {
        _key = key;
        _hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Extra hostnames that point back at this same app (e.g. behind a reverse proxy): "Security": { "InternalHosts": ["sacco.example.com"] }
        foreach (var h in cfg.GetSection("Security:InternalHosts").Get<string[]>() ?? Array.Empty<string>()) _hosts.Add(h);
        if (Uri.TryCreate(cfg["ApiBaseUrl"], UriKind.Absolute, out var api)) _hosts.Add(api.Host);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } u && (u.IsLoopback || _hosts.Contains(u.Host)))
            request.Headers.TryAddWithoutValidation(InternalApiKey.Header, _key.Value);
        return base.SendAsync(request, ct);
    }
}

/// <summary>Closes /api/** to anonymous callers and adds a CSRF check for cookie-authenticated writes.</summary>
public sealed class ApiGuardMiddleware
{
    public const string AjaxHeader = "X-Requested-With";
    public const string AjaxValue = "milestone";
    private readonly RequestDelegate _next;
    public ApiGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, InternalApiKey key)
    {
        var path = ctx.Request.Path;
        if (!path.StartsWithSegments("/api")) { await _next(ctx); return; }

        bool unsafeMethod = !(HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method));
        bool ajax = ctx.Request.Headers[AjaxHeader] == AjaxValue;

        // Server-side loopback from our own Razor pages
        if (ctx.Request.Headers.TryGetValue(InternalApiKey.Header, out var provided)
            && PasswordHasher.ConstantTimeEquals(provided.ToString(), key.Value))
        {
            await _next(ctx);
            return;
        }

        // Sign-in endpoints are anonymous, but still refuse cross-site form posts.
        if (path.StartsWithSegments("/api/auth"))
        {
            if (unsafeMethod && !ajax) { ctx.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            await _next(ctx);
            return;
        }

        var auth = await ctx.AuthenticateAsync(AuthDefaults.Scheme);
        if (!auth.Succeeded)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        ctx.User = auth.Principal!;
        if (unsafeMethod && !ajax) { ctx.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        await _next(ctx);
    }
}

/// <summary>Cookie events: reject cookies whose server-side session was revoked or idled out.</summary>
public sealed class SessionCookieEvents : CookieAuthenticationEvents
{
    private readonly AuthService _auth;
    public SessionCookieEvents(AuthService auth) => _auth = auth;

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal == null || !await _auth.IsSessionValidAsync(context.Principal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthDefaults.Scheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}

/// <summary>Blazor circuit auth state that re-checks the session every minute so revoked sessions drop out quickly.</summary>
public sealed class SessionAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopes;

    public SessionAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopes) : base(loggerFactory)
        => _scopes = scopes;

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var user = authenticationState.User;
        if (user.Identity?.IsAuthenticated != true) return true;
        return await auth.IsSessionValidAsync(user);
    }
}
