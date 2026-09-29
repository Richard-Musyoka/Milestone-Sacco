using Microsoft.Extensions.Caching.Memory;

namespace SaccoManagementSystem.Security;

/// <summary>
/// Role model. Admin: everything. Manager: everything except user administration and organisation settings.
/// Teller: day-to-day records (members, contributions, shares, guarantors, loan applications), no approvals.
/// Viewer: read-only. Legacy "Staff" accounts behave as Teller.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin", Manager = "Manager", Teller = "Teller", Viewer = "Viewer", Member = "Member";
    /// <summary>Staff roles that can be assigned in Users &amp; roles. Members get their role by signing in with their member number.</summary>
    public static readonly string[] All = { Admin, Manager, Teller, Viewer };

    public static string Normalise(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Viewer;
        if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase)) return Teller;
        if (role.Equals(Member, StringComparison.OrdinalIgnoreCase)) return Member;
        return All.FirstOrDefault(r => r.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Viewer;
    }

    public static string Describe(string role) => role switch
    {
        Admin => "Full access, including users, security and organisation settings.",
        Manager => "Approvals, dividends, reports and audit trail. No user administration.",
        Teller => "Records members, contributions, shares, guarantors and loan applications.",
        Member => "A SACCO member. Sees only their own savings, loans, shares, dividends and chamas.",
        _ => "Read-only access to records and reports."
    };
}

public static class Access
{
    private static readonly string[] Approvers = { Roles.Admin, Roles.Manager };
    private static readonly string[] Writers = { Roles.Admin, Roles.Manager, Roles.Teller };
    private static readonly string[] Approvals = { "approve", "reject", "disburse", "mark-paid", "process", "fail", "reverse", "payout" };
    private static readonly string[] WriteWords = { "add", "add-member", "edit", "apply", "purchase", "declare", "process", "transfer", "record" };

    private static string[] Segs(string path) =>
        path.Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant()).ToArray();

    /// <summary>Whether a role may open a Blazor page. Pass a path like "loans/apply" (with or without a leading slash).</summary>
    public static bool CanViewPage(string? role, string path)
    {
        role = Roles.Normalise(role);
        var s = Segs(path);
        if (role == Roles.Member)                                   // members live in their own portal
            return s.Length == 0 || s[0] is "my" or "profile" or "portal";
        if (s.Length == 0) return true;
        if (s[0] == "my") return false;                             // staff don't use the member portal
        switch (s[0])
        {
            case "users": return role == Roles.Admin;
            case "audit-trail": return Approvers.Contains(role);
            case "website": return Approvers.Contains(role);
            case "reports": return role != Roles.Teller;
            case "dividends" when s.Length > 1 && (s.Contains("declare") || s.Contains("process") || s.Contains("edit")): return Approvers.Contains(role);
        }
        if (s.Skip(1).Any(x => WriteWords.Contains(x))) return Writers.Contains(role);
        return true;
    }

    /// <summary>Whether a role may call an API endpoint with this method. Returns null when allowed, otherwise a reason.</summary>
    public static string? DenyApi(string? role, string method, string path)
    {
        role = Roles.Normalise(role);
        var s = Segs(path);
        if (s.Length < 2) return null;                      // "api" only
        var area = s[1];
        bool read = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        if (area is "auth" or "security" or "profile") return null;   // sign-in and the caller's own security settings / profile
        if (role == Roles.Member) return "Members can only see their own records, in the member portal.";
        if (area == "site") return Approvers.Contains(role) ? null : "The website and its enquiries are managed by managers and administrators.";
        if (area == "users") return role == Roles.Admin ? null : "Only administrators can manage users.";
        if (area == "audit") return Approvers.Contains(role) ? null : "The audit trail is limited to managers and administrators.";
        if (read) return null;
        if (area is "settings" or "config") return role == Roles.Admin ? null : "Only administrators can change organisation settings.";
        if (area == "dividends" || s.Skip(2).Any(x => Approvals.Contains(x)))
            return Approvers.Contains(role) ? null : "This action needs a manager or administrator.";
        return Writers.Contains(role) ? null : "Your role is read-only.";
    }
}

/// <summary>Looks up the current role/active flag of a user (cached briefly) so role changes take effect within seconds.</summary>
public sealed class RoleLookup
{
    private readonly IDbFactory _db;
    private readonly IMemoryCache _cache;
    public RoleLookup(IDbFactory db, IMemoryCache cache) { _db = db; _cache = cache; }

    public sealed record Info(string Role, bool Active);

    public async Task<Info?> GetAsync(int userId)
    {
        if (_cache.TryGetValue("role:" + userId, out Info? cached)) return cached;
        await using var c = await _db.OpenAsync();
        var info = await c.FirstAsync("SELECT Role, IsActive FROM Users WHERE Id = @id",
            r => new Info(Roles.Normalise(r.Str("Role")), r.Bool("IsActive")), ("id", userId));
        _cache.Set("role:" + userId, info, TimeSpan.FromSeconds(20));
        return info;
    }

    public void Forget(int userId) => _cache.Remove("role:" + userId);
}
