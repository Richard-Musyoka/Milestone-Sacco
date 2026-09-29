using System.Text.RegularExpressions;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

/// <summary>Kenyan KYC details that sit beside the core Members row (keyed by member number): photo, KRA PIN, county, check-off, next of kin.</summary>
public sealed class MemberExtras
{
    public string MemberNo { get; set; } = "";
    public string Photo { get; set; } = "";
    public string KraPin { get; set; } = "";
    public string County { get; set; } = "";
    public string PostalAddress { get; set; } = "";
    public string PayrollNo { get; set; } = "";
    public bool CheckOff { get; set; }
    public string NextOfKinName { get; set; } = "";
    public string NextOfKinRelation { get; set; } = "";
    public string NextOfKinPhone { get; set; } = "";
    public string NextOfKinIdNo { get; set; } = "";
    public string PreferredChannel { get; set; } = "SMS";
    public DateTime? UpdatedUtc { get; set; }

    public MemberExtras Copy() => (MemberExtras)MemberwiseClone();
}

public sealed class MemberExtrasStore
{
    private static readonly Regex Img = new(@"^data:image/(png|jpeg|jpg|webp);base64,[A-Za-z0-9+/=]+$", RegexOptions.Compiled);
    private static readonly Regex Pin = new("^[AP][0-9]{9}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"^\+?[0-9 ]{9,16}$", RegexOptions.Compiled);
    private readonly IDbFactory _db;
    public MemberExtrasStore(IDbFactory db) => _db = db;

    public async Task EnsureSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"IF OBJECT_ID('dbo.MemberExtras') IS NULL
            CREATE TABLE dbo.MemberExtras (MemberNo NVARCHAR(40) NOT NULL PRIMARY KEY, Photo NVARCHAR(MAX) NULL, KraPin NVARCHAR(20) NULL, County NVARCHAR(40) NULL,
              PostalAddress NVARCHAR(120) NULL, PayrollNo NVARCHAR(40) NULL, CheckOff BIT NOT NULL DEFAULT 0, NextOfKinName NVARCHAR(120) NULL,
              NextOfKinRelation NVARCHAR(40) NULL, NextOfKinPhone NVARCHAR(30) NULL, NextOfKinIdNo NVARCHAR(20) NULL, PreferredChannel NVARCHAR(20) NULL,
              UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());");
    }

    public async Task<MemberExtras> GetAsync(string memberNo)
    {
        if (string.IsNullOrWhiteSpace(memberNo)) return new MemberExtras();
        try
        {
            await using var c = await _db.OpenAsync();
            return await c.FirstAsync("SELECT * FROM dbo.MemberExtras WHERE MemberNo = @m", r => new MemberExtras
            {
                MemberNo = r.Str("MemberNo"), Photo = r.Str("Photo"), KraPin = r.Str("KraPin"), County = r.Str("County"), PostalAddress = r.Str("PostalAddress"),
                PayrollNo = r.Str("PayrollNo"), CheckOff = r.Bool("CheckOff"), NextOfKinName = r.Str("NextOfKinName"), NextOfKinRelation = r.Str("NextOfKinRelation"),
                NextOfKinPhone = r.Str("NextOfKinPhone"), NextOfKinIdNo = r.Str("NextOfKinIdNo"), PreferredChannel = r.Str("PreferredChannel"), UpdatedUtc = r.DateN("UpdatedUtc")
            }, ("m", memberNo)) ?? new MemberExtras { MemberNo = memberNo };
        }
        catch { return new MemberExtras { MemberNo = memberNo }; }
    }

    /// <summary>Checks the member and extras against the organisation's membership rules. Returns a message or null.</summary>
    public static string? Check(SaccoManagementSystem.Models.MemberModel m, MemberExtras x, OrgConfig cfg)
    {
        string T(string? v) => (v ?? "").Trim();
        x.KraPin = T(x.KraPin).ToUpperInvariant(); x.NextOfKinName = T(x.NextOfKinName); x.NextOfKinPhone = T(x.NextOfKinPhone); x.PayrollNo = T(x.PayrollNo);
        if (m.DateOfBirth is DateTime dob)
        {
            if (dob > DateTime.Today) return "Date of birth can't be in the future.";
            var age = DateTime.Today.Year - dob.Year - (DateTime.Today.DayOfYear < dob.DayOfYear ? 1 : 0);
            if (age < cfg.MinMemberAge) return $"Members must be at least {cfg.MinMemberAge} years old. This date of birth makes them {age}.";
            if (age > 110) return "Check the date of birth.";
        }
        if (!string.IsNullOrWhiteSpace(m.NationalID) && !Regex.IsMatch(m.NationalID.Trim(), "^[A-Za-z0-9]{6,12}$")) return "National ID or passport number should be 6 to 12 letters or digits.";
        if (!string.IsNullOrWhiteSpace(m.PhoneNumber) && !Phone.IsMatch(m.PhoneNumber.Trim())) return "Phone number looks wrong. Use 07XX XXX XXX or +254 7XX XXX XXX.";
        if (cfg.RequireKraPin && x.KraPin.Length == 0) return "KRA PIN is required by your membership rules.";
        if (x.KraPin.Length > 0 && !Pin.IsMatch(x.KraPin)) return "KRA PIN is 11 characters, like A001234567Z.";
        if (cfg.RequireNextOfKin && (x.NextOfKinName.Length == 0 || x.NextOfKinPhone.Length == 0)) return "Next of kin name and phone are required by your membership rules.";
        if (x.NextOfKinPhone.Length > 0 && !Phone.IsMatch(x.NextOfKinPhone)) return "Next of kin phone looks wrong.";
        if (x.CheckOff && x.PayrollNo.Length == 0) return "Add the payroll number for check-off deductions.";
        if (!string.IsNullOrEmpty(x.Photo) && (x.Photo.Length > 1_400_000 || !Img.IsMatch(x.Photo))) return "The photo must be a JPG, PNG or WebP under 1 MB.";
        return null;
    }

    /// <summary>0712345678 → +254 712 345 678 (other formats are left alone).</summary>
    public static string KenyanPhone(string? p)
    {
        var d = new string((p ?? "").Where(char.IsDigit).ToArray());
        if (d.Length == 10 && (d.StartsWith("07") || d.StartsWith("01"))) d = "254" + d[1..];
        if (d.Length == 12 && d.StartsWith("254")) return "+254 " + d.Substring(3, 3) + " " + d.Substring(6, 3) + " " + d.Substring(9, 3);
        return (p ?? "").Trim();
    }

    public async Task SaveAsync(MemberExtras x)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"MERGE dbo.MemberExtras AS t USING (SELECT @m AS MemberNo) s ON t.MemberNo = s.MemberNo
            WHEN MATCHED THEN UPDATE SET Photo=@ph, KraPin=@kp, County=@co, PostalAddress=@pa, PayrollNo=@pn, CheckOff=@ck, NextOfKinName=@kn, NextOfKinRelation=@kr,
                NextOfKinPhone=@kph, NextOfKinIdNo=@kid, PreferredChannel=@ch, UpdatedUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (MemberNo, Photo, KraPin, County, PostalAddress, PayrollNo, CheckOff, NextOfKinName, NextOfKinRelation, NextOfKinPhone, NextOfKinIdNo, PreferredChannel)
                VALUES (@m, @ph, @kp, @co, @pa, @pn, @ck, @kn, @kr, @kph, @kid, @ch);",
            ("m", x.MemberNo), ("ph", x.Photo), ("kp", x.KraPin), ("co", x.County), ("pa", x.PostalAddress), ("pn", x.PayrollNo), ("ck", x.CheckOff),
            ("kn", x.NextOfKinName), ("kr", x.NextOfKinRelation), ("kph", x.NextOfKinPhone), ("kid", x.NextOfKinIdNo), ("ch", x.PreferredChannel));
    }

    public sealed record Lite(int MemberId, string Name, string MemberNo, string Phone, string Status, string? Photo);
    public sealed record LoanLite(int LoanId, string LoanType, decimal Balance, decimal Installment, string Status);

    /// <summary>Everyone, for pickers (small projection, no photos' bytes).</summary>
    public async Task<List<Lite>> DirectoryAsync()
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT m.MemberId, CONCAT(m.FirstName, ' ', m.LastName) AS FullName, m.MemberNo, m.PhoneNumber, m.Status, m.ProfileImageUrl FROM Members m ORDER BY m.FirstName, m.LastName",
            r => new Lite(r.Int("MemberId"), r.Str("FullName"), r.Str("MemberNo"), r.Str("PhoneNumber"), r.Str("Status"), r.StrN("ProfileImageUrl")));
    }

    public async Task<List<LoanLite>> OpenLoansAsync(int memberId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT LoanId, LoanType, ISNULL(OutstandingBalance, PrincipalAmount) AS Bal, ISNULL(MonthlyInstallment,0) AS Inst, Status FROM Loans
            WHERE MemberId = @m AND Status IN ('Approved','Disbursed','Active') ORDER BY ApplicationDate DESC",
            r => new LoanLite(r.Int("LoanId"), r.Str("LoanType"), Convert.ToDecimal(r["Bal"]), Convert.ToDecimal(r["Inst"]), r.Str("Status")), ("m", memberId));
    }

    public static string PhotoUrl(string memberNo) => "media/member/" + Uri.EscapeDataString(memberNo) + "?v=" + DateTime.UtcNow.Ticks;
}
