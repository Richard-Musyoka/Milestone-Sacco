using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SaccoManagementSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Data;
using static Sacco_Management_System.Pages.Loans.ApplyLoan;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/loans")]
    [ApiController]
    public class LoansController : ControllerBase
    {
        private readonly string _connectionString;

        public LoansController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllLoans()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT 
                l.LoanId,
                l.MemberId,
                m.FirstName + ' ' + m.LastName AS MemberName,
                m.MemberNo,
                l.LoanType,
                l.PrincipalAmount,
                l.InterestRate,
                l.TermMonths,
                l.ApplicationDate,
                l.ApprovalDate,
                l.StartDate,
                l.EndDate,
                l.Status,
                l.MonthlyInstallment,
                l.TotalPayable,
                l.OutstandingBalance,
                l.Remarks,
                l.Guarantor1Id,
                l.Guarantor2Id,
                g1.FirstName + ' ' + g1.LastName AS Guarantor1Name,
                g2.FirstName + ' ' + g2.LastName AS Guarantor2Name
            FROM Loans l
            JOIN Members m ON l.MemberId = m.MemberId
            LEFT JOIN Members g1 ON l.Guarantor1Id = g1.MemberId
            LEFT JOIN Members g2 ON l.Guarantor2Id = g2.MemberId
            ORDER BY l.ApplicationDate DESC", conn);

                var loans = new List<LoanViewModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    loans.Add(new LoanViewModel
                    {
                        LoanId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNo = reader.GetString(3),
                        LoanNumber = $"LN-{reader.GetInt32(0).ToString("D4")}",
                        LoanType = reader.GetString(4),
                        PrincipalAmount = reader.GetDecimal(5),
                        InterestRate = reader.GetDecimal(6),
                        TermMonths = reader.GetInt32(7),
                        ApplicationDate = reader.GetDateTime(8),
                        ApprovalDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        StartDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                        EndDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        Status = reader.GetString(12),
                        MonthlyInstallment = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                        TotalPayable = reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                        OutstandingBalance = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                        Remarks = reader.IsDBNull(16) ? null : reader.GetString(16),
                        Guarantor1Id = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                        Guarantor2Id = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                        Guarantor1Name = reader.IsDBNull(19) ? null : reader.GetString(19),
                        Guarantor2Name = reader.IsDBNull(20) ? null : reader.GetString(20)
                    });
                }

                return Ok(loans);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve loans.", error = ex.Message });
            }
        }

        [HttpGet("installments/{loanId}")]
        public async Task<IActionResult> GetLoanInstallments(int loanId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT 
                i.InstallmentId,
                i.LoanId,
                i.InstallmentNumber,
                i.DueDate,
                i.PrincipalAmount AS Principal,
                i.InterestAmount AS Interest,
                i.TotalAmount AS TotalDue,
                i.Status,
                i.PaymentDate
            FROM LoanInstallments i
            WHERE i.LoanId = @LoanId
            ORDER BY i.InstallmentNumber", conn);

                cmd.Parameters.AddWithValue("@LoanId", loanId);

                var installments = new List<InstallmentViewModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    installments.Add(new InstallmentViewModel
                    {
                        InstallmentId = reader.GetInt32(0),
                        LoanId = reader.GetInt32(1),
                        InstallmentNumber = reader.GetInt32(2),
                        DueDate = reader.GetDateTime(3),
                        Principal = reader.GetDecimal(4),
                        Interest = reader.GetDecimal(5),
                        TotalDue = reader.GetDecimal(6),
                        Status = reader.GetString(7),
                        PaymentDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                    });
                }

                return Ok(installments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve installments.", error = ex.Message });
            }
        }

        [HttpPut("mark-paid")]
        public async Task<IActionResult> MarkInstallmentAsPaid([FromBody] MarkPaymentDto paymentDto)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Update the installment
                var cmd = new SqlCommand(@"
            UPDATE LoanInstallments 
            SET 
                Status = 'Paid',
                PaymentDate = @PaymentDate
            WHERE InstallmentId = @InstallmentId
            
            -- Update loan outstanding balance
            UPDATE Loans
            SET OutstandingBalance = OutstandingBalance - 
                (SELECT TotalAmount FROM LoanInstallments WHERE InstallmentId = @InstallmentId)
            WHERE LoanId = (SELECT LoanId FROM LoanInstallments WHERE InstallmentId = @InstallmentId)", conn);

                cmd.Parameters.AddWithValue("@InstallmentId", paymentDto.InstallmentId);
                cmd.Parameters.AddWithValue("@PaymentDate", paymentDto.PaymentDate);

                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Installment marked as paid successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to mark installment as paid.", error = ex.Message });
            }
        }

        public class MarkPaymentDto
        {
            public int InstallmentId { get; set; }
            public DateTime PaymentDate { get; set; }
        }

        // Add these new DTO classes to your controller file
        public class GuarantorDto
        {
            public int GuarantorId { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string IDNumber { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string PhysicalAddress { get; set; } = string.Empty;
            public bool IsExternal { get; set; }
            public int? MemberId { get; set; } // Null for external guarantors
        }

        public class LoanApplicationDto
        {
            [Required] public int MemberId { get; set; }
            [Required] public string LoanType { get; set; } = string.Empty;
            [Required] public decimal PrincipalAmount { get; set; }
            [Required] public decimal InterestRate { get; set; } = 12;
            [Required] public int TermMonths { get; set; } = 12;
            [Required] public string Purpose { get; set; } = string.Empty;
            public int? Guarantor1Id { get; set; }
            public int? Guarantor2Id { get; set; }
            public string? Remarks { get; set; } 
            public string LoanNumber { get; set; } = string.Empty;
            public DateTime ApplicationDate { get; set; }
            public DateTime CreatedDate { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddLoan([FromBody] LoanApplicationDto loanDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Validate member exists
                if (!await MemberExists(conn, loanDto.MemberId))
                {
                    return BadRequest("Specified member does not exist");
                }

                // Validate guarantors exist if provided
                if (loanDto.Guarantor1Id.HasValue && !await GuarantorExists(conn, loanDto.Guarantor1Id.Value))
                {
                    return BadRequest("Primary guarantor does not exist");
                }

                if (loanDto.Guarantor2Id.HasValue && !await GuarantorExists(conn, loanDto.Guarantor2Id.Value))
                {
                    return BadRequest("Secondary guarantor does not exist");
                }

                // Calculate loan details
                decimal monthlyInstallment = CalculateMonthlyPayment(
                    loanDto.PrincipalAmount,
                    loanDto.InterestRate,
                    loanDto.TermMonths);

                var cmd = new SqlCommand(@"
            INSERT INTO Loans 
                (MemberId, LoanType, PrincipalAmount, InterestRate, TermMonths, 
                 ApplicationDate, Status, MonthlyInstallment, TotalPayable, 
                 OutstandingBalance, Guarantor1Id, Guarantor2Id, Remarks, CreatedDate)
            VALUES 
                (@MemberId, @LoanType, @PrincipalAmount, @InterestRate, @TermMonths, 
                 @ApplicationDate, @Status, @MonthlyInstallment, @TotalPayable, 
                 @OutstandingBalance, 
                 CASE WHEN @Guarantor1Id = 0 THEN NULL ELSE @Guarantor1Id END, 
                 CASE WHEN @Guarantor2Id = 0 THEN NULL ELSE @Guarantor2Id END, 
                 @Remarks, @CreatedDate);
            SELECT SCOPE_IDENTITY();", conn);

                cmd.Parameters.AddWithValue("@MemberId", loanDto.MemberId);
                cmd.Parameters.AddWithValue("@LoanType", loanDto.LoanType);
                cmd.Parameters.AddWithValue("@PrincipalAmount", loanDto.PrincipalAmount);
                cmd.Parameters.AddWithValue("@InterestRate", loanDto.InterestRate);
                cmd.Parameters.AddWithValue("@TermMonths", loanDto.TermMonths);
                cmd.Parameters.AddWithValue("@ApplicationDate", loanDto.ApplicationDate);
                cmd.Parameters.AddWithValue("@Status", loanDto.Status ?? "Pending");
                cmd.Parameters.AddWithValue("@MonthlyInstallment", monthlyInstallment);
                cmd.Parameters.AddWithValue("@TotalPayable", monthlyInstallment * loanDto.TermMonths);
                cmd.Parameters.AddWithValue("@OutstandingBalance", monthlyInstallment * loanDto.TermMonths);
                cmd.Parameters.AddWithValue("@Guarantor1Id", loanDto.Guarantor1Id ?? 0);
                cmd.Parameters.AddWithValue("@Guarantor2Id", loanDto.Guarantor2Id ?? 0);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(loanDto.Remarks) ? DBNull.Value : (object)loanDto.Remarks);
                cmd.Parameters.AddWithValue("@CreatedDate", loanDto.CreatedDate);

                var newId = await cmd.ExecuteScalarAsync();

                return Ok(new
                {
                    message = "Loan added successfully",
                    loanId = newId,
                    loanNumber = loanDto.LoanNumber
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to add loan", error = ex.Message });
            }
        }

        private async Task<bool> MemberExists(SqlConnection conn, int memberId)
        {
            var cmd = new SqlCommand("SELECT 1 FROM Members WHERE MemberId = @MemberId", conn);
            cmd.Parameters.AddWithValue("@MemberId", memberId);
            return await cmd.ExecuteScalarAsync() != null;
        }

        private async Task<bool> GuarantorExists(SqlConnection conn, int guarantorId)
        {
            var cmd = new SqlCommand("SELECT 1 FROM Guarantors WHERE GuarantorId = @GuarantorId", conn);
            cmd.Parameters.AddWithValue("@GuarantorId", guarantorId);
            return await cmd.ExecuteScalarAsync() != null;
        }

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetLoanById(int id)
        {
            Console.WriteLine($"Received request for loan ID: {id}");

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                Console.WriteLine("Database connection opened");

                var cmd = new SqlCommand(@"
                    SELECT 
                        l.*, 
                        m.FirstName + ' ' + m.LastName AS MemberName,
                        m.MemberNo,
                        g1.FirstName + ' ' + g1.LastName AS Guarantor1Name,
                        g2.FirstName + ' ' + g2.LastName AS Guarantor2Name
                    FROM Loans l
                    JOIN Members m ON l.MemberId = m.MemberId
                    LEFT JOIN Members g1 ON l.Guarantor1Id = g1.MemberId
                    LEFT JOIN Members g2 ON l.Guarantor2Id = g2.MemberId
                    WHERE l.LoanId = @id", conn);

                cmd.Parameters.AddWithValue("@id", id);
                Console.WriteLine($"Executing query for loan ID: {id}");

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Console.WriteLine("Loan record found");
                    var loan = MapReaderToLoan(reader);
                    return Ok(loan);
                }

                Console.WriteLine("No loan record found in database");
                return NotFound(new { message = "Loan not found." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex}");
                return StatusCode(500, new { message = "Failed to retrieve loan.", error = ex.Message });
            }
        }

        [HttpPut("approve/{id}")]
        public async Task<IActionResult> ApproveLoan(int id, [FromBody] LoanApprovalDto approvalDto)
        {
            try
            {
                // Calculate loan details
                decimal monthlyInstallment = CalculateMonthlyPayment(
                    approvalDto.PrincipalAmount,
                    approvalDto.InterestRate,
                    approvalDto.TermMonths);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE Loans SET 
                        Status = 'Approved',
                        ApprovalDate = @ApprovalDate,
                        InterestRate = @InterestRate,
                        TermMonths = @TermMonths,
                        MonthlyInstallment = @MonthlyInstallment,
                        TotalPayable = @TotalPayable,
                        OutstandingBalance = @OutstandingBalance,
                        Remarks = @Remarks
                    WHERE LoanId = @LoanId", conn);

                cmd.Parameters.AddWithValue("@LoanId", id);
                cmd.Parameters.AddWithValue("@ApprovalDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@InterestRate", approvalDto.InterestRate);
                cmd.Parameters.AddWithValue("@TermMonths", approvalDto.TermMonths);
                cmd.Parameters.AddWithValue("@MonthlyInstallment", monthlyInstallment);
                cmd.Parameters.AddWithValue("@TotalPayable", monthlyInstallment * approvalDto.TermMonths);
                cmd.Parameters.AddWithValue("@OutstandingBalance", monthlyInstallment * approvalDto.TermMonths);
                cmd.Parameters.AddWithValue("@Remarks", (object?)approvalDto.Remarks ?? 0);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    return NotFound(new { message = "Loan not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to approve loan.", error = ex.Message });
            }

            return Ok(new { message = "Loan approved successfully." });
        }

        [HttpPut("reject/{id}")]
        public async Task<IActionResult> RejectLoan(int id, [FromBody] string remarks)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE Loans SET 
                        Status = 'Rejected',
                        Remarks = @Remarks
                    WHERE LoanId = @LoanId", conn);

                cmd.Parameters.AddWithValue("@LoanId", id);
                cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? 0);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    return NotFound(new { message = "Loan not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to reject loan.", error = ex.Message });
            }

            return Ok(new { message = "Loan rejected successfully." });
        }

        [HttpPut("disburse/{id}")]
        public async Task<IActionResult> DisburseLoan(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE Loans SET 
                        Status = 'Disbursed',
                        StartDate = @StartDate,
                        EndDate = DATEADD(MONTH, TermMonths, @StartDate)
                    WHERE LoanId = @LoanId AND Status = 'Approved'", conn);

                cmd.Parameters.AddWithValue("@LoanId", id);
                cmd.Parameters.AddWithValue("@StartDate", DateTime.Now);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    return NotFound(new { message = "Loan not found or not approved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to disburse loan.", error = ex.Message });
            }

            return Ok(new { message = "Loan disbursed successfully." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteLoan(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand("DELETE FROM Loans WHERE LoanId = @LoanId", conn);
                cmd.Parameters.AddWithValue("@LoanId", id);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "Loan not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete loan.", error = ex.Message });
            }

            return Ok(new { message = "Loan deleted successfully." });
        }
        private LoanViewModel MapReaderToLoan(SqlDataReader reader)
        {
            var loanIdValue = reader["LoanId"] != DBNull.Value ? Convert.ToInt32(reader["LoanId"]) : 0;

            // Generate loan number if not in database
            var loanNumber = $"LN-{loanIdValue.ToString("D4")}";

            return new LoanViewModel
            {
                LoanId = loanIdValue,
                MemberId = reader["MemberId"] != DBNull.Value ? Convert.ToInt32(reader["MemberId"]) : 0,
                MemberName = reader["MemberName"]?.ToString() ?? string.Empty,
                MemberNo = reader["MemberNo"]?.ToString() ?? string.Empty,
                LoanNumber = loanNumber,  
                LoanType = reader["LoanType"]?.ToString() ?? string.Empty,
                PrincipalAmount = reader["PrincipalAmount"] != DBNull.Value ? Convert.ToDecimal(reader["PrincipalAmount"]) : 0,
                InterestRate = reader["InterestRate"] != DBNull.Value ? Convert.ToDecimal(reader["InterestRate"]) : 0,
                TermMonths = reader["TermMonths"] != DBNull.Value ? Convert.ToInt32(reader["TermMonths"]) : 0,
                ApplicationDate = reader["ApplicationDate"] != DBNull.Value ? Convert.ToDateTime(reader["ApplicationDate"]) : DateTime.Now,
                ApprovalDate = reader["ApprovalDate"] != DBNull.Value ? Convert.ToDateTime(reader["ApprovalDate"]) : (DateTime?)null,
                StartDate = reader["StartDate"] != DBNull.Value ? Convert.ToDateTime(reader["StartDate"]) : (DateTime?)null,
                EndDate = reader["EndDate"] != DBNull.Value ? Convert.ToDateTime(reader["EndDate"]) : (DateTime?)null,
                Status = reader["Status"]?.ToString() ?? "Pending",
                MonthlyInstallment = reader["MonthlyInstallment"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlyInstallment"]) : (decimal?)null,
                TotalPayable = reader["TotalPayable"] != DBNull.Value ? Convert.ToDecimal(reader["TotalPayable"]) : (decimal?)null,
                OutstandingBalance = reader["OutstandingBalance"] != DBNull.Value ? Convert.ToDecimal(reader["OutstandingBalance"]) : (decimal?)null,
                Remarks = reader["Remarks"]?.ToString(),
                Guarantor1Id = reader["Guarantor1Id"] != DBNull.Value ? Convert.ToInt32(reader["Guarantor1Id"]) : (int?)null,
                Guarantor2Id = reader["Guarantor2Id"] != DBNull.Value ? Convert.ToInt32(reader["Guarantor2Id"]) : (int?)null,
                Guarantor1Name = reader["Guarantor1Name"]?.ToString(),
                Guarantor2Name = reader["Guarantor2Name"]?.ToString()
            };
        }

        private decimal CalculateMonthlyPayment(decimal principal, decimal interestRate, int termMonths)
        {
            decimal monthlyRate = interestRate / 100 / 12;
            decimal factor = (decimal)Math.Pow((double)(1 + monthlyRate), termMonths);
            return principal * monthlyRate * factor / (factor - 1);
        }
    }

    public class LoanApprovalDto
    {
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int TermMonths { get; set; }
        public string? Remarks { get; set; }
    }
}