using System.Text.Json;

namespace SaccoManagementSystem.Services;

/// <summary>
/// Makes the system installable as an app (Progressive Web App) and ready to wrap for Google Play as a
/// Trusted Web Activity. Everything here is anonymous and read-only.
///   /manifest.webmanifest          app name, colours, icons, shortcuts (built from organisation settings)
///   /.well-known/assetlinks.json   proves to Android that the Play Store app and this website belong together
/// Configure in appsettings.json:
///   "Pwa": { "AndroidPackage": "ke.co.tajirisacco.app", "Sha256Fingerprints": [ "AB:CD:..." ] }
/// </summary>
public static class Pwa
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    public static string ShortName()
    {
        var name = Sacco_Management_System.Shared.Brand.Name;
        return name.Length > 12 ? name.Split(' ')[0] : name;
    }

    public static void MapPwa(this WebApplication app)
    {
        app.MapGet("/manifest.webmanifest", (HttpContext ctx, ConfigStore cfg) =>
        {
            var o = cfg.Current;
            var name = Sacco_Management_System.Shared.Brand.Name;
            var manifest = new
            {
                id = "/",
                name,
                short_name = ShortName(),
                description = $"{name}: savings, loans, shares, dividends and chamas in your pocket.",
                start_url = "/login?source=app",
                scope = "/",
                display = "standalone",
                display_override = new[] { "window-controls-overlay", "standalone" },
                orientation = "any",
                background_color = "#FFFFFF",
                theme_color = string.IsNullOrWhiteSpace(o.BrandColor) ? "#0B7A52" : o.BrandColor,
                lang = "en-KE",
                categories = new[] { "finance", "business", "productivity" },
                icons = new object[]
                {
                    new { src = "img/app/icon-192.png", sizes = "192x192", type = "image/png", purpose = "any" },
                    new { src = "img/app/icon-512.png", sizes = "512x512", type = "image/png", purpose = "any" },
                    new { src = "img/app/icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
                },
                shortcuts = new object[]
                {
                    new { name = "My money", short_name = "My money", url = "/my", icons = new[] { new { src = "img/app/icon-192.png", sizes = "192x192" } } },
                    new { name = "My statement", short_name = "Statement", url = "/my/statement", icons = new[] { new { src = "img/app/icon-192.png", sizes = "192x192" } } },
                    new { name = "Staff dashboard", short_name = "Dashboard", url = "/dashboard", icons = new[] { new { src = "img/app/icon-192.png", sizes = "192x192" } } },
                },
            };
            ctx.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.Text(JsonSerializer.Serialize(manifest, Json), "application/manifest+json");
        }).AllowAnonymous();

        app.MapGet("/.well-known/assetlinks.json", (HttpContext ctx, IConfiguration cfg) =>
        {
            var pkg = cfg["Pwa:AndroidPackage"];
            var prints = cfg.GetSection("Pwa:Sha256Fingerprints").Get<string[]>() ?? Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(pkg) || prints.Length == 0) return Results.Text("[]", "application/json");
            var body = new[]
            {
                new
                {
                    relation = new[] { "delegate_permission/common.handle_all_urls" },
                    target = new { @namespace = "android_app", package_name = pkg, sha256_cert_fingerprints = prints },
                },
            };
            ctx.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.Text(JsonSerializer.Serialize(body), "application/json");
        }).AllowAnonymous();
    }
}
