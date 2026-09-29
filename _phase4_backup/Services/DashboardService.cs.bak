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
                d.LoanBook = await conn.ScalarAsync<decimal?>("SELECT SUM(ISNULL(OutstandingBalance, PrincipalAmount)) FROM Loans WHERE Status IN ('Approved','Disbursed','Active')") ?? 0;
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
