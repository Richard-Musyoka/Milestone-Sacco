using System.Data.Common;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

/// <summary>
/// Everything a signed-in member may see about themselves - and nothing else. Every query is filtered by the
/// member id taken from the member's own sign-in claim, never from the URL, so one member can't read another's money.
/// Also owns the link between a member record and the Users row used for their portal sign-in.
/// </summary>
public sealed class MemberPortalStore
{
    public const string MemberClaim = "member_id";
    private const string ContribOk = "ISNULL(Status,'') NOT IN ('Cancelled','Rejected','Failed','Reversed')";
    private readonly IDbFactory _db;
    private readonly ILogger<MemberPortalStore> _log;
    public MemberPortalStore(IDbFactory db, ILogger<MemberPortalStore> log) { _db = db; _log = log; }

    public async Task EnsureSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"
IF COL_LENGTH('dbo.Users','MemberId') IS NULL ALTER TABLE dbo.Users ADD MemberId INT NULL;");
        await c.ExecAsync(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_MemberId') CREATE INDEX IX_Users_MemberId ON dbo.Users (MemberId);");
    }

    // ================================================================ sign-in link
    public sealed record LoginMember(int MemberId, string MemberNo, string First, string Last, string Email, string Phone, string Status);

    private static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    /// <summary>Finds a member by member number, national ID or phone (07.., 7.., +2547..).</summary>
    public async Task<LoginMember?> FindForLoginAsync(string identifier)
    {
        var id = identifier.Trim();
        var d = Digits(id);
        var last9 = d.Length >= 9 ? d[^9..] : "";
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync(@"
SELECT TOP 1 MemberId, MemberNo, FirstName, LastName, ISNULL(Email,'') Email, ISNULL(PhoneNumber,'') PhoneNumber, ISNULL(Status,'') Status
FROM Members
WHERE MemberNo = @id OR NationalID = @id
   OR (@l9 <> '' AND RIGHT(REPLACE(REPLACE(REPLACE(ISNULL(PhoneNumber,''),' ',''),'-',''),'+',''), 9) = @l9)
ORDER BY CASE WHEN MemberNo = @id THEN 0 WHEN NationalID = @id THEN 1 ELSE 2 END",
            r => new LoginMember(r.Int("MemberId"), r.Str("MemberNo"), r.Str("FirstName"), r.Str("LastName"), r.Str("Email"), r.Str("PhoneNumber"), r.Str("Status")),
            ("id", id), ("l9", last9));
    }

    /// <summary>Returns the Users.Id that represents this member in the portal, creating it the first time.</summary>
    public async Task<int> EnsureUserAsync(LoginMember m)
    {
        await using var c = await _db.OpenAsync();
        var existing = await c.ScalarAsync<int?>("SELECT TOP 1 Id FROM Users WHERE MemberId = @m AND Role = 'Member'", ("m", m.MemberId));
        var email = m.Email.Contains('@') ? m.Email.Trim() : $"m-{m.MemberNo.ToLowerInvariant().Replace(' ', '-')}@members.local";
        if (existing is int uid)
        {
            // keep contact details in step with the member record so codes go to the right place
            await c.ExecAsync("UPDATE Users SET PhoneNumber = @p, FirstName = @f, LastName = @l WHERE Id = @id",
                ("p", m.Phone), ("f", m.First), ("l", m.Last), ("id", uid));
            return uid;
        }
        // a staff account may already use this email: members get their own row with a member-scoped address
        if (await c.ScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Email = @e", ("e", email)) > 0)
            email = $"m-{m.MemberNo.ToLowerInvariant().Replace(' ', '-')}@members.local";
        var unusable = PasswordHasher.Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        return await c.ScalarAsync<int>(@"
INSERT INTO Users (FirstName, MiddleName, LastName, UserName, Password, PasswordHash, Email, PhoneNumber, CreatedDate, Role, PasswordChangedUtc, MemberId, IsActive)
OUTPUT INSERTED.Id
VALUES (@f, NULL, @l, @u, '', @h, @e, @p, GETDATE(), 'Member', SYSUTCDATETIME(), @m, 1)",
            ("f", m.First), ("l", m.Last), ("u", m.MemberNo), ("h", unusable), ("e", email), ("p", m.Phone), ("m", m.MemberId));
    }

    public async Task<int?> MemberIdForUserAsync(int userId)
    {
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int?>("SELECT MemberId FROM Users WHERE Id = @id", ("id", userId));
    }

    public sealed record PortalAccess(int UserId, bool Active, DateTime? LastLoginUtc);
    public async Task<PortalAccess?> AccessForMemberAsync(int memberId)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync("SELECT TOP 1 Id, IsActive, LastLoginUtc FROM Users WHERE MemberId = @m AND Role = 'Member'",
            r => new PortalAccess(r.Int("Id"), r.Bool("IsActive"), r.DateN("LastLoginUtc")), ("m", memberId));
    }

    public async Task SetAccessAsync(int memberId, bool active)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE Users SET IsActive = @a WHERE MemberId = @m AND Role = 'Member'", ("a", active), ("m", memberId));
    }

    // ================================================================ portal data
    public sealed record Point(string Label, decimal Amount);
    public sealed record Loan(int LoanId, string Type, decimal Principal, decimal Rate, int Term, decimal Monthly, decimal TotalPayable, decimal Balance, string Status, DateTime Applied, DateTime? Start, DateTime? End)
    {
        public string No => "LN-" + LoanId.ToString("D4");
        public decimal Repaid => Math.Max(0, TotalPayable - Balance);
        public double RepaidPct => TotalPayable <= 0 ? 0 : Math.Min(100, (double)(Repaid / TotalPayable * 100));
        public bool Live => Status is "Disbursed" or "Active" or "Approved";
    }
    public sealed record Installment(int LoanId, int No, DateTime Due, decimal Amount, string Status, DateTime? Paid);
    public sealed record Dividend(string Year, decimal Rate, decimal Shares, decimal Amount, string Status, DateTime? PaidOn, string Method, string Reference);
    public sealed record ShareLot(DateTime Date, decimal Units, decimal UnitPrice, decimal Value, string Type, string Status);
    public sealed record Chama(int Id, string Name, string Type, decimal Amount, string Frequency, string Role, int Position, int Cycle, int Members, decimal MyTotal, string Color,
                               string? NextName, DateTime? NextDate, int? MyCycle, DateTime? MyDate, string? MyStatus, decimal Pot);
    public sealed record Txn(DateTime When, string Kind, string Title, string Reference, decimal Amount, bool In);
    public sealed record Guarantee(string LoanNo, string Borrower, decimal Principal, decimal Balance, string Status);

    public sealed class Data
    {
        public int MemberId { get; set; }
        public string MemberNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string First { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? Joined { get; set; }
        public decimal Deposits { get; set; }
        public decimal DepositsThisYear { get; set; }
        public decimal ShareUnits { get; set; }
        public decimal SharesValue { get; set; }
        public decimal LoanBalance { get; set; }
        public int Multiple { get; set; } = 3;
        public decimal DividendsPaid { get; set; }
        public decimal DividendsDue { get; set; }
        public int MonthsSaved { get; set; }
        public int Streak { get; set; }
        public List<Point> Monthly { get; set; } = new();
        public List<Loan> Loans { get; set; } = new();
        public List<Installment> Upcoming { get; set; } = new();
        public List<Dividend> Dividends { get; set; } = new();
        public List<ShareLot> Shares { get; set; } = new();
        public List<Chama> Chamas { get; set; } = new();
        public List<Txn> Statement { get; set; } = new();
        public List<Guarantee> Guaranteeing { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public decimal LoanLimit => Math.Max(0, Deposits * Multiple - LoanBalance);
        public Installment? NextDue => Upcoming.OrderBy(i => i.Due).FirstOrDefault();
        public decimal NetWorth => Deposits + SharesValue + DividendsDue - LoanBalance;
    }

    private async Task Safe(Data d, string part, Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex) { _log.LogWarning(ex, "Member portal part {Part} failed", part); d.Warnings.Add(part); }
    }

    public async Task<Data?> LoadAsync(int memberId)
    {
        await using var c = await _db.OpenAsync();
        var d = await c.FirstAsync("SELECT MemberId, MemberNo, FirstName, LastName, ISNULL(PhoneNumber,'') PhoneNumber, ISNULL(Email,'') Email, ISNULL(Status,'') Status, JoinDate FROM Members WHERE MemberId = @m",
            r => new Data
            {
                MemberId = r.Int("MemberId"), MemberNo = r.Str("MemberNo"), First = r.Str("FirstName"),
                Name = (r.Str("FirstName") + " " + r.Str("LastName")).Trim(), Phone = r.Str("PhoneNumber"),
                Email = r.Str("Email"), Status = r.Str("Status"), Joined = r.DateN("JoinDate")
            }, ("m", memberId));
        if (d == null) return null;
        var p = ("m", (object?)memberId);

        await Safe(d, "Rules", async () =>
            d.Multiple = Math.Max(1, await c.ScalarAsync<int?>("SELECT TOP 1 MaxLoanAmountMultiple FROM SaccoSettings ORDER BY Id DESC") ?? 3));

        await Safe(d, "Savings", async () =>
        {
            d.Deposits = await c.ScalarAsync<decimal?>($"SELECT SUM(Amount) FROM Contributions WHERE MemberId = @m AND {ContribOk}", p) ?? 0;
            d.DepositsThisYear = await c.ScalarAsync<decimal?>($"SELECT SUM(Amount) FROM Contributions WHERE MemberId = @m AND {ContribOk} AND YEAR(DateContributed) = YEAR(GETDATE())", p) ?? 0;
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
            var rows = await c.QueryAsync($@"SELECT YEAR(DateContributed) y, MONTH(DateContributed) mo, SUM(Amount) a FROM Contributions
                WHERE MemberId = @m AND {ContribOk} AND DateContributed >= @s GROUP BY YEAR(DateContributed), MONTH(DateContributed)",
                r => (Y: r.Int("y"), M: r.Int("mo"), A: Convert.ToDecimal(r["a"])), p, ("s", start));
            for (var i = 0; i < 12; i++)
            {
                var dt = start.AddMonths(i);
                d.Monthly.Add(new Point(dt.ToString("MMM"), rows.Where(x => x.Y == dt.Year && x.M == dt.Month).Sum(x => x.A)));
            }
            d.MonthsSaved = d.Monthly.Count(x => x.Amount > 0);
            for (var i = d.Monthly.Count - 1; i >= 0; i--)
            {
                if (d.Monthly[i].Amount > 0) d.Streak++;
                else if (i == d.Monthly.Count - 1) continue;    // this month may simply not be paid yet
                else break;
            }
        });

        await Safe(d, "Shares", async () =>
        {
            d.Shares = await c.QueryAsync("SELECT PurchaseDate, Units, UnitPrice, TotalValue, ISNULL(ShareType,'') ShareType, ISNULL(Status,'') Status FROM Shares WHERE MemberId = @m ORDER BY PurchaseDate DESC",
                r => new ShareLot(r.Date("PurchaseDate"), Convert.ToDecimal(r["Units"]), Convert.ToDecimal(r["UnitPrice"]), Convert.ToDecimal(r["TotalValue"]), r.Str("ShareType"), r.Str("Status")), p);
            var live = d.Shares.Where(s => s.Status == "Active").ToList();
            d.ShareUnits = live.Sum(s => s.Units); d.SharesValue = live.Sum(s => s.Value);
        });

        await Safe(d, "Loans", async () =>
        {
            d.Loans = await c.QueryAsync(@"SELECT LoanId, ISNULL(LoanType,'Loan') LoanType, PrincipalAmount, ISNULL(InterestRate,0) InterestRate, ISNULL(TermMonths,0) TermMonths,
                    ISNULL(MonthlyInstallment,0) MonthlyInstallment, ISNULL(TotalPayable, PrincipalAmount) TotalPayable, ISNULL(OutstandingBalance, PrincipalAmount) OutstandingBalance,
                    ISNULL(Status,'') Status, ApplicationDate, StartDate, EndDate
                FROM Loans WHERE MemberId = @m ORDER BY ApplicationDate DESC",
                r => new Loan(r.Int("LoanId"), r.Str("LoanType"), Convert.ToDecimal(r["PrincipalAmount"]), Convert.ToDecimal(r["InterestRate"]), r.Int("TermMonths"),
                    Convert.ToDecimal(r["MonthlyInstallment"]), Convert.ToDecimal(r["TotalPayable"]), Convert.ToDecimal(r["OutstandingBalance"]), r.Str("Status"),
                    r.Date("ApplicationDate"), r.DateN("StartDate"), r.DateN("EndDate")), p);
            d.LoanBalance = d.Loans.Where(l => l.Status is "Disbursed" or "Active").Sum(l => l.Balance);
        });

        await Safe(d, "Repayments", async () =>
            d.Upcoming = await c.QueryAsync(@"SELECT TOP 12 i.LoanId, i.InstallmentNumber, i.DueDate, i.TotalAmount, ISNULL(i.Status,'Pending') Status, i.PaymentDate
                FROM LoanInstallments i JOIN Loans l ON l.LoanId = i.LoanId
                WHERE l.MemberId = @m AND l.Status IN ('Disbursed','Active') AND ISNULL(i.Status,'') <> 'Paid' ORDER BY i.DueDate",
                r => new Installment(r.Int("LoanId"), r.Int("InstallmentNumber"), r.Date("DueDate"), Convert.ToDecimal(r["TotalAmount"]), r.Str("Status"), r.DateN("PaymentDate")), p));

        await Safe(d, "Dividends", async () =>
        {
            d.Dividends = await c.QueryAsync(@"SELECT dp.FinancialYear, ISNULL(dd.Rate,0) Rate, ISNULL(dp.Shares,0) Shares, dp.Amount, ISNULL(dp.Status,'') Status, dp.PaymentDate,
                    ISNULL(dp.PaymentMethod,'') PaymentMethod, ISNULL(dp.TransactionReference,'') TransactionReference
                FROM DividendPayments dp LEFT JOIN DividendDeclarations dd ON dd.DeclarationId = dp.DeclarationId
                WHERE dp.MemberId = @m ORDER BY dp.FinancialYear DESC, dp.CreatedDate DESC",
                r => new Dividend(r.Str("FinancialYear"), Convert.ToDecimal(r["Rate"]), Convert.ToDecimal(r["Shares"]), Convert.ToDecimal(r["Amount"]), r.Str("Status"),
                    r.DateN("PaymentDate"), r.Str("PaymentMethod"), r.Str("TransactionReference")), p);
            d.DividendsPaid = d.Dividends.Where(x => x.Status == "Paid").Sum(x => x.Amount);
            d.DividendsDue = d.Dividends.Where(x => x.Status is "Pending" or "Processing" or "Approved").Sum(x => x.Amount);
        });

        await Safe(d, "Chamas", async () =>
            d.Chamas = await c.QueryAsync(@"
SELECT ch.Id, ch.Name, ch.Type, ch.ContributionAmount, ch.Frequency, cm.Role, cm.Position, ch.CurrentCycle, ISNULL(ch.Color,'') Color,
       (SELECT COUNT(*) FROM dbo.ChamaMembers x WHERE x.ChamaId = ch.Id AND x.Status = 'Active') Members,
       (SELECT ISNULL(SUM(Amount),0) FROM dbo.ChamaContributions cc WHERE cc.ChamaId = ch.Id AND cc.MemberId = @m) MyTotal,
       (SELECT ISNULL(SUM(Amount),0) FROM dbo.ChamaContributions cc WHERE cc.ChamaId = ch.Id AND cc.CycleNo = ch.CurrentCycle) Pot,
       nx.NextName, nx.DueDate NextDate, me.CycleNo MyCycle, me.DueDate MyDate, me.Status MyStatus
FROM dbo.ChamaMembers cm JOIN dbo.Chamas ch ON ch.Id = cm.ChamaId
OUTER APPLY (SELECT TOP 1 CONCAT(m.FirstName,' ',m.LastName) NextName, p.DueDate FROM dbo.ChamaPayouts p JOIN Members m ON m.MemberId = p.MemberId
             WHERE p.ChamaId = ch.Id AND p.Status <> 'Paid' ORDER BY p.CycleNo) nx
OUTER APPLY (SELECT TOP 1 p.CycleNo, p.DueDate, p.Status FROM dbo.ChamaPayouts p WHERE p.ChamaId = ch.Id AND p.MemberId = @m ORDER BY p.CycleNo DESC) me
WHERE cm.MemberId = @m AND cm.Status = 'Active' ORDER BY ch.Name",
                r => new Chama(r.Int("Id"), r.Str("Name"), r.Str("Type"), Convert.ToDecimal(r["ContributionAmount"]), r.Str("Frequency"), r.Str("Role"), r.Int("Position"),
                    r.Int("CurrentCycle"), r.Int("Members"), Convert.ToDecimal(r["MyTotal"]), r.Str("Color"), r.StrN("NextName"), r.DateN("NextDate"),
                    r.IsDBNull(r.GetOrdinal("MyCycle")) ? null : r.Int("MyCycle"), r.DateN("MyDate"), r.StrN("MyStatus"), Convert.ToDecimal(r["Pot"])), p));

        await Safe(d, "Guarantees", async () =>
            d.Guaranteeing = await c.QueryAsync(@"
SELECT l.LoanId, CONCAT(bm.FirstName,' ',bm.LastName) Borrower, l.PrincipalAmount, ISNULL(l.OutstandingBalance, l.PrincipalAmount) Bal, ISNULL(l.Status,'') Status
FROM Loans l JOIN Guarantors g ON g.GuarantorId IN (l.Guarantor1Id, l.Guarantor2Id) JOIN Members bm ON bm.MemberId = l.MemberId
WHERE g.MemberId = @m AND l.MemberId <> @m AND l.Status IN ('Approved','Disbursed','Active')",
                r => new Guarantee("LN-" + r.Int("LoanId").ToString("D4"), r.Str("Borrower"), Convert.ToDecimal(r["PrincipalAmount"]), Convert.ToDecimal(r["Bal"]), r.Str("Status")), p));

        await Safe(d, "Statement", async () =>
        {
            var t = new List<Txn>();
            t.AddRange(await c.QueryAsync($"SELECT TOP 200 DateContributed, ISNULL(ContributionType,'Deposit') ContributionType, ISNULL(TransactionRef,'') TransactionRef, ISNULL(PaymentMethod,'') PaymentMethod, Amount FROM Contributions WHERE MemberId = @m AND {ContribOk} ORDER BY DateContributed DESC",
                r => new Txn(r.Date("DateContributed"), "Deposit", r.Str("ContributionType") + (r.Str("PaymentMethod") == "" ? "" : " · " + r.Str("PaymentMethod")), r.Str("TransactionRef"), Convert.ToDecimal(r["Amount"]), true), p));
            t.AddRange(d.Shares.Where(s => s.Status != "Cancelled").Select(s => new Txn(s.Date, "Shares", $"Bought {s.Units:N0} shares at {s.UnitPrice:N0}", "", s.Value, true)));
            t.AddRange(d.Loans.Where(l => l.Start.HasValue).Select(l => new Txn(l.Start!.Value, "Loan", l.Type + " paid out", l.No, l.Principal, false)));
            t.AddRange(await c.QueryAsync(@"SELECT TOP 200 i.PaymentDate, i.TotalAmount, i.InstallmentNumber, i.LoanId FROM LoanInstallments i JOIN Loans l ON l.LoanId = i.LoanId
                WHERE l.MemberId = @m AND i.Status = 'Paid' AND i.PaymentDate IS NOT NULL ORDER BY i.PaymentDate DESC",
                r => new Txn(r.Date("PaymentDate"), "Repayment", "Instalment " + r.Int("InstallmentNumber"), "LN-" + r.Int("LoanId").ToString("D4"), Convert.ToDecimal(r["TotalAmount"]), true), p));
            t.AddRange(d.Dividends.Where(x => x.Status == "Paid" && x.PaidOn.HasValue).Select(x => new Txn(x.PaidOn!.Value, "Dividend", "Dividend " + x.Year + (x.Method == "" ? "" : " · " + x.Method), x.Reference, x.Amount, false)));
            d.Statement = t.OrderByDescending(x => x.When).ToList();
        });

        return d;
    }
}
