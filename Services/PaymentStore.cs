using System.Data.Common;
using System.Text.RegularExpressions;
using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

public sealed record Payment(long Id, string ReceiptNo, int? MemberId, string MemberName, string Channel, string Reference, string Phone, decimal Amount,
    string Purpose, int? TargetId, string PostedTo, DateTime PaidOn, string Status, string Notes, string RecordedBy, DateTime CreatedUtc, string ReverseReason);

public sealed class PaymentInput
{
    public int? MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string Channel { get; set; } = "M-Pesa";
    public string Reference { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Amount { get; set; }
    public string Purpose { get; set; } = "Deposits";
    public int? TargetId { get; set; }
    public DateTime PaidOn { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
}

public sealed record PaymentSummary(decimal Today, decimal Month, decimal Year, int CountMonth, Dictionary<string, decimal> ByChannel, Dictionary<string, decimal> ByPurpose, List<(DateTime Day, decimal Amount)> Last30);

/// <summary>Money received from members, and where it was posted (deposits, loan installments, shares, chama).</summary>
public sealed class PaymentStore
{
    public static readonly string[] Channels = { "M-Pesa", "Bank", "Cash", "Check-off", "Cheque" };
    public static readonly string[] Purposes = { "Deposits", "Loan repayment", "Share capital", "Entrance fee", "Chama", "Other" };
    private static readonly Regex MpesaCode = new("^[A-Z0-9]{10}$", RegexOptions.Compiled);

    private readonly IDbFactory _db;
    private readonly ILogger<PaymentStore> _log;
    public PaymentStore(IDbFactory db, ILogger<PaymentStore> log) { _db = db; _log = log; }

    public async Task EnsureSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"IF OBJECT_ID('dbo.Payments') IS NULL
            CREATE TABLE dbo.Payments (Id BIGINT IDENTITY(1,1) PRIMARY KEY, ReceiptNo NVARCHAR(30) NOT NULL, MemberId INT NULL, MemberName NVARCHAR(160) NULL,
              Channel NVARCHAR(20) NOT NULL, Reference NVARCHAR(60) NULL, Phone NVARCHAR(30) NULL, Amount DECIMAL(18,2) NOT NULL, Purpose NVARCHAR(30) NOT NULL,
              TargetId INT NULL, PostedTo NVARCHAR(300) NULL, AllocTable NVARCHAR(40) NULL, AllocIds NVARCHAR(400) NULL, PaidOn DATETIME2 NOT NULL,
              Status NVARCHAR(20) NOT NULL DEFAULT 'Posted', Notes NVARCHAR(500) NULL, RecordedBy NVARCHAR(200) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
              ReversedBy NVARCHAR(200) NULL, ReversedUtc DATETIME2 NULL, ReverseReason NVARCHAR(300) NULL);
          IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Reference')
            CREATE INDEX IX_Payments_Reference ON dbo.Payments (Reference);
          IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Member')
            CREATE INDEX IX_Payments_Member ON dbo.Payments (MemberId, PaidOn);");
    }

    private static Payment Map(DbDataReader r) => new(r.Long("Id"), r.Str("ReceiptNo"), r.IsDBNull(r.GetOrdinal("MemberId")) ? null : r.Int("MemberId"), r.Str("MemberName"),
        r.Str("Channel"), r.Str("Reference"), r.Str("Phone"), Convert.ToDecimal(r["Amount"]), r.Str("Purpose"), r.IsDBNull(r.GetOrdinal("TargetId")) ? null : r.Int("TargetId"),
        r.Str("PostedTo"), r.Date("PaidOn"), r.Str("Status"), r.Str("Notes"), r.Str("RecordedBy"), r.Date("CreatedUtc"), r.Str("ReverseReason"));

    public async Task<List<Payment>> ListAsync(string? q = null, string? channel = null, string? purpose = null, int? memberId = null, DateTime? from = null, DateTime? to = null, int take = 500)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT TOP (@t) * FROM dbo.Payments
            WHERE (@m IS NULL OR MemberId = @m) AND (@ch IS NULL OR Channel = @ch) AND (@pu IS NULL OR Purpose = @pu)
              AND (@f IS NULL OR PaidOn >= @f) AND (@to IS NULL OR PaidOn < DATEADD(day, 1, @to))
              AND (@q IS NULL OR ReceiptNo LIKE @ql OR Reference LIKE @ql OR MemberName LIKE @ql OR Phone LIKE @ql)
            ORDER BY PaidOn DESC, Id DESC", Map,
            ("t", take), ("m", memberId), ("ch", Nz(channel)), ("pu", Nz(purpose)), ("f", from), ("to", to), ("q", Nz(q)), ("ql", "%" + (q ?? "").Trim() + "%"));
    }

    public async Task<Payment?> GetAsync(long id)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync("SELECT * FROM dbo.Payments WHERE Id = @id", Map, ("id", id));
    }

    public async Task<PaymentSummary> SummaryAsync()
    {
        await using var c = await _db.OpenAsync();
        var today = DateTime.Today; var m0 = new DateTime(today.Year, today.Month, 1); var y0 = new DateTime(today.Year, 1, 1);
        var t = await c.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d", ("d", today)) ?? 0;
        var m = await c.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d", ("d", m0)) ?? 0;
        var y = await c.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d", ("d", y0)) ?? 0;
        var n = await c.ScalarAsync<int?>("SELECT COUNT(*) FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d", ("d", m0)) ?? 0;
        var ch = (await c.QueryAsync("SELECT Channel, SUM(Amount) AS A FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d GROUP BY Channel", r => (r.Str("Channel"), Convert.ToDecimal(r["A"])), ("d", m0))).ToDictionary(x => x.Item1, x => x.Item2);
        var pu = (await c.QueryAsync("SELECT Purpose, SUM(Amount) AS A FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d GROUP BY Purpose", r => (r.Str("Purpose"), Convert.ToDecimal(r["A"])), ("d", m0))).ToDictionary(x => x.Item1, x => x.Item2);
        var days = await c.QueryAsync("SELECT CAST(PaidOn AS DATE) AS D, SUM(Amount) AS A FROM dbo.Payments WHERE Status <> 'Reversed' AND PaidOn >= @d GROUP BY CAST(PaidOn AS DATE)",
            r => (Convert.ToDateTime(r["D"]).Date, Convert.ToDecimal(r["A"])), ("d", today.AddDays(-29)));
        var map = days.ToDictionary(x => x.Item1, x => x.Item2);
        var last = Enumerable.Range(0, 30).Select(i => today.AddDays(-29 + i)).Select(d => (d, map.TryGetValue(d, out var v) ? v : 0m)).ToList();
        return new PaymentSummary(t, m, y, n, ch, pu, last);
    }

    private static object? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string? Validate(PaymentInput p)
    {
        p.Reference = (p.Reference ?? "").Trim().ToUpperInvariant(); p.Notes = (p.Notes ?? "").Trim(); p.Phone = (p.Phone ?? "").Trim();
        if (p.Amount <= 0) return "Enter the amount received.";
        if (p.Amount > 100_000_000) return "That amount looks too large. Check it.";
        if (!Channels.Contains(p.Channel)) return "Pick how the money came in.";
        if (!Purposes.Contains(p.Purpose)) return "Pick what the payment is for.";
        if (p.Purpose != "Other" && (p.MemberId is null or <= 0)) return "Pick the member who paid.";
        if (p.Channel == "M-Pesa" && !MpesaCode.IsMatch(p.Reference)) return "M-Pesa codes are 10 letters and numbers, like SJK4H7Q2LM.";
        if (p.Channel is "Bank" or "Cheque" && p.Reference.Length < 3) return "Add the bank or cheque reference.";
        if (p.Purpose is "Loan repayment" or "Chama" && p.TargetId is null or <= 0) return p.Purpose == "Chama" ? "Pick the chama." : "Pick the loan being repaid.";
        if (p.PaidOn > DateTime.Now.AddMinutes(5)) return "The payment date can't be in the future.";
        if (p.Notes.Length > 500) return "Notes are too long.";
        return null;
    }

    /// <summary>Records the payment and posts it. Everything happens in one transaction.</summary>
    public async Task<(Payment? Saved, string? Error)> RecordAsync(PaymentInput p, string? by, decimal sharePrice)
    {
        var err = Validate(p);
        if (err != null) return (null, err);
        await using var c = await _db.OpenAsync();
        if (p.Reference.Length > 0)
        {
            var dup = await c.ScalarAsync<string>("SELECT TOP 1 ReceiptNo FROM dbo.Payments WHERE Reference = @r AND Channel = @ch AND Status <> 'Reversed'", ("r", p.Reference), ("ch", p.Channel));
            if (!string.IsNullOrEmpty(dup)) return (null, $"Reference {p.Reference} was already received as {dup}.");
        }

        await using var tx = await c.BeginTransactionAsync();
        async Task<T?> Scalar<T>(string sql, params (string, object?)[] ps) { await using var cmd = Cmd(c, tx, sql, ps); var o = await cmd.ExecuteScalarAsync(); return o == null || o is DBNull ? default : (T)Convert.ChangeType(o, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)); }
        async Task<int> Exec(string sql, params (string, object?)[] ps) { await using var cmd = Cmd(c, tx, sql, ps); return await cmd.ExecuteNonQueryAsync(); }

        try
        {
            var year = p.PaidOn.Year;
            var seq = (await Scalar<int?>("SELECT COUNT(*) FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK) WHERE YEAR(PaidOn) = @y", ("y", year)) ?? 0) + 1;
            var receipt = $"RCT-{year}-{seq:D5}";
            string postedTo = "", allocTable = "", allocIds = "";
            var remark = "Receipt " + receipt + (p.Reference.Length > 0 ? " · " + p.Channel + " " + p.Reference : "");

            switch (p.Purpose)
            {
                case "Deposits":
                {
                    var id = await Scalar<int?>(@"INSERT INTO Contributions (MemberId, ContributionType, Amount, DateContributed, PaymentMethod, TransactionRef, Status, Remarks, CreatedDate)
                        VALUES (@m, 'Monthly', @a, @d, @pm, @ref, 'Confirmed', @rem, GETDATE()); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        ("m", p.MemberId), ("a", p.Amount), ("d", p.PaidOn), ("pm", p.Channel == "Bank" ? "Bank Transfer" : p.Channel), ("ref", p.Reference.Length > 0 ? p.Reference : receipt), ("rem", remark));
                    postedTo = "Deposits (contribution CT-" + (id ?? 0).ToString("D3") + ")"; allocTable = "Contributions"; allocIds = (id ?? 0).ToString();
                    break;
                }
                case "Share capital":
                {
                    var price = sharePrice <= 0 ? 100 : sharePrice;
                    var units = (int)Math.Floor(p.Amount / price);
                    if (units < 1) { await tx.RollbackAsync(); return (null, $"Share capital is bought in units of {price:N0}. This amount buys none."); }
                    var id = await Scalar<int?>(@"INSERT INTO Shares (MemberId, Units, UnitPrice, PurchaseDate, Status, ShareType, Remarks, CreatedDate)
                        VALUES (@m, @u, @pr, @d, 'Active', 'Ordinary', @rem, GETDATE()); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        ("m", p.MemberId), ("u", units), ("pr", price), ("d", p.PaidOn), ("rem", remark));
                    var left = p.Amount - units * price;
                    postedTo = $"{units:N0} shares at {price:N0}" + (left > 0 ? $" ({left:N0} left over, refund or add to deposits)" : "");
                    allocTable = "Shares"; allocIds = (id ?? 0).ToString();
                    break;
                }
                case "Loan repayment":
                {
                    var owner = await Scalar<int?>("SELECT MemberId FROM Loans WHERE LoanId = @l", ("l", p.TargetId));
                    if (owner == null) { await tx.RollbackAsync(); return (null, "That loan doesn't exist."); }
                    if (owner != p.MemberId) { await tx.RollbackAsync(); return (null, "That loan belongs to a different member."); }
                    var due = new List<(int Id, decimal Amt)>();
                    await using (var cmd = Cmd(c, tx, "SELECT InstallmentId, TotalAmount FROM LoanInstallments WHERE LoanId = @l AND ISNULL(Status,'') <> 'Paid' ORDER BY DueDate", ("l", p.TargetId)))
                    await using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync()) due.Add((r.GetInt32(0), Convert.ToDecimal(r[1])));
                    var left = p.Amount; var paid = new List<int>(); decimal applied = 0;
                    foreach (var (id, amt) in due)
                    {
                        if (left + 0.005m < amt) break;
                        await Exec("UPDATE LoanInstallments SET Status = 'Paid', PaymentDate = @d WHERE InstallmentId = @i", ("d", p.PaidOn), ("i", id));
                        paid.Add(id); left -= amt; applied += amt;
                    }
                    if (applied > 0)
                        await Exec(@"UPDATE Loans SET OutstandingBalance = CASE WHEN ISNULL(OutstandingBalance,0) - @a < 0 THEN 0 ELSE ISNULL(OutstandingBalance,0) - @a END,
                                     Status = CASE WHEN ISNULL(OutstandingBalance,0) - @a <= 0 THEN 'Completed' ELSE Status END WHERE LoanId = @l", ("a", applied), ("l", p.TargetId));
                    postedTo = paid.Count == 0 ? $"Loan LN-{p.TargetId:D3}: held as credit (less than one installment)" : $"Loan LN-{p.TargetId:D3}: {paid.Count} installment(s) cleared" + (left > 0 ? $", {left:N0} held as credit" : "");
                    allocTable = "LoanInstallments"; allocIds = string.Join(",", paid) + "|" + applied.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }
                case "Chama":
                {
                    var cycle = await Scalar<int?>("SELECT CurrentCycle FROM dbo.Chamas WHERE Id = @c", ("c", p.TargetId));
                    if (cycle == null) { await tx.RollbackAsync(); return (null, "That chama doesn't exist."); }
                    var inChama = await Scalar<int?>("SELECT COUNT(*) FROM dbo.ChamaMembers WHERE ChamaId = @c AND MemberId = @m AND Status = 'Active'", ("c", p.TargetId), ("m", p.MemberId));
                    if ((inChama ?? 0) == 0) { await tx.RollbackAsync(); return (null, "This member isn't in that chama."); }
                    var id = await Scalar<long?>(@"INSERT INTO dbo.ChamaContributions (ChamaId, MemberId, CycleNo, Amount, PaidOn, Reference, RecordedBy)
                        VALUES (@c, @m, @cy, @a, @d, @r, @by); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                        ("c", p.TargetId), ("m", p.MemberId), ("cy", Math.Max(1, cycle.Value)), ("a", p.Amount), ("d", p.PaidOn), ("r", receipt), ("by", by ?? ""));
                    var name = await Scalar<string>("SELECT Name FROM dbo.Chamas WHERE Id = @c", ("c", p.TargetId));
                    postedTo = $"{name}: round {Math.Max(1, cycle.Value)} contribution"; allocTable = "ChamaContributions"; allocIds = (id ?? 0).ToString();
                    break;
                }
                case "Entrance fee": postedTo = "Entrance fee (income)"; break;
                default: postedTo = "Not posted (other income)"; break;
            }

            var newId = await Scalar<long?>(@"INSERT INTO dbo.Payments (ReceiptNo, MemberId, MemberName, Channel, Reference, Phone, Amount, Purpose, TargetId, PostedTo, AllocTable, AllocIds, PaidOn, Status, Notes, RecordedBy)
                VALUES (@rc, @m, @mn, @ch, @r, @ph, @a, @pu, @t, @po, @at, @ai, @d, 'Posted', @n, @by); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                ("rc", receipt), ("m", p.MemberId), ("mn", p.MemberName), ("ch", p.Channel), ("r", p.Reference), ("ph", p.Phone), ("a", p.Amount), ("pu", p.Purpose),
                ("t", p.TargetId), ("po", postedTo), ("at", allocTable), ("ai", allocIds), ("d", p.PaidOn), ("n", p.Notes), ("by", by ?? ""));
            await tx.CommitAsync();
            return (await GetAsync(newId ?? 0), null);
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(); } catch { }
            _log.LogError(ex, "Payment failed");
            return (null, "Couldn't record the payment: " + ex.Message);
        }
    }

    /// <summary>Reverses a payment and undoes what it posted.</summary>
    public async Task<string?> ReverseAsync(long id, string reason, string? by)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < 3) return "Say why the payment is being reversed.";
        await using var c = await _db.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        async Task<int> Exec(string sql, params (string, object?)[] ps) { await using var cmd = Cmd(c, tx, sql, ps); return await cmd.ExecuteNonQueryAsync(); }
        try
        {
            string status = "", table = "", ids = ""; int? target = null;
            await using (var cmd = Cmd(c, tx, "SELECT Status, AllocTable, AllocIds, TargetId FROM dbo.Payments WITH (UPDLOCK) WHERE Id = @id", ("id", id)))
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                if (!await r.ReadAsync()) return "Payment not found.";
                status = r.Str("Status"); table = r.Str("AllocTable"); ids = r.Str("AllocIds"); target = r.IsDBNull(3) ? null : r.GetInt32(3);
            }
            if (status == "Reversed") return "This payment is already reversed.";
            switch (table)
            {
                case "Contributions": await Exec("UPDATE Contributions SET Status = 'Cancelled', Remarks = CONCAT(Remarks, ' · reversed') WHERE ContributionId = @i", ("i", int.Parse(ids))); break;
                case "Shares": await Exec("UPDATE Shares SET Status = 'Cancelled', Remarks = CONCAT(Remarks, ' · reversed') WHERE ShareId = @i", ("i", int.Parse(ids))); break;
                case "ChamaContributions": await Exec("DELETE FROM dbo.ChamaContributions WHERE Id = @i", ("i", long.Parse(ids))); break;
                case "LoanInstallments":
                {
                    var parts = ids.Split('|');
                    var list = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                    var applied = parts.Length > 1 ? decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    foreach (var i in list) await Exec("UPDATE LoanInstallments SET Status = 'Pending', PaymentDate = NULL WHERE InstallmentId = @i", ("i", i));
                    if (applied > 0) await Exec("UPDATE Loans SET OutstandingBalance = ISNULL(OutstandingBalance,0) + @a, Status = CASE WHEN Status = 'Completed' THEN 'Disbursed' ELSE Status END WHERE LoanId = @l", ("a", applied), ("l", target));
                    break;
                }
            }
            await Exec("UPDATE dbo.Payments SET Status = 'Reversed', ReversedBy = @b, ReversedUtc = SYSUTCDATETIME(), ReverseReason = @r WHERE Id = @id", ("b", by ?? ""), ("r", reason), ("id", id));
            await tx.CommitAsync();
            return null;
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(); } catch { }
            return "Couldn't reverse: " + ex.Message;
        }
    }

    private static DbCommand Cmd(DbConnection c, DbTransaction tx, string sql, params (string Name, object? Value)[] ps)
    {
        var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (n, v) in ps) { var prm = cmd.CreateParameter(); prm.ParameterName = "@" + n; prm.Value = v ?? DBNull.Value; cmd.Parameters.Add(prm); }
        return cmd;
    }
}
