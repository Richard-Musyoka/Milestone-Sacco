using System.Collections.Generic;

public class ReportsModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReportType { get; set; }

    // Financial Summary
    public int ActiveMembers { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalSavings { get; set; }
    public decimal TotalLoansIssued { get; set; }
    public decimal TotalOutstandingLoans { get; set; }
    public decimal TotalDividendsPaid { get; set; }

    // Chart Data
    public List<ChartData> ContributionsData { get; set; }
    public List<ChartData> SavingsData { get; set; }
    public List<ChartData> LoansData { get; set; }
    public List<ChartData> DividendsData { get; set; }
    public List<ChartData> LineChartData { get; set; }
    public List<KeyValuePair<string, decimal>> PieChartData { get; set; }

    // Loans Report
    public List<LoanStatusData> LoanStatusData { get; set; }
    public List<LoanPerformanceData> LoanPerformanceData { get; set; }

    // Dividends Report
    public List<DividendSummaryData> DividendSummary { get; set; }
    public List<KeyValuePair<string, decimal>> TopDividendRecipients { get; set; }

    // Savings Report
    public List<KeyValuePair<string, decimal>> SavingsByType { get; set; }

    // Membership Report
    public List<KeyValuePair<string, int>> MemberStatusData { get; set; }
    public List<KeyValuePair<string, int>> MemberGenderData { get; set; }
}

public class ChartData
{
    public string Label { get; set; }
    public decimal Value { get; set; }
    public string SeriesLabel { get; set; }
}

public class LoanStatusData
{
    public string Status { get; set; }
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class LoanPerformanceData
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int LoanCount { get; set; }
    public decimal TotalIssued { get; set; }
    public decimal DefaultedAmount { get; set; }
    public decimal PaidAmount { get; set; }
}

public class DividendSummaryData
{
    public string FinancialYear { get; set; }
    public decimal TotalAmount { get; set; }
    public int MemberCount { get; set; }
}