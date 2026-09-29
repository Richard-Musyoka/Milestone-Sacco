using System.Text.RegularExpressions;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

/// <summary>Extra details for staff accounts (photo, job, Kenyan identity details, next of kin). One row per user.</summary>
public sealed class UserProfile
{
    public int UserId { get; set; }
    public string Photo { get; set; } = "";              // data URL, served through /media/user/{id}
    public string StaffNo { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Department { get; set; } = "";
    public string Branch { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string KraPin { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string County { get; set; } = "";
    public string PostalAddress { get; set; } = "";
    public string AltPhone { get; set; } = "";
    public string Language { get; set; } = "English";
    public string NextOfKinName { get; set; } = "";
    public string NextOfKinRelation { get; set; } = "";
    public string NextOfKinPhone { get; set; } = "";
    public string Bio { get; set; } = "";
    public DateTime? UpdatedUtc { get; set; }

    public UserProfile Copy() => (UserProfile)MemberwiseClone();
}

public sealed class ProfileStore
{
    private static readonly Regex Img = new(@"^data:image/(png|jpeg|jpg|webp|gif);base64,[A-Za-z0-9+/=]+$", RegexOptions.Compiled);
    private static readonly Regex Pin = new("^[AP][0-9]{9}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex Id = new("^[0-9]{6,9}$", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"^\+?[0-9 ]{9,16}$", RegexOptions.Compiled);

    public static readonly string[] Counties =
    {
        "Baringo","Bomet","Bungoma","Busia","Elgeyo-Marakwet","Embu","Garissa","Homa Bay","Isiolo","Kajiado","Kakamega","Kericho","Kiambu","Kilifi",
        "Kirinyaga","Kisii","Kisumu","Kitui","Kwale","Laikipia","Lamu","Machakos","Makueni","Mandera","Marsabit","Meru","Migori","Mombasa","Murang'a",
        "Nairobi","Nakuru","Nandi","Narok","Nyamira","Nyandarua","Nyeri","Samburu","Siaya","Taita-Taveta","Tana River","Tharaka-Nithi","Trans Nzoia",
        "Turkana","Uasin Gishu","Vihiga","Wajir","West Pokot"
    };

    private readonly IDbFactory _db;
    private readonly Dictionary<int, string> _ver = new();
    public ProfileStore(IDbFactory db) => _db = db;

    public event Action<int>? Changed;

    /// <summary>"/media/user/5?v=…" or null when the user has no photo.</summary>
    public string? PhotoUrl(int userId) { lock (_ver) return _ver.TryGetValue(userId, out var v) && v != "" ? "media/user/" + userId + "?v=" + v : null; }

    public async Task EnsureSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"IF OBJECT_ID('dbo.UserProfiles') IS NULL
            CREATE TABLE dbo.UserProfiles (UserId INT NOT NULL PRIMARY KEY, Photo NVARCHAR(MAX) NULL, StaffNo NVARCHAR(30) NULL, JobTitle NVARCHAR(80) NULL,
              Department NVARCHAR(80) NULL, Branch NVARCHAR(80) NULL, NationalId NVARCHAR(20) NULL, KraPin NVARCHAR(20) NULL, DateOfBirth DATE NULL,
              Gender NVARCHAR(20) NULL, County NVARCHAR(40) NULL, PostalAddress NVARCHAR(120) NULL, AltPhone NVARCHAR(30) NULL, Language NVARCHAR(20) NULL,
              NextOfKinName NVARCHAR(120) NULL, NextOfKinRelation NVARCHAR(40) NULL, NextOfKinPhone NVARCHAR(30) NULL, Bio NVARCHAR(600) NULL,
              UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());");
        var rows = await c.QueryAsync("SELECT UserId, UpdatedUtc FROM dbo.UserProfiles WHERE Photo IS NOT NULL AND Photo <> ''", r => (r.Int("UserId"), r.Date("UpdatedUtc").Ticks));
        lock (_ver) foreach (var (id, t) in rows) _ver[id] = t.ToString();
    }

    public async Task<UserProfile?> GetAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync("SELECT * FROM dbo.UserProfiles WHERE UserId = @u", r => new UserProfile
        {
            UserId = r.Int("UserId"), Photo = r.Str("Photo"), StaffNo = r.Str("StaffNo"), JobTitle = r.Str("JobTitle"), Department = r.Str("Department"),
            Branch = r.Str("Branch"), NationalId = r.Str("NationalId"), KraPin = r.Str("KraPin"), DateOfBirth = r.DateN("DateOfBirth"), Gender = r.Str("Gender"),
            County = r.Str("County"), PostalAddress = r.Str("PostalAddress"), AltPhone = r.Str("AltPhone"), Language = r.Str("Language"),
            NextOfKinName = r.Str("NextOfKinName"), NextOfKinRelation = r.Str("NextOfKinRelation"), NextOfKinPhone = r.Str("NextOfKinPhone"),
            Bio = r.Str("Bio"), UpdatedUtc = r.DateN("UpdatedUtc")
        }, ("u", userId));
    }

    public static string? Validate(UserProfile p)
    {
        string T(string? v) => (v ?? "").Trim();
        p.StaffNo = T(p.StaffNo); p.JobTitle = T(p.JobTitle); p.Department = T(p.Department); p.Branch = T(p.Branch);
        p.NationalId = T(p.NationalId); p.KraPin = T(p.KraPin).ToUpperInvariant(); p.Gender = T(p.Gender); p.County = T(p.County);
        p.PostalAddress = T(p.PostalAddress); p.AltPhone = T(p.AltPhone); p.Language = T(p.Language); p.NextOfKinName = T(p.NextOfKinName);
        p.NextOfKinRelation = T(p.NextOfKinRelation); p.NextOfKinPhone = T(p.NextOfKinPhone); p.Bio = T(p.Bio);
        if (!string.IsNullOrEmpty(p.Photo) && (p.Photo.Length > 1_400_000 || !Img.IsMatch(p.Photo))) return "Your photo must be a PNG, JPG, WebP or GIF under 1 MB.";
        if (p.NationalId.Length > 0 && !Id.IsMatch(p.NationalId)) return "National ID should be 6 to 9 digits.";
        if (p.KraPin.Length > 0 && !Pin.IsMatch(p.KraPin)) return "KRA PIN is 11 characters, like A001234567Z.";
        if (p.AltPhone.Length > 0 && !Phone.IsMatch(p.AltPhone)) return "Alternative phone looks wrong. Use a format like +254 712 345 678.";
        if (p.NextOfKinPhone.Length > 0 && !Phone.IsMatch(p.NextOfKinPhone)) return "Next of kin phone looks wrong.";
        if (p.DateOfBirth is DateTime d && (d > DateTime.Today.AddYears(-16) || d < DateTime.Today.AddYears(-100))) return "Check the date of birth.";
        if (p.County.Length > 0 && !Counties.Contains(p.County)) return "Pick a county from the list.";
        if (p.Bio.Length > 600 || p.JobTitle.Length > 80 || p.Department.Length > 80 || p.Branch.Length > 80 || p.PostalAddress.Length > 120 || p.NextOfKinName.Length > 120 || p.StaffNo.Length > 30)
            return "One of the fields is too long.";
        return null;
    }

    public async Task<string?> SaveAsync(UserProfile p)
    {
        var err = Validate(p);
        if (err != null) return err;
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"MERGE dbo.UserProfiles AS t USING (SELECT @u AS UserId) s ON t.UserId = s.UserId
            WHEN MATCHED THEN UPDATE SET Photo=@ph, StaffNo=@sn, JobTitle=@jt, Department=@dp, Branch=@br, NationalId=@ni, KraPin=@kp, DateOfBirth=@dob,
                Gender=@g, County=@co, PostalAddress=@pa, AltPhone=@ap, Language=@la, NextOfKinName=@kn, NextOfKinRelation=@kr, NextOfKinPhone=@kph, Bio=@bio, UpdatedUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (UserId, Photo, StaffNo, JobTitle, Department, Branch, NationalId, KraPin, DateOfBirth, Gender, County, PostalAddress, AltPhone, Language, NextOfKinName, NextOfKinRelation, NextOfKinPhone, Bio)
                VALUES (@u, @ph, @sn, @jt, @dp, @br, @ni, @kp, @dob, @g, @co, @pa, @ap, @la, @kn, @kr, @kph, @bio);",
            ("u", p.UserId), ("ph", p.Photo), ("sn", p.StaffNo), ("jt", p.JobTitle), ("dp", p.Department), ("br", p.Branch), ("ni", p.NationalId), ("kp", p.KraPin),
            ("dob", p.DateOfBirth), ("g", p.Gender), ("co", p.County), ("pa", p.PostalAddress), ("ap", p.AltPhone), ("la", p.Language),
            ("kn", p.NextOfKinName), ("kr", p.NextOfKinRelation), ("kph", p.NextOfKinPhone), ("bio", p.Bio));
        lock (_ver) _ver[p.UserId] = string.IsNullOrEmpty(p.Photo) ? "" : DateTime.UtcNow.Ticks.ToString();
        Changed?.Invoke(p.UserId);
        return null;
    }
}
