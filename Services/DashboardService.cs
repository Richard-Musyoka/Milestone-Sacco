using System.Data.Common;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

public sealed record MonthPoint(string Label, decimal Contributions, decimal Loans);
public sealed record ActivityItem(string Kind, string Title, string Subtitle, decimal Amount, DateTime When);
public sealed record PendingLoan(int LoanId, string Member, string LoanType, decimal Amount, DateTime Applied);
public sealed record StatusSlice(string Label, int Count);

public sealed class DashboardData
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int NewMembers30d { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal ContributionsThisMonth { get; set; }
    public decimal ContributionsLastMonth { get; set; }
    public int ActiveLoans { get; set; }
    public decimal LoanBook { get; set; }
    public int PendingLoansCount { get; set; }
    public decimal SharesValue { get; set; }
    public int ShareholderCount { get; set; }
    public decimal DividendsPaid { get; set; }
    public decimal? DividendRate { get; set; }
    public string? DividendYear { get; set; }
    public List<MonthPoint> Months { get; set; } = new();
    public List<ActivityItem> Activity { get; set; } = new();
    public List<PendingLoan> Pending { get; set; } = new();
    public List<StatusSlice> LoanStatuses { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    // ---- demographics ----
    public List<StatusSlice> Genders { get; set; } = new();
    public List<StatusSlice> AgeBands { get; set; } = new();
    public List<StatusSlice> Marital { get; set; } = new();
    public List<StatusSlice> Occupations { get; set; } = new();
    public List<StatusSlice> MemberStatuses { get; set; } = new();
    public List<StatusSlice> Tenure { get; set; } = new();
    public List<MonthPoint> Joins { get; set; } = new();            // Contributions = new members that month
    public List<(string Label, decimal Amount)> ContribTypes { get; set; } = new();
    public double? AverageAge { get; set; }
    public int WithDob { get; set; }
    public int WithGender { get; set; }
    public int Participants90d { get; set; }
    public int Borrowers { get; set; }
    public int YouthCount { get; set; }        // aged 18-35

    public double FemalePct => Genders.Sum(g => g.Count) == 0 ? 0 : Genders.Where(g => g.Label == "Female").Sum(g => g.Count) * 100.0 / Genders.Sum(g => g.Count);
    public double YouthPct => WithDob == 0 ? 0 : YouthCount * 100.0 / WithDob;
    public double ParticipationPct => ActiveMembers == 0 ? 0 : Math.Min(100, Participants90d * 100.0 / ActiveMembers);
    public double BorrowerPct => ActiveMembers == 0 ? 0 : Math.Min(100, Borrowers * 100.0 / ActiveMembers);
    public double CompletenessPct => TotalMembers == 0 ? 0 : (WithDob + WithGender) * 50.0 / TotalMembers;

    public double ContributionDeltaPct => ContributionsLastMonth <= 0 ? 0 : (double)((ContributionsThisMonth - ContributionsLastMonth) / ContributionsLastMonth * 100m);
}

/// <summary>Read-only aggregates for the executive dashboard. Every query is isolated so one missing table never blanks the page.</summary>
public sealed class DashboardService
{
    private readonly IDbFactory _db;
    public DashboardService(IDbFactory db) => _db = db;

    private const string ContribOk = "ISNULL(Status,'') NOT IN ('Cancelled','Rejected','Failed','Reversed')";

    public async Task<DashboardData> LoadAsync()
    {
        var d = new DashboardData();
        DbConnection conn;
        try { conn = await _db.OpenAsync(); }
        catch (Exception ex) { d.Warnings.Add("Database unavailable: " + ex.Message); return d; }

        await using (conn)
        {
            async Task Try(string what, Func<Task> work)
            {
                try { await work(); } catch (Exception ex) { d.Warnings.Add($"{what}: {ex.Message}"); }
            }

            await Try("Members", async () =>
            {
                d.TotalMembers = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members");
                d.ActiveMembers = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members WHERE Status = 'Active'");
                d.NewMembers30d = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members WHERE COALESCE(JoinDate, CreatedDate) >= DATEADD(DAY, -30, GETDATE())");
            });

            await Try("Contributions", async () =>
            {
                d.TotalContributions = await conn.ScalarAsync<decimal?>($"SELECT SUM(Amount) FROM Contributions WHERE {ContribOk}") ?? 0;
                d.ContributionsThisMonth = await conn.ScalarAsync<decimal?>(
                    $"SELECT SUM(Amount) FROM Contributions WHERE {ContribOk} AND DateContributed >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)") ?? 0;
                d.ContributionsLastMonth = await conn.ScalarAsync<decimal?>(
                    $@"SELECT SUM(Amount) FROM Contributions WHERE {ContribOk}
                       AND DateContributed >= DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
                       AND DateContributed <  DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)") ?? 0;
            });

            await Try("Loans", async () =>
            {
                d.ActiveLoans = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Loans WHERE Status IN ('Approved','Disbursed','Active')");
                d.LoanBook = await conn.ScalarAsync<decimal?>("SELECT SUM(CASE WHEN OutstandingBalance IS NULL THEN PrincipalAmount WHEN OutstandingBalance > 0 THEN OutstandingBalance ELSE 0 END) FROM Loans WHERE Status IN ('Approved','Disbursed','Active')") ?? 0;
                d.PendingLoansCount = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Loans WHERE Status = 'Pending'");
                d.LoanStatuses = await conn.QueryAsync("SELECT Status, COUNT(*) AS N FROM Loans GROUP BY Status ORDER BY N DESC",
                    r => new StatusSlice(r.Str("Status"), r.Int("N")));
                d.Pending = await conn.QueryAsync(@"SELECT TOP 5 l.LoanId, l.LoanType, l.PrincipalAmount, l.ApplicationDate,
                        LTRIM(RTRIM(ISNULL(m.FirstName,'') + ' ' + ISNULL(m.LastName,''))) AS MemberName
                    FROM Loans l LEFT JOIN Members m ON m.MemberId = l.MemberId
                    WHERE l.Status = 'Pending' ORDER BY l.ApplicationDate DESC",
                    r => new PendingLoan(r.Int("LoanId"), r.Str("MemberName"), r.Str("LoanType"), Convert.ToDecimal(r["PrincipalAmount"]), r.Date("ApplicationDate")));
            });

            await Try("Shares", async () =>
            {
                d.SharesValue = await conn.ScalarAsync<decimal?>("SELECT SUM(TotalValue) FROM Shares WHERE ISNULL(Status,'Active') = 'Active'") ?? 0;
                d.ShareholderCount = await conn.ScalarAsync<int>("SELECT COUNT(DISTINCT MemberId) FROM Shares WHERE ISNULL(Status,'Active') = 'Active'");
            });

            await Try("Dividends", async () =>
            {
                d.DividendsPaid = await conn.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM DividendPayments WHERE Status = 'Paid'") ?? 0;
                d.DividendRate = await conn.ScalarAsync<decimal?>("SELECT TOP 1 Rate FROM DividendDeclarations ORDER BY DeclarationDate DESC");
                d.DividendYear = await conn.ScalarAsync<string?>("SELECT TOP 1 CAST(FinancialYear AS NVARCHAR(20)) FROM DividendDeclarations ORDER BY DeclarationDate DESC");
            });

            await Try("Trend", async () =>
            {
                var contrib = new Dictionary<string, decimal>();
                var loans = new Dictionary<string, decimal>();
                var rows = await conn.QueryAsync($@"SELECT FORMAT(DateContributed,'yyyy-MM') AS M, SUM(Amount) AS T FROM Contributions
                        WHERE {ContribOk} AND DateContributed >= DATEADD(MONTH, -5, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
                        GROUP BY FORMAT(DateContributed,'yyyy-MM')", r => (r.Str("M"), Convert.ToDecimal(r["T"])));
                foreach (var (m, t) in rows) contrib[m] = t;
                try
                {
                    var lrows = await conn.QueryAsync(@"SELECT FORMAT(ApplicationDate,'yyyy-MM') AS M, SUM(PrincipalAmount) AS T FROM Loans
                        WHERE Status IN ('Approved','Disbursed','Active','Completed') AND ApplicationDate >= DATEADD(MONTH, -5, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
                        GROUP BY FORMAT(ApplicationDate,'yyyy-MM')", r => (r.Str("M"), Convert.ToDecimal(r["T"])));
                    foreach (var (m, t) in lrows) loans[m] = t;
                }
                catch { /* loans table optional for the chart */ }

                var today = DateTime.Today;
                for (int i = 5; i >= 0; i--)
                {
                    var month = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                    var key = month.ToString("yyyy-MM");
                    d.Months.Add(new MonthPoint(month.ToString("MMM"), contrib.GetValueOrDefault(key), loans.GetValueOrDefault(key)));
                }
            });

            await Try("Demographics", async () =>
            {
                StatusSlice Slice(DbDataReader r) => new(r.Str("L"), r.Int("N"));
                d.Genders = await conn.QueryAsync("SELECT ISNULL(NULLIF(LTRIM(RTRIM(Gender)),''),'Unspecified') AS L, COUNT(*) AS N FROM Members GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(Gender)),''),'Unspecified') ORDER BY N DESC", Slice);
                d.Marital = await conn.QueryAsync("SELECT ISNULL(NULLIF(LTRIM(RTRIM(MaritalStatus)),''),'Unspecified') AS L, COUNT(*) AS N FROM Members GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(MaritalStatus)),''),'Unspecified') ORDER BY N DESC", Slice);
                d.MemberStatuses = await conn.QueryAsync("SELECT ISNULL(NULLIF(Status,''),'Unspecified') AS L, COUNT(*) AS N FROM Members GROUP BY ISNULL(NULLIF(Status,''),'Unspecified') ORDER BY N DESC", Slice);
                d.WithDob = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members WHERE DateOfBirth IS NOT NULL");
                d.WithGender = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members WHERE NULLIF(LTRIM(RTRIM(Gender)),'') IS NOT NULL");
                d.YouthCount = await conn.ScalarAsync<int>("SELECT COUNT(*) FROM Members WHERE DateOfBirth IS NOT NULL AND DATEDIFF(YEAR, DateOfBirth, GETDATE()) BETWEEN 18 AND 35");
                var avg = await conn.ScalarAsync<int?>("SELECT AVG(DATEDIFF(YEAR, DateOfBirth, GETDATE())) FROM Members WHERE DateOfBirth IS NOT NULL AND DateOfBirth < GETDATE()");
                d.AverageAge = avg;
                d.AgeBands = await conn.QueryAsync(@"SELECT B AS L, COUNT(*) AS N, MIN(K) AS K FROM (
                        SELECT CASE WHEN DateOfBirth IS NULL THEN 'Unknown'
                                    WHEN A < 18 THEN 'Under 18' WHEN A <= 25 THEN '18–25' WHEN A <= 35 THEN '26–35' WHEN A <= 45 THEN '36–45'
                                    WHEN A <= 55 THEN '46–55' WHEN A <= 65 THEN '56–65' ELSE '65+' END AS B,
                               CASE WHEN DateOfBirth IS NULL THEN 99 WHEN A < 18 THEN 0 WHEN A <= 25 THEN 1 WHEN A <= 35 THEN 2 WHEN A <= 45 THEN 3
                                    WHEN A <= 55 THEN 4 WHEN A <= 65 THEN 5 ELSE 6 END AS K
                        FROM (SELECT DateOfBirth, DATEDIFF(YEAR, DateOfBirth, GETDATE()) AS A FROM Members) x) y
                    GROUP BY B ORDER BY MIN(K)", Slice);
                d.Tenure = await conn.QueryAsync(@"SELECT B AS L, COUNT(*) AS N FROM (
                        SELECT CASE WHEN M < 12 THEN 'Under 1 year' WHEN M < 36 THEN '1–3 years' WHEN M < 60 THEN '3–5 years' ELSE '5+ years' END AS B,
                               CASE WHEN M < 12 THEN 0 WHEN M < 36 THEN 1 WHEN M < 60 THEN 2 ELSE 3 END AS K
                        FROM (SELECT DATEDIFF(MONTH, COALESCE(JoinDate, CreatedDate), GETDATE()) AS M FROM Members WHERE COALESCE(JoinDate, CreatedDate) IS NOT NULL) x) y
                    GROUP BY B, K ORDER BY K", Slice);
                try
                {
                    d.Occupations = await conn.QueryAsync("SELECT TOP 6 LTRIM(RTRIM(Occupation)) AS L, COUNT(*) AS N FROM Members WHERE NULLIF(LTRIM(RTRIM(Occupation)),'') IS NOT NULL GROUP BY LTRIM(RTRIM(Occupation)) ORDER BY N DESC", Slice);
                }
                catch { /* occupation column optional */ }

                var joins = new Dictionary<string, int>();
                var jr = await conn.QueryAsync(@"SELECT FORMAT(COALESCE(JoinDate, CreatedDate),'yyyy-MM') AS M, COUNT(*) AS N FROM Members
                        WHERE COALESCE(JoinDate, CreatedDate) >= DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
                        GROUP BY FORMAT(COALESCE(JoinDate, CreatedDate),'yyyy-MM')", r => (r.Str("M"), r.Int("N")));
                foreach (var (m, n) in jr) joins[m] = n;
                var today = DateTime.Today;
                for (int i = 11; i >= 0; i--)
                {
                    var month = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                    d.Joins.Add(new MonthPoint(month.ToString("MMM"), joins.GetValueOrDefault(month.ToString("yyyy-MM")), 0));
                }
            });

            await Try("Engagement", async () =>
            {
                d.Participants90d = await conn.ScalarAsync<int>($"SELECT COUNT(DISTINCT MemberId) FROM Contributions WHERE {ContribOk} AND DateContributed >= DATEADD(DAY, -90, GETDATE())");
                d.Borrowers = await conn.ScalarAsync<int>("SELECT COUNT(DISTINCT MemberId) FROM Loans WHERE Status IN ('Approved','Disbursed','Active')");
                d.ContribTypes = await conn.QueryAsync($"SELECT TOP 5 ISNULL(NULLIF(ContributionType,''),'Other') AS L, SUM(Amount) AS T FROM Contributions WHERE {ContribOk} GROUP BY ISNULL(NULLIF(ContributionType,''),'Other') ORDER BY T DESC",
                    r => (r.Str("L"), Convert.ToDecimal(r["T"])));
            });

            await Try("Activity", async () =>
            {
                var items = new List<ActivityItem>();
                items.AddRange(await conn.QueryAsync($@"SELECT TOP 6 c.Amount, c.ContributionType, c.DateContributed,
                        LTRIM(RTRIM(ISNULL(m.FirstName,'') + ' ' + ISNULL(m.LastName,''))) AS MemberName
                    FROM Contributions c LEFT JOIN Members m ON m.MemberId = c.MemberId
                    WHERE {ContribOk.Replace("Status", "c.Status")} ORDER BY c.DateContributed DESC, c.ContributionId DESC",
                    r => new ActivityItem("contribution", r.Str("MemberName"), r.Str("ContributionType") + " contribution", Convert.ToDecimal(r["Amount"]), r.Date("DateContributed"))));
                try
                {
                    items.AddRange(await conn.QueryAsync(@"SELECT TOP 4 l.PrincipalAmount, l.LoanType, l.ApplicationDate, l.Status,
                            LTRIM(RTRIM(ISNULL(m.FirstName,'') + ' ' + ISNULL(m.LastName,''))) AS MemberName
                        FROM Loans l LEFT JOIN Members m ON m.MemberId = l.MemberId ORDER BY l.ApplicationDate DESC",
                        r => new ActivityItem("loan", r.Str("MemberName"), r.Str("LoanType") + " loan · " + r.Str("Status"), Convert.ToDecimal(r["PrincipalAmount"]), r.Date("ApplicationDate"))));
                }
                catch { }
                d.Activity = items.OrderByDescending(i => i.When).Take(8).ToList();
            });
        }
        return d;
    }
}
