using SaccoManagementSystem.Security;

namespace SaccoManagementSystem.Services;

public sealed class Chama
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RegNo { get; set; } = "";
    public string Type { get; set; } = "Merry-go-round";       // Merry-go-round | Investment | Table banking | Welfare
    public decimal ContributionAmount { get; set; } = 1000;
    public string Frequency { get; set; } = "Monthly";           // Weekly | Fortnightly | Monthly
    public string MeetingDay { get; set; } = "Saturday";
    public string MeetingPlace { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.Today;
    public int CurrentCycle { get; set; } = 1;
    public string Status { get; set; } = "Active";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#f97316";
    // computed
    public int Members { get; set; }
    public decimal CollectedThisRound { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalPaidOut { get; set; }
    public string NextRecipient { get; set; } = "";
    public DateTime? NextPayout { get; set; }
    public bool Rotates => Type == "Merry-go-round";
    public decimal Pot => ContributionAmount * Members;
}

public sealed record ChamaMember(int Id, int ChamaId, int MemberId, string Name, string MemberNo, string Phone, string Role, int Position, DateTime JoinedOn, string Status);
public sealed record ChamaContribution(long Id, int MemberId, int CycleNo, decimal Amount, DateTime PaidOn, string Reference);
public sealed record ChamaPayout(int Id, int CycleNo, int MemberId, string Name, decimal Amount, DateTime DueDate, DateTime? PaidOn, string Reference, string Status);

/// <summary>Chamas (investment groups, table banking, welfare) and merry-go-round rotations.</summary>
public sealed class ChamaStore
{
    public static readonly string[] Types = { "Merry-go-round", "Investment", "Table banking", "Welfare" };
    public static readonly string[] Frequencies = { "Weekly", "Fortnightly", "Monthly" };
    public static readonly string[] Roles = { "Chairperson", "Secretary", "Treasurer", "Member" };
    private readonly IDbFactory _db;
    public ChamaStore(IDbFactory db) => _db = db;

    public async Task EnsureSchemaAsync()
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(@"IF OBJECT_ID('dbo.Chamas') IS NULL
            CREATE TABLE dbo.Chamas (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(120) NOT NULL, RegNo NVARCHAR(60) NULL, Type NVARCHAR(30) NOT NULL,
              ContributionAmount DECIMAL(18,2) NOT NULL, Frequency NVARCHAR(20) NOT NULL, MeetingDay NVARCHAR(20) NULL, MeetingPlace NVARCHAR(120) NULL,
              StartDate DATE NOT NULL, CurrentCycle INT NOT NULL DEFAULT 1, Status NVARCHAR(20) NOT NULL DEFAULT 'Active', Description NVARCHAR(600) NULL,
              Color NVARCHAR(9) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy NVARCHAR(200) NULL);
          IF OBJECT_ID('dbo.ChamaMembers') IS NULL
            CREATE TABLE dbo.ChamaMembers (Id INT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, MemberId INT NOT NULL, Role NVARCHAR(30) NOT NULL DEFAULT 'Member',
              Position INT NOT NULL DEFAULT 0, JoinedOn DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE), Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
              CONSTRAINT UQ_ChamaMember UNIQUE (ChamaId, MemberId));
          IF OBJECT_ID('dbo.ChamaContributions') IS NULL
            CREATE TABLE dbo.ChamaContributions (Id BIGINT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, MemberId INT NOT NULL, CycleNo INT NOT NULL,
              Amount DECIMAL(18,2) NOT NULL, PaidOn DATETIME2 NOT NULL, Reference NVARCHAR(60) NULL, RecordedBy NVARCHAR(200) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
          IF OBJECT_ID('dbo.ChamaPayouts') IS NULL
            CREATE TABLE dbo.ChamaPayouts (Id INT IDENTITY(1,1) PRIMARY KEY, ChamaId INT NOT NULL, CycleNo INT NOT NULL, MemberId INT NOT NULL, Amount DECIMAL(18,2) NOT NULL,
              DueDate DATE NOT NULL, PaidOn DATETIME2 NULL, Reference NVARCHAR(60) NULL, Status NVARCHAR(20) NOT NULL DEFAULT 'Scheduled', PaidBy NVARCHAR(200) NULL,
              CONSTRAINT UQ_ChamaCycle UNIQUE (ChamaId, CycleNo));");
    }

    private const string ChamaSelect = @"SELECT c.*,
        (SELECT COUNT(*) FROM dbo.ChamaMembers m WHERE m.ChamaId = c.Id AND m.Status = 'Active') AS MemberCount,
        (SELECT ISNULL(SUM(Amount),0) FROM dbo.ChamaContributions x WHERE x.ChamaId = c.Id AND x.CycleNo = c.CurrentCycle) AS RoundIn,
        (SELECT ISNULL(SUM(Amount),0) FROM dbo.ChamaContributions x WHERE x.ChamaId = c.Id) AS TotalIn,
        (SELECT ISNULL(SUM(Amount),0) FROM dbo.ChamaPayouts p WHERE p.ChamaId = c.Id AND p.Status = 'Paid') AS TotalOut,
        (SELECT TOP 1 CONCAT(mm.FirstName, ' ', mm.LastName) FROM dbo.ChamaPayouts p JOIN Members mm ON mm.MemberId = p.MemberId WHERE p.ChamaId = c.Id AND p.CycleNo = c.CurrentCycle) AS NextName,
        (SELECT TOP 1 p.DueDate FROM dbo.ChamaPayouts p WHERE p.ChamaId = c.Id AND p.CycleNo = c.CurrentCycle) AS NextDate
        FROM dbo.Chamas c";

    private static Chama MapChama(System.Data.Common.DbDataReader r) => new()
    {
        Id = r.Int("Id"), Name = r.Str("Name"), RegNo = r.Str("RegNo"), Type = r.Str("Type"), ContributionAmount = Convert.ToDecimal(r["ContributionAmount"]),
        Frequency = r.Str("Frequency"), MeetingDay = r.Str("MeetingDay"), MeetingPlace = r.Str("MeetingPlace"), StartDate = r.Date("StartDate"), CurrentCycle = r.Int("CurrentCycle"),
        Status = r.Str("Status"), Description = r.Str("Description"), Color = string.IsNullOrEmpty(r.Str("Color")) ? "#f97316" : r.Str("Color"),
        Members = r.Int("MemberCount"), CollectedThisRound = Convert.ToDecimal(r["RoundIn"]), TotalCollected = Convert.ToDecimal(r["TotalIn"]), TotalPaidOut = Convert.ToDecimal(r["TotalOut"]),
        NextRecipient = r.Str("NextName"), NextPayout = r.DateN("NextDate"),
    };

    public async Task<List<Chama>> ListAsync()
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(ChamaSelect + " ORDER BY CASE WHEN c.Status = 'Active' THEN 0 ELSE 1 END, c.Name", MapChama);
    }

    public async Task<Chama?> GetAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.FirstAsync(ChamaSelect + " WHERE c.Id = @id", MapChama, ("id", id));
    }

    public static string? Validate(Chama x)
    {
        x.Name = (x.Name ?? "").Trim(); x.RegNo = (x.RegNo ?? "").Trim(); x.MeetingPlace = (x.MeetingPlace ?? "").Trim(); x.Description = (x.Description ?? "").Trim();
        if (x.Name.Length < 3) return "Give the chama a name.";
        if (x.Name.Length > 120 || x.RegNo.Length > 60 || x.MeetingPlace.Length > 120 || x.Description.Length > 600) return "One of the fields is too long.";
        if (!Types.Contains(x.Type)) return "Pick the type of chama.";
        if (!Frequencies.Contains(x.Frequency)) return "Pick how often members contribute.";
        if (x.ContributionAmount <= 0 || x.ContributionAmount > 10_000_000) return "Enter the contribution each member makes per round.";
        if (x.Status is not ("Active" or "Paused" or "Closed")) x.Status = "Active";
        return null;
    }

    public async Task<(int Id, string? Error)> SaveAsync(Chama x, string? by)
    {
        var err = Validate(x);
        if (err != null) return (0, err);
        await using var c = await _db.OpenAsync();
        if (x.Id == 0)
        {
            var id = await c.ScalarAsync<int>(@"INSERT INTO dbo.Chamas (Name, RegNo, Type, ContributionAmount, Frequency, MeetingDay, MeetingPlace, StartDate, Status, Description, Color, CreatedBy)
                VALUES (@n, @r, @t, @a, @f, @d, @p, @s, @st, @de, @co, @by); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                ("n", x.Name), ("r", x.RegNo), ("t", x.Type), ("a", x.ContributionAmount), ("f", x.Frequency), ("d", x.MeetingDay), ("p", x.MeetingPlace), ("s", x.StartDate.Date),
                ("st", x.Status), ("de", x.Description), ("co", x.Color), ("by", by ?? ""));
            return (id, null);
        }
        await c.ExecAsync(@"UPDATE dbo.Chamas SET Name=@n, RegNo=@r, Type=@t, ContributionAmount=@a, Frequency=@f, MeetingDay=@d, MeetingPlace=@p, StartDate=@s, Status=@st, Description=@de, Color=@co WHERE Id=@id",
            ("n", x.Name), ("r", x.RegNo), ("t", x.Type), ("a", x.ContributionAmount), ("f", x.Frequency), ("d", x.MeetingDay), ("p", x.MeetingPlace), ("s", x.StartDate.Date),
            ("st", x.Status), ("de", x.Description), ("co", x.Color), ("id", x.Id));
        await c.ExecAsync("UPDATE dbo.ChamaPayouts SET Amount = @a WHERE ChamaId = @id AND Status = 'Scheduled'", ("a", x.ContributionAmount * await ActiveCount(c, x.Id)), ("id", x.Id));
        return (x.Id, null);
    }

    private static async Task<int> ActiveCount(System.Data.Common.DbConnection c, int id) => await c.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ChamaMembers WHERE ChamaId = @c AND Status = 'Active'", ("c", id));

    public async Task<List<ChamaMember>> MembersAsync(int chamaId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT cm.*, CONCAT(m.FirstName, ' ', m.LastName) AS FullName, m.MemberNo, m.PhoneNumber FROM dbo.ChamaMembers cm
            JOIN Members m ON m.MemberId = cm.MemberId WHERE cm.ChamaId = @c ORDER BY CASE WHEN cm.Status = 'Active' THEN 0 ELSE 1 END, cm.Position, cm.Id",
            r => new ChamaMember(r.Int("Id"), r.Int("ChamaId"), r.Int("MemberId"), r.Str("FullName"), r.Str("MemberNo"), r.Str("PhoneNumber"), r.Str("Role"), r.Int("Position"), r.Date("JoinedOn"), r.Str("Status")),
            ("c", chamaId));
    }

    public async Task<List<(int ChamaId, string Name, string Role, string Type)>> ForMemberAsync(int memberId)
    {
        try
        {
            await using var c = await _db.OpenAsync();
            return await c.QueryAsync("SELECT ch.Id, ch.Name, cm.Role, ch.Type FROM dbo.ChamaMembers cm JOIN dbo.Chamas ch ON ch.Id = cm.ChamaId WHERE cm.MemberId = @m AND cm.Status = 'Active'",
                r => (r.Int("Id"), r.Str("Name"), r.Str("Role"), r.Str("Type")), ("m", memberId));
        }
        catch { return new(); }
    }

    public async Task<string?> AddMemberAsync(int chamaId, int memberId, string role)
    {
        await using var c = await _db.OpenAsync();
        var exists = await c.ScalarAsync<string>("SELECT Status FROM dbo.ChamaMembers WHERE ChamaId = @c AND MemberId = @m", ("c", chamaId), ("m", memberId));
        var pos = await c.ScalarAsync<int?>("SELECT MAX(Position) FROM dbo.ChamaMembers WHERE ChamaId = @c", ("c", chamaId)) ?? 0;
        if (exists == "Active") return "Already in this chama.";
        if (exists != null) await c.ExecAsync("UPDATE dbo.ChamaMembers SET Status = 'Active', Role = @r, Position = @p WHERE ChamaId = @c AND MemberId = @m", ("r", role), ("p", pos + 1), ("c", chamaId), ("m", memberId));
        else await c.ExecAsync("INSERT INTO dbo.ChamaMembers (ChamaId, MemberId, Role, Position) VALUES (@c, @m, @r, @p)", ("c", chamaId), ("m", memberId), ("r", Roles.Contains(role) ? role : "Member"), ("p", pos + 1));
        return null;
    }

    public async Task SetRoleAsync(int rowId, string role)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("UPDATE dbo.ChamaMembers SET Role = @r WHERE Id = @id", ("r", Roles.Contains(role) ? role : "Member"), ("id", rowId));
    }

    public async Task<string?> RemoveMemberAsync(int chamaId, int memberId)
    {
        await using var c = await _db.OpenAsync();
        var pending = await c.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ChamaPayouts WHERE ChamaId = @c AND MemberId = @m AND Status = 'Scheduled'", ("c", chamaId), ("m", memberId));
        await c.ExecAsync("UPDATE dbo.ChamaMembers SET Status = 'Left' WHERE ChamaId = @c AND MemberId = @m", ("c", chamaId), ("m", memberId));
        if (pending > 0) await c.ExecAsync("DELETE FROM dbo.ChamaPayouts WHERE ChamaId = @c AND Status = 'Scheduled'", ("c", chamaId));
        return pending > 0 ? "Removed. Their upcoming turn was cleared, so regenerate the rotation." : null;
    }

    /// <summary>Sets the rotation order. Pass member ids in the order they receive the pot.</summary>
    public async Task SetOrderAsync(int chamaId, IList<int> memberIds)
    {
        await using var c = await _db.OpenAsync();
        for (var i = 0; i < memberIds.Count; i++)
            await c.ExecAsync("UPDATE dbo.ChamaMembers SET Position = @p WHERE ChamaId = @c AND MemberId = @m", ("p", i + 1), ("c", chamaId), ("m", memberIds[i]));
    }

    public static DateTime DueFor(Chama ch, int cycle) => ch.Frequency switch
    {
        "Weekly" => ch.StartDate.AddDays(7 * (cycle - 1)),
        "Fortnightly" => ch.StartDate.AddDays(14 * (cycle - 1)),
        _ => ch.StartDate.AddMonths(cycle - 1),
    };

    /// <summary>Builds the payout schedule from the current round onwards, following the rotation order. Paid rounds are kept.</summary>
    public async Task<string?> GenerateScheduleAsync(int chamaId)
    {
        var ch = await GetAsync(chamaId);
        if (ch == null) return "Chama not found.";
        if (!ch.Rotates) return "Only merry-go-rounds have a payout rotation.";
        var members = (await MembersAsync(chamaId)).Where(m => m.Status == "Active").OrderBy(m => m.Position).ToList();
        if (members.Count < 2) return "Add at least two members first.";
        await using var c = await _db.OpenAsync();
        var paid = (await c.QueryAsync("SELECT MemberId FROM dbo.ChamaPayouts WHERE ChamaId = @c AND Status = 'Paid'", r => r.Int("MemberId"), ("c", chamaId))).ToHashSet();
        var lastPaidCycle = await c.ScalarAsync<int?>("SELECT MAX(CycleNo) FROM dbo.ChamaPayouts WHERE ChamaId = @c AND Status = 'Paid'", ("c", chamaId)) ?? 0;
        await c.ExecAsync("DELETE FROM dbo.ChamaPayouts WHERE ChamaId = @c AND Status <> 'Paid'", ("c", chamaId));
        var queue = members.Where(m => !paid.Contains(m.MemberId)).ToList();
        if (queue.Count == 0) { queue = members; }   // everyone has had a turn: start a new lap
        var cycle = lastPaidCycle + 1;
        foreach (var m in queue)
        {
            await c.ExecAsync("INSERT INTO dbo.ChamaPayouts (ChamaId, CycleNo, MemberId, Amount, DueDate, Status) VALUES (@c, @cy, @m, @a, @d, 'Scheduled')",
                ("c", chamaId), ("cy", cycle), ("m", m.MemberId), ("a", ch.ContributionAmount * members.Count), ("d", DueFor(ch, cycle)));
            cycle++;
        }
        await c.ExecAsync("UPDATE dbo.Chamas SET CurrentCycle = @cy WHERE Id = @c", ("cy", lastPaidCycle + 1), ("c", chamaId));
        return null;
    }

    /// <summary>Random draw (like picking papers from a hat) for the members who haven't received yet.</summary>
    public async Task<string?> ShuffleAsync(int chamaId)
    {
        await using var c = await _db.OpenAsync();
        var paid = (await c.QueryAsync("SELECT MemberId FROM dbo.ChamaPayouts WHERE ChamaId = @c AND Status = 'Paid' ORDER BY CycleNo", r => r.Int("MemberId"), ("c", chamaId))).ToList();
        var rest = (await MembersAsync(chamaId)).Where(m => m.Status == "Active" && !paid.Contains(m.MemberId)).Select(m => m.MemberId).OrderBy(_ => Random.Shared.Next()).ToList();
        await SetOrderAsync(chamaId, paid.Concat(rest).ToList());
        return await GenerateScheduleAsync(chamaId);
    }

    public async Task<List<ChamaPayout>> PayoutsAsync(int chamaId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(@"SELECT p.*, CONCAT(m.FirstName, ' ', m.LastName) AS FullName FROM dbo.ChamaPayouts p JOIN Members m ON m.MemberId = p.MemberId WHERE p.ChamaId = @c ORDER BY p.CycleNo",
            r => new ChamaPayout(r.Int("Id"), r.Int("CycleNo"), r.Int("MemberId"), r.Str("FullName"), Convert.ToDecimal(r["Amount"]), r.Date("DueDate"), r.DateN("PaidOn"), r.Str("Reference"), r.Str("Status")),
            ("c", chamaId));
    }

    public async Task<List<ChamaContribution>> ContributionsAsync(int chamaId)
    {
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync("SELECT * FROM dbo.ChamaContributions WHERE ChamaId = @c ORDER BY PaidOn DESC",
            r => new ChamaContribution(r.Long("Id"), r.Int("MemberId"), r.Int("CycleNo"), Convert.ToDecimal(r["Amount"]), r.Date("PaidOn"), r.Str("Reference")), ("c", chamaId));
    }

    /// <summary>Marks this round's pot as handed over and moves the chama to the next round.</summary>
    public async Task<string?> PayOutAsync(int chamaId, string reference, string? by)
    {
        await using var c = await _db.OpenAsync();
        var cycle = await c.ScalarAsync<int?>("SELECT CurrentCycle FROM dbo.Chamas WHERE Id = @c", ("c", chamaId));
        if (cycle == null) return "Chama not found.";
        var n = await c.ExecAsync("UPDATE dbo.ChamaPayouts SET Status = 'Paid', PaidOn = SYSUTCDATETIME(), Reference = @r, PaidBy = @b WHERE ChamaId = @c AND CycleNo = @cy AND Status = 'Scheduled'",
            ("r", (reference ?? "").Trim()), ("b", by ?? ""), ("c", chamaId), ("cy", cycle));
        if (n == 0) return "There's no scheduled payout for this round. Generate the rotation first.";
        await c.ExecAsync("UPDATE dbo.Chamas SET CurrentCycle = CurrentCycle + 1 WHERE Id = @c", ("c", chamaId));
        return null;
    }

    /// <summary>Quick cash contribution straight from the round grid (M-Pesa and bank go through Payments).</summary>
    public async Task QuickContributionAsync(int chamaId, int memberId, int cycle, decimal amount, string? by)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecAsync("INSERT INTO dbo.ChamaContributions (ChamaId, MemberId, CycleNo, Amount, PaidOn, Reference, RecordedBy) VALUES (@c, @m, @cy, @a, SYSUTCDATETIME(), 'Cash at meeting', @b)",
            ("c", chamaId), ("m", memberId), ("cy", cycle), ("a", amount), ("b", by ?? ""));
    }

    public async Task<(int Chamas, int Members, decimal Collected, decimal PaidOut)> TotalsAsync()
    {
        try
        {
            await using var c = await _db.OpenAsync();
            var a = await c.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Chamas WHERE Status = 'Active'");
            var b = await c.ScalarAsync<int>("SELECT COUNT(DISTINCT MemberId) FROM dbo.ChamaMembers WHERE Status = 'Active'");
            var d = await c.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM dbo.ChamaContributions") ?? 0;
            var e = await c.ScalarAsync<decimal?>("SELECT SUM(Amount) FROM dbo.ChamaPayouts WHERE Status = 'Paid'") ?? 0;
            return (a, b, d, e);
        }
        catch { return (0, 0, 0, 0); }
    }
}
