using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using SaccoManagementSystem.Models;
using System.Data;
using System.Globalization;

public class ReportsController : Controller
{
    private readonly string _connectionString;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IConfiguration configuration, ILogger<ReportsController> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var model = new ReportsModel
        {
            StartDate = DateTime.Now.AddMonths(-6).Date,
            EndDate = DateTime.Now.Date,
            ReportType = "FinancialSummary"
        };

        await PopulateReportTypes();
        await LoadReportData(model);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateReport(ReportsModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateReportTypes();
            return View("Index", model);
        }

        await PopulateReportTypes();
        await LoadReportData(model);

        return View("Index", model);
    }

    private async Task PopulateReportTypes()
    {
        var reportTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "FinancialSummary", Text = "Financial Summary" },
            new SelectListItem { Value = "Contributions", Text = "Contributions Analysis" },
            new SelectListItem { Value = "Loans", Text = "Loans Performance" },
            new SelectListItem { Value = "Dividends", Text = "Dividends Distribution" },
            new SelectListItem { Value = "Savings", Text = "Savings Trends" },
            new SelectListItem { Value = "Membership", Text = "Membership Statistics" }
        };

        ViewBag.ReportTypes = reportTypes;
    }

    private async Task LoadReportData(ReportsModel model)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            switch (model.ReportType)
            {
                case "FinancialSummary":
                    await LoadFinancialSummary(connection, model);
                    break;
                case "Contributions":
                    await LoadContributionsReport(connection, model);
                    break;
                case "Loans":
                    await LoadLoansReport(connection, model);
                    break;
                case "Dividends":
                    await LoadDividendsReport(connection, model);
                    break;
                case "Savings":
                    await LoadSavingsReport(connection, model);
                    break;
                case "Membership":
                    await LoadMembershipReport(connection, model);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report data");
            ModelState.AddModelError("", "An error occurred while loading report data.");
        }
    }

    private async Task LoadFinancialSummary(SqlConnection connection, ReportsModel model)
    {
        // Total Contributions
        var contributionsQuery = @"
            SELECT SUM(Amount) AS Total, 
                   DATEPART(MONTH, DateContributed) AS Month,
                   DATEPART(YEAR, DateContributed) AS Year
            FROM Contributions
            WHERE DateContributed BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, DateContributed), DATEPART(MONTH, DateContributed)
            ORDER BY Year, Month";

        model.ContributionsData = await GetChartData(connection, contributionsQuery, "Contributions");

        // Total Savings
        var savingsQuery = @"
            SELECT SUM(Amount) AS Total, 
                   DATEPART(MONTH, TransactionDate) AS Month,
                   DATEPART(YEAR, TransactionDate) AS Year
            FROM Savings
            WHERE TransactionDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, TransactionDate), DATEPART(MONTH, TransactionDate)
            ORDER BY Year, Month";

        model.SavingsData = await GetChartData(connection, savingsQuery, "Savings");

        // Total Loans
        var loansQuery = @"
            SELECT SUM(PrincipalAmount) AS Total, 
                   DATEPART(MONTH, ApplicationDate) AS Month,
                   DATEPART(YEAR, ApplicationDate) AS Year
            FROM Loans
            WHERE ApplicationDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, ApplicationDate), DATEPART(MONTH, ApplicationDate)
            ORDER BY Year, Month";

        model.LoansData = await GetChartData(connection, loansQuery, "Loans");

        // Total Dividends
        var dividendsQuery = @"
            SELECT SUM(Amount) AS Total, 
                   DATEPART(MONTH, PaymentDate) AS Month,
                   DATEPART(YEAR, PaymentDate) AS Year
            FROM Dividends
            WHERE PaymentDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, PaymentDate), DATEPART(MONTH, PaymentDate)
            ORDER BY Year, Month";

        model.DividendsData = await GetChartData(connection, dividendsQuery, "Dividends");

        // Key Metrics
        var metricsQuery = @"
            SELECT 
                (SELECT COUNT(*) FROM Members WHERE Status = 'Active') AS ActiveMembers,
                (SELECT SUM(Amount) FROM Contributions WHERE DateContributed BETWEEN @StartDate AND @EndDate) AS TotalContributions,
                (SELECT SUM(Amount) FROM Savings WHERE TransactionDate BETWEEN @StartDate AND @EndDate) AS TotalSavings,
                (SELECT SUM(PrincipalAmount) FROM Loans WHERE ApplicationDate BETWEEN @StartDate AND @EndDate) AS TotalLoansIssued,
                (SELECT SUM(OutstandingBalance) FROM Loans WHERE Status = 'Active') AS TotalOutstandingLoans,
                (SELECT SUM(Amount) FROM Dividends WHERE PaymentDate BETWEEN @StartDate AND @EndDate) AS TotalDividendsPaid";

        using var command = new SqlCommand(metricsQuery, connection);
        command.Parameters.AddWithValue("@StartDate", model.StartDate);
        command.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            model.ActiveMembers = reader.GetInt32(0);
            model.TotalContributions = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
            model.TotalSavings = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
            model.TotalLoansIssued = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
            model.TotalOutstandingLoans = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
            model.TotalDividendsPaid = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
        }
    }

    private async Task LoadContributionsReport(SqlConnection connection, ReportsModel model)
    {
        // Contributions by type
        var query = @"
            SELECT ContributionType, SUM(Amount) AS Total
            FROM Contributions
            WHERE DateContributed BETWEEN @StartDate AND @EndDate
            GROUP BY ContributionType
            ORDER BY Total DESC";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@StartDate", model.StartDate);
        command.Parameters.AddWithValue("@EndDate", model.EndDate);

        var contributionsByType = new List<KeyValuePair<string, decimal>>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contributionsByType.Add(new KeyValuePair<string, decimal>(
                reader.GetString(0),
                reader.GetDecimal(1)
            ));
        }

        model.PieChartData = contributionsByType;

        // Monthly contributions trend
        var trendQuery = @"
            SELECT SUM(Amount) AS Total, 
                   DATEPART(MONTH, DateContributed) AS Month,
                   DATEPART(YEAR, DateContributed) AS Year
            FROM Contributions
            WHERE DateContributed BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, DateContributed), DATEPART(MONTH, DateContributed)
            ORDER BY Year, Month";

        model.LineChartData = await GetChartData(connection, trendQuery, "Contributions");
    }

    private async Task LoadLoansReport(SqlConnection connection, ReportsModel model)
    {
        // Loans by status
        var statusQuery = @"
            SELECT Status, COUNT(*) AS Count, SUM(PrincipalAmount) AS TotalAmount
            FROM Loans
            WHERE ApplicationDate BETWEEN @StartDate AND @EndDate
            GROUP BY Status";

        model.LoanStatusData = new List<LoanStatusData>();
        using var statusCommand = new SqlCommand(statusQuery, connection);
        statusCommand.Parameters.AddWithValue("@StartDate", model.StartDate);
        statusCommand.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var statusReader = await statusCommand.ExecuteReaderAsync();
        while (await statusReader.ReadAsync())
        {
            model.LoanStatusData.Add(new LoanStatusData
            {
                Status = statusReader.GetString(0),
                Count = statusReader.GetInt32(1),
                TotalAmount = statusReader.GetDecimal(2)
            });
        }

        // Loan performance
        var performanceQuery = @"
            SELECT 
                DATEPART(MONTH, ApplicationDate) AS Month,
                DATEPART(YEAR, ApplicationDate) AS Year,
                COUNT(*) AS LoanCount,
                SUM(PrincipalAmount) AS TotalIssued,
                SUM(CASE WHEN Status = 'Defaulted' THEN PrincipalAmount ELSE 0 END) AS DefaultedAmount,
                SUM(CASE WHEN Status = 'Paid' THEN PrincipalAmount ELSE 0 END) AS PaidAmount
            FROM Loans
            WHERE ApplicationDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, ApplicationDate), DATEPART(MONTH, ApplicationDate)
            ORDER BY Year, Month";

        model.LoanPerformanceData = new List<LoanPerformanceData>();
        using var perfCommand = new SqlCommand(performanceQuery, connection);
        perfCommand.Parameters.AddWithValue("@StartDate", model.StartDate);
        perfCommand.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var perfReader = await perfCommand.ExecuteReaderAsync();
        while (await perfReader.ReadAsync())
        {
            model.LoanPerformanceData.Add(new LoanPerformanceData
            {
                Month = perfReader.GetInt32(0),
                Year = perfReader.GetInt32(1),
                LoanCount = perfReader.GetInt32(2),
                TotalIssued = perfReader.GetDecimal(3),
                DefaultedAmount = perfReader.GetDecimal(4),
                PaidAmount = perfReader.GetDecimal(5)
            });
        }
    }

    private async Task LoadDividendsReport(SqlConnection connection, ReportsModel model)
    {
        // Dividends by year
        var query = @"
            SELECT FinancialYear, SUM(Amount) AS Total, COUNT(*) AS MemberCount
            FROM DividendPayments
            WHERE PaymentDate BETWEEN @StartDate AND @EndDate
            GROUP BY FinancialYear
            ORDER BY FinancialYear";

        model.DividendSummary = new List<DividendSummaryData>();
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@StartDate", model.StartDate);
        command.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            model.DividendSummary.Add(new DividendSummaryData
            {
                FinancialYear = reader.GetString(0),
                TotalAmount = reader.GetDecimal(1),
                MemberCount = reader.GetInt32(2)
            });
        }

        // Top dividend recipients
        var topRecipientsQuery = @"
            SELECT TOP 10 m.FirstName + ' ' + m.LastName AS MemberName, SUM(d.Amount) AS Total
            FROM DividendPayments d
            JOIN Members m ON d.MemberId = m.MemberId
            WHERE d.PaymentDate BETWEEN @StartDate AND @EndDate
            GROUP BY m.FirstName, m.LastName
            ORDER BY Total DESC";

        model.TopDividendRecipients = new List<KeyValuePair<string, decimal>>();
        using var topCommand = new SqlCommand(topRecipientsQuery, connection);
        topCommand.Parameters.AddWithValue("@StartDate", model.StartDate);
        topCommand.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var topReader = await topCommand.ExecuteReaderAsync();
        while (await topReader.ReadAsync())
        {
            model.TopDividendRecipients.Add(new KeyValuePair<string, decimal>(
                topReader.GetString(0),
                topReader.GetDecimal(1)
            ));
        }
    }

    private async Task LoadSavingsReport(SqlConnection connection, ReportsModel model)
    {
        // Savings by transaction type
        var typeQuery = @"
            SELECT TransactionType, SUM(Amount) AS Total
            FROM Savings
            WHERE TransactionDate BETWEEN @StartDate AND @EndDate
            GROUP BY TransactionType";

        model.SavingsByType = new List<KeyValuePair<string, decimal>>();
        using var typeCommand = new SqlCommand(typeQuery, connection);
        typeCommand.Parameters.AddWithValue("@StartDate", model.StartDate);
        typeCommand.Parameters.AddWithValue("@EndDate", model.EndDate);

        using var typeReader = await typeCommand.ExecuteReaderAsync();
        while (await typeReader.ReadAsync())
        {
            model.SavingsByType.Add(new KeyValuePair<string, decimal>(
                typeReader.GetString(0),
                typeReader.GetDecimal(1)
            ));
        }

        // Monthly savings trend
        var trendQuery = @"
            SELECT SUM(Amount) AS Total, 
                   DATEPART(MONTH, TransactionDate) AS Month,
                   DATEPART(YEAR, TransactionDate) AS Year
            FROM Savings
            WHERE TransactionDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, TransactionDate), DATEPART(MONTH, TransactionDate)
            ORDER BY Year, Month";

        model.LineChartData = await GetChartData(connection, trendQuery, "Savings");
    }

    private async Task LoadMembershipReport(SqlConnection connection, ReportsModel model)
    {
        // Membership by status
        var statusQuery = "SELECT Status, COUNT(*) AS Count FROM Members GROUP BY Status";
        model.MemberStatusData = new List<KeyValuePair<string, int>>();
        using var statusCommand = new SqlCommand(statusQuery, connection);
        using var statusReader = await statusCommand.ExecuteReaderAsync();
        while (await statusReader.ReadAsync())
        {
            model.MemberStatusData.Add(new KeyValuePair<string, int>(
                statusReader.GetString(0),
                statusReader.GetInt32(1)
            ));
        }

        // Membership growth
        var growthQuery = @"
            SELECT COUNT(*) AS Count, 
                   DATEPART(MONTH, JoinDate) AS Month,
                   DATEPART(YEAR, JoinDate) AS Year
            FROM Members
            WHERE JoinDate BETWEEN @StartDate AND @EndDate
            GROUP BY DATEPART(YEAR, JoinDate), DATEPART(MONTH, JoinDate)
            ORDER BY Year, Month";

        model.LineChartData = await GetChartData(connection, growthQuery, "New Members");

        // Membership by gender
        var genderQuery = "SELECT Gender, COUNT(*) AS Count FROM Members GROUP BY Gender";
        model.MemberGenderData = new List<KeyValuePair<string, int>>();
        using var genderCommand = new SqlCommand(genderQuery, connection);
        using var genderReader = await genderCommand.ExecuteReaderAsync();
        while (await genderReader.ReadAsync())
        {
            model.MemberGenderData.Add(new KeyValuePair<string, int>(
                genderReader.GetString(0),
                genderReader.GetInt32(1)
            ));
        }
    }

    private async Task<List<ChartData>> GetChartData(SqlConnection connection, string query, string label)
    {
        var chartData = new List<ChartData>();
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@StartDate", DateTime.Now.AddMonths(-6).Date);
        command.Parameters.AddWithValue("@EndDate", DateTime.Now.Date);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            decimal total = reader.GetDecimal(0);
            int month = reader.GetInt32(1);
            int year = reader.GetInt32(2);

            chartData.Add(new ChartData
            {
                Label = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month)} {year}",
                Value = total,
                SeriesLabel = label
            });
        }

        return chartData;
    }
}