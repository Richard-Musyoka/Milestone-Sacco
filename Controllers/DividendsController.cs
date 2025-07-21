using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaccoManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Threading.Tasks;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/dividends")]
    [ApiController]
    public class DividendsController : ControllerBase
    {
        private readonly string _connectionString;

        public DividendsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        #region Summary Endpoints

        [HttpGet("summary")]
        public async Task<IActionResult> GetDividendSummary()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        (SELECT SUM(TotalAmount) FROM DividendDeclarations WHERE Status = 'Processed') AS TotalDividends,
                        (SELECT SUM(Amount) FROM DividendPayments WHERE Status = 'Paid') AS PaidDividends,
                        (SELECT SUM(Amount) FROM DividendPayments WHERE Status = 'Pending') AS PendingDividends,
                        (SELECT COUNT(DISTINCT MemberId) FROM DividendPayments WHERE Status = 'Paid') AS PaidMembersCount,
                        (SELECT COUNT(DISTINCT MemberId) FROM DividendPayments WHERE Status = 'Pending') AS PendingMembersCount,
                        (SELECT TOP 1 Rate FROM DividendDeclarations ORDER BY DeclarationDate DESC) AS CurrentDividendRate,
                        (SELECT TOP 1 FinancialYear FROM DividendDeclarations ORDER BY DeclarationDate DESC) AS CurrentDividendYear,
                        (SELECT TOP 1 DeclarationDate FROM DividendDeclarations ORDER BY DeclarationDate DESC) AS CurrentDeclarationDate", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var summary = new DividendSummaryDto
                    {
                        TotalDividends = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0),
                        PaidDividends = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        PendingDividends = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        PaidMembersCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        PendingMembersCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        CurrentDividendRate = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                        CurrentDividendYear = reader.IsDBNull(6) ? null : reader.GetString(6),
                        CurrentDeclarationDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7)
                    };

                    return Ok(summary);
                }

                return Ok(new DividendSummaryDto());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve dividend summary", error = ex.Message });
            }
        }

        #endregion

        #region Declaration Endpoints

        [HttpGet("declarations")]
        public async Task<IActionResult> GetAllDeclarations()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        dd.DeclarationId,
                        dd.DeclarationNumber,
                        dd.FinancialYear,
                        dd.Rate,
                        dd.TotalAmount,
                        dd.DeclarationDate,
                        dd.RecordDate,
                        dd.PaymentDate,
                        dd.Status,
                        dd.Notes,
                        dd.CreatedDate,
                        (SELECT COUNT(*) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId) AS PaymentCount,
                        (SELECT SUM(Amount) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId AND dp.Status = 'Paid') AS PaidAmount
                    FROM DividendDeclarations dd
                    ORDER BY dd.DeclarationDate DESC", conn);

                var declarations = new List<DividendDeclarationDto>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    declarations.Add(new DividendDeclarationDto
                    {
                        DeclarationId = reader.GetInt32(0),
                        DeclarationNumber = reader.GetString(1),
                        FinancialYear = reader.GetString(2),
                        Rate = reader.GetDecimal(3),
                        TotalAmount = reader.GetDecimal(4),
                        DeclarationDate = reader.GetDateTime(5),
                        RecordDate = reader.GetDateTime(6),
                        PaymentDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                        Status = reader.GetString(8),
                        Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                        CreatedDate = reader.GetDateTime(10),
                        PaymentCount = reader.GetInt32(11),
                        PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12)
                    });
                }

                return Ok(declarations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve dividend declarations", error = ex.Message });
            }
        }

        [HttpGet("declarations/{id}")]
        public async Task<IActionResult> GetDeclarationById(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        dd.DeclarationId,
                        dd.DeclarationNumber,
                        dd.FinancialYear,
                        dd.Rate,
                        dd.TotalAmount,
                        dd.DeclarationDate,
                        dd.RecordDate,
                        dd.PaymentDate,
                        dd.Status,
                        dd.Notes,
                        dd.CreatedDate,
                        (SELECT COUNT(*) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId) AS PaymentCount,
                        (SELECT SUM(Amount) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId AND dp.Status = 'Paid') AS PaidAmount
                    FROM DividendDeclarations dd
                    WHERE dd.DeclarationId = @DeclarationId", conn);

                cmd.Parameters.AddWithValue("@DeclarationId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var declaration = new DividendDeclarationDto
                    {
                        DeclarationId = reader.GetInt32(0),
                        DeclarationNumber = reader.GetString(1),
                        FinancialYear = reader.GetString(2),
                        Rate = reader.GetDecimal(3),
                        TotalAmount = reader.GetDecimal(4),
                        DeclarationDate = reader.GetDateTime(5),
                        RecordDate = reader.GetDateTime(6),
                        PaymentDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                        Status = reader.GetString(8),
                        Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                        CreatedDate = reader.GetDateTime(10),
                        PaymentCount = reader.GetInt32(11),
                        PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12)
                    };

                    return Ok(declaration);
                }

                return NotFound(new { message = "Dividend declaration not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve dividend declaration", error = ex.Message });
            }
        }

        [HttpGet("declarations/financial-year/{financialYear}")]
        public async Task<IActionResult> GetDeclarationByYear(string financialYear)
        {
            if (string.IsNullOrWhiteSpace(financialYear))
            {
                return BadRequest(new { message = "Financial year is required" });
            }

            try
            {
                // Decode the URL-encoded financial year first
                var decodedYear = Uri.UnescapeDataString(financialYear);

                // Then normalize the format
                var normalizedFinancialYear = decodedYear.Replace("-", "/");

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
            SELECT 
                dd.DeclarationId,
                dd.DeclarationNumber,
                dd.FinancialYear,
                dd.Rate,
                dd.TotalAmount,
                dd.DeclarationDate,
                dd.RecordDate,
                dd.PaymentDate,
                dd.Status,
                dd.Notes,
                dd.CreatedDate,
                (SELECT COUNT(*) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId) AS PaymentCount,
                (SELECT SUM(Amount) FROM DividendPayments dp WHERE dp.DeclarationId = dd.DeclarationId AND dp.Status = 'Paid') AS PaidAmount
            FROM DividendDeclarations dd
            WHERE dd.FinancialYear = @FinancialYear";

                var declaration = await conn.QueryFirstOrDefaultAsync<DividendDeclarationDto>(query, new
                {
                    FinancialYear = normalizedFinancialYear
                });

                return declaration == null
                    ? NotFound(new { message = $"No dividend declaration found for financial year {normalizedFinancialYear}" })
                    : Ok(declaration);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while processing your request",
                    detail = ex.Message
                });
            }
        }

        [HttpPost("declarations")]
        public async Task<IActionResult> CreateDeclaration([FromBody] DividendDeclarationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check for existing declaration
                var existsCmd = new SqlCommand(
                    "SELECT 1 FROM DividendDeclarations WHERE FinancialYear = @FinancialYear",
                    conn);
                existsCmd.Parameters.AddWithValue("@FinancialYear", request.FinancialYear);

                if (await existsCmd.ExecuteScalarAsync() != null)
                {
                    return Conflict(new
                    {
                        message = $"Declaration for {request.FinancialYear} already exists"
                    });
                }

                // Generate shorter declaration number (max 20 chars)
                var yearPart = request.FinancialYear.Replace("/", "");
                var timePart = DateTime.Now.ToString("MMddHHmm");
                var declarationNumber = $"DIV-{yearPart}-{timePart}";

                // Ensure it doesn't exceed 20 characters
                if (declarationNumber.Length > 20)
                {
                    declarationNumber = declarationNumber.Substring(0, 20);
                }

                var insertCmd = new SqlCommand(@"
            INSERT INTO DividendDeclarations 
                (DeclarationNumber, FinancialYear, Rate, TotalAmount, 
                 DeclarationDate, RecordDate, PaymentDate, Status, Notes, 
                 CreatedBy, CreatedDate)
            VALUES 
                (@DeclarationNumber, @FinancialYear, @Rate, @TotalAmount, 
                 @DeclarationDate, @RecordDate, @PaymentDate, 'Pending', @Notes, 
                 1, GETDATE());
            SELECT SCOPE_IDENTITY();", conn);

                insertCmd.Parameters.AddWithValue("@DeclarationNumber", declarationNumber);
                insertCmd.Parameters.AddWithValue("@FinancialYear", request.FinancialYear);
                insertCmd.Parameters.AddWithValue("@Rate", request.Rate);
                insertCmd.Parameters.AddWithValue("@TotalAmount", request.TotalAmount);
                insertCmd.Parameters.AddWithValue("@DeclarationDate", request.DeclarationDate);
                insertCmd.Parameters.AddWithValue("@RecordDate", request.RecordDate);
                insertCmd.Parameters.AddWithValue("@PaymentDate", request.PaymentDate ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);

                var newId = await insertCmd.ExecuteScalarAsync();

                return CreatedAtAction(
                    nameof(GetDeclarationById),
                    new { id = newId },
                    new
                    {
                        message = "Declaration created successfully",
                        declarationId = newId,
                        declarationNumber
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Declaration creation failed",
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }
        [HttpPut("declarations/financial-year/{financialYear}")]
        public async Task<IActionResult> UpdateDeclaration(string financialYear, [FromBody] DividendDeclarationRequest request)
        {
            // Validate input
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Decode URL-encoded financial year
            financialYear = WebUtility.UrlDecode(financialYear);

            // Verify financial year matches between route and body
            if (!string.Equals(request.FinancialYear, financialYear, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Financial year in URL doesn't match request body" });
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // First check if declaration exists and is modifiable
                var checkCmd = new SqlCommand(
                    "SELECT DeclarationId FROM DividendDeclarations WHERE FinancialYear = @FinancialYear",
                    conn);
                checkCmd.Parameters.AddWithValue("@FinancialYear", financialYear);

                var existingId = await checkCmd.ExecuteScalarAsync();
                if (existingId == null)
                {
                    return NotFound(new { message = "Dividend declaration not found" });
                }

                // Update the declaration
                var updateCmd = new SqlCommand(@"
            UPDATE DividendDeclarations SET 
                Rate = @Rate,
                DeclarationDate = @DeclarationDate,
                RecordDate = @RecordDate,
                PaymentDate = @PaymentDate,
                Notes = @Notes,
                TotalAmount = @TotalAmount
            WHERE FinancialYear = @FinancialYear AND Status = 'Pending'", conn);

                updateCmd.Parameters.AddWithValue("@FinancialYear", financialYear);
                updateCmd.Parameters.AddWithValue("@Rate", request.Rate);
                updateCmd.Parameters.AddWithValue("@DeclarationDate", request.DeclarationDate);
                updateCmd.Parameters.AddWithValue("@RecordDate", request.RecordDate);
                updateCmd.Parameters.AddWithValue("@PaymentDate", request.PaymentDate);
                updateCmd.Parameters.AddWithValue("@Notes", string.IsNullOrEmpty(request.Notes) ? DBNull.Value : (object)request.Notes);
                updateCmd.Parameters.AddWithValue("@TotalAmount", request.TotalAmount);

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return Conflict(new
                    {
                        message = "Dividend declaration cannot be modified - it may already be processed",
                        status = "NotModifiable"
                    });
                }

                return Ok(new
                {
                    message = "Dividend declaration updated successfully",
                    financialYear = financialYear
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to update dividend declaration",
                    error = ex.Message
                });
            }
        }
        [HttpPost("declarations/{id}/approve")]
        public async Task<IActionResult> ApproveDeclaration(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // First check if we can approve (must be in Pending status)
                var checkCmd = new SqlCommand(
                    "SELECT Status, RecordDate, FinancialYear FROM DividendDeclarations WHERE DeclarationId = @DeclarationId",
                    conn);
                checkCmd.Parameters.AddWithValue("@DeclarationId", id);

                DateTime recordDate;
                string financialYear;
                string currentStatus;

                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return NotFound(new { message = "Declaration not found" });
                    }

                    currentStatus = reader.GetString(0);
                    recordDate = reader.GetDateTime(1);
                    financialYear = reader.GetString(2);
                }

                if (currentStatus != "Pending")
                {
                    return BadRequest(new { message = $"Declaration cannot be approved in its current status ({currentStatus})" });
                }

                // Calculate total shares and generate payments
                var calculateCmd = new SqlCommand(@"
                    -- Calculate total shares
                    DECLARE @TotalShares DECIMAL(18,2) = (
                        SELECT SUM(s.Units) 
                        FROM Shares s
                        JOIN Members m ON s.MemberId = m.MemberId
                        WHERE s.Status = 'Active' 
                        AND s.PurchaseDate <= @RecordDate
                        AND m.Status = 'Active'
                    );
                    
                    -- Update declaration with total amount
                    UPDATE DividendDeclarations 
                    SET TotalAmount = @TotalShares * Rate,
                        Status = 'Approved',
                        ApprovedBy = @ApprovedBy,
                        ApprovedDate = @ApprovedDate
                    WHERE DeclarationId = @DeclarationId;
                    
                    -- Generate dividend payments for all eligible members
                    INSERT INTO DividendPayments (
                        MemberId, DeclarationId, FinancialYear, Amount, Shares, Status, CreatedDate
                    )
                    SELECT 
                        s.MemberId,
                        @DeclarationId,
                        @FinancialYear,
                        s.Units * dd.Rate AS Amount,
                        s.Units AS Shares,
                        'Pending' AS Status,
                        GETDATE() AS CreatedDate
                    FROM Shares s
                    JOIN Members m ON s.MemberId = m.MemberId
                    JOIN DividendDeclarations dd ON dd.DeclarationId = @DeclarationId
                    WHERE s.Status = 'Active' 
                    AND s.PurchaseDate <= dd.RecordDate
                    AND m.Status = 'Active';", conn);

                calculateCmd.Parameters.AddWithValue("@DeclarationId", id);
                calculateCmd.Parameters.AddWithValue("@RecordDate", recordDate);
                calculateCmd.Parameters.AddWithValue("@FinancialYear", financialYear);
                calculateCmd.Parameters.AddWithValue("@ApprovedBy", 1); // TODO: Replace with actual user ID
                calculateCmd.Parameters.AddWithValue("@ApprovedDate", DateTime.Now);

                await calculateCmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    message = "Dividend declaration approved and payments generated",
                    declarationId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to approve dividend declaration",
                    error = ex.Message
                });
            }
        }

        [HttpPost("declarations/{id}/process")]
        public async Task<IActionResult> ProcessDeclaration(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // First check if we can process (must be in Approved status)
                var checkCmd = new SqlCommand(
                    "SELECT Status FROM DividendDeclarations WHERE DeclarationId = @DeclarationId",
                    conn);
                checkCmd.Parameters.AddWithValue("@DeclarationId", id);

                var currentStatus = await checkCmd.ExecuteScalarAsync() as string;
                if (currentStatus != "Approved")
                {
                    return BadRequest(new
                    {
                        message = $"Declaration cannot be processed in its current status ({currentStatus})"
                    });
                }

                var cmd = new SqlCommand(@"
                    UPDATE DividendDeclarations 
                    SET Status = 'Processed',
                        ProcessedBy = @ProcessedBy,
                        ProcessedDate = @ProcessedDate
                    WHERE DeclarationId = @DeclarationId;", conn);

                cmd.Parameters.AddWithValue("@DeclarationId", id);
                cmd.Parameters.AddWithValue("@ProcessedBy", 1); // TODO: Replace with actual user ID from auth
                cmd.Parameters.AddWithValue("@ProcessedDate", DateTime.Now);

                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Dividend declaration marked as processed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to process dividend declaration",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("declarations/{id}")]
        public async Task<IActionResult> DeleteDeclaration(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // First check if we can delete (must be in Pending status)
                var checkCmd = new SqlCommand(
                    "SELECT Status FROM DividendDeclarations WHERE DeclarationId = @DeclarationId",
                    conn);
                checkCmd.Parameters.AddWithValue("@DeclarationId", id);

                var currentStatus = await checkCmd.ExecuteScalarAsync() as string;
                if (currentStatus != "Pending")
                {
                    return BadRequest(new
                    {
                        message = $"Declaration cannot be deleted in its current status ({currentStatus})"
                    });
                }

                var cmd = new SqlCommand(
                    "DELETE FROM DividendDeclarations WHERE DeclarationId = @DeclarationId",
                    conn);
                cmd.Parameters.AddWithValue("@DeclarationId", id);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Dividend declaration not found" });
                }

                return Ok(new { message = "Dividend declaration deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to delete dividend declaration",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region Payment Endpoints

        [HttpGet("payments")]
        public async Task<IActionResult> GetAllPayments()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var query = @"
            SELECT 
                dp.DividendId,
                dp.MemberId,
                m.FirstName + ' ' + m.LastName AS MemberName,
                m.MemberNo,
                dp.DeclarationId,
                dd.FinancialYear,
                dp.Amount,
                dp.Shares,
                dp.PaymentDate,
                dp.PaymentMethod,
                dp.PaymentNumber,
                dp.TransactionReference,
                dp.Status,
                dp.Remarks,
                dp.BankAccountNumber AS BankAccount,
                m.PhoneNumber
            FROM DividendPayments dp
            JOIN Members m ON dp.MemberId = m.MemberId
            JOIN DividendDeclarations dd ON dp.DeclarationId = dd.DeclarationId
            ORDER BY dp.Status, dp.PaymentDate DESC";

                var payments = await conn.QueryAsync<DividendPaymentDto>(query);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to retrieve dividend payments",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("payments/{id}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT 
                dp.DividendId,
                dp.MemberId,
                m.FirstName + ' ' + m.LastName AS MemberName,
                m.MemberNo,
                dp.DeclarationId,
                dp.FinancialYear,
                dp.Amount,
                dp.Shares,
                dp.PaymentDate,
                dp.PaymentMethod,
                dp.PaymentNumber,
                dp.TransactionReference,
                dp.Status,
                dp.Remarks,
                dp.BankAccountNumber,  
                m.PhoneNumber
            FROM DividendPayments dp
            JOIN Members m ON dp.MemberId = m.MemberId
            JOIN DividendDeclarations dd ON dp.DeclarationId = dd.DeclarationId
            WHERE dp.DividendId = @DividendId", conn);

                cmd.Parameters.AddWithValue("@DividendId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var payment = new DividendPaymentDto
                    {
                        DividendId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNumber = reader.GetString(3),
                        DeclarationId = reader.GetInt32(4),
                        FinancialYear = reader.GetString(5),
                        Amount = reader.GetDecimal(6),
                        Shares = reader.GetDecimal(7),
                        PaymentDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                        PaymentMethod = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PaymentNumber = reader.IsDBNull(10) ? null : reader.GetString(10),
                        TransactionReference = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = reader.GetString(12),
                        Remarks = reader.IsDBNull(13) ? null : reader.GetString(13),
                        BankAccountNumber = reader.IsDBNull(14) ? null : reader.GetString(14),  // Mapped to correct property
                        PhoneNumber = reader.IsDBNull(15) ? null : reader.GetString(15)
                    };

                    return Ok(payment);
                }

                return NotFound(new { message = "Dividend payment not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to retrieve dividend payment",
                    error = ex.Message
                });
            }
        }

        [HttpGet("payments/declaration/{declarationId}")]
        public async Task<IActionResult> GetPaymentsByDeclaration(int declarationId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT 
                dp.DividendId,
                dp.MemberId,
                m.FirstName + ' ' + m.LastName AS MemberName,
                m.MemberNo,
                dp.DeclarationId,
                dd.FinancialYear,
                dp.Amount,
                dp.Shares,
                dp.PaymentDate,
                dp.PaymentMethod,
                dp.PaymentNumber,
                dp.TransactionReference,
                dp.Status,
                dp.Remarks,
                dp.BankAccountNumber AS BankAccount,  -- This is the critical fix
                m.PhoneNumber
            FROM DividendPayments dp
            JOIN Members m ON dp.MemberId = m.MemberId
            JOIN DividendDeclarations dd ON dp.DeclarationId = dd.DeclarationId
            WHERE dp.DeclarationId = @DeclarationId
            ORDER BY dp.Status, dp.PaymentDate DESC", conn);

                cmd.Parameters.AddWithValue("@DeclarationId", declarationId);

                var payments = new List<DividendPaymentDto>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    payments.Add(new DividendPaymentDto
                    {
                        DividendId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNumber = reader.GetString(3),
                        DeclarationId = reader.GetInt32(4),
                        FinancialYear = reader.GetString(5),
                        Amount = reader.GetDecimal(6),
                        Shares = reader.GetDecimal(7),
                        PaymentDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                        PaymentMethod = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PaymentNumber = reader.IsDBNull(10) ? null : reader.GetString(10),
                        TransactionReference = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = reader.GetString(12),
                        Remarks = reader.IsDBNull(13) ? null : reader.GetString(13),
                        BankAccountNumber = reader.IsDBNull(14) ? null : reader.GetString(14), // Now matches the alias
                        PhoneNumber = reader.IsDBNull(15) ? null : reader.GetString(15)
                    });
                }

                return Ok(payments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to retrieve dividend payments",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("payments/process")]
        public async Task<IActionResult> ProcessPayments([FromBody] ProcessDividendPaymentsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    var batchRef = string.IsNullOrEmpty(request.BatchReference)
                        ? $"BATCH-{DateTime.Now:yyyyMMddHHmmss}"
                        : request.BatchReference;

                    foreach (var dividendId in request.DividendIds)
                    {
                        var paymentMethod = request.PaymentMethods.ContainsKey(dividendId)
                            ? request.PaymentMethods[dividendId]
                            : "Bank Transfer"; // Default method

                        var cmd = new SqlCommand(@"
                    UPDATE dp
                    SET 
                        dp.PaymentDate = @PaymentDate,
                        dp.Status = 'Paid',
                        dp.PaymentMethod = @PaymentMethod,
                        dp.TransactionReference = @TransactionReference,
                        dp.Remarks = @Remarks
                    FROM DividendPayments dp
                    WHERE dp.DividendId = @DividendId", conn, transaction);

                        cmd.Parameters.AddWithValue("@DividendId", dividendId);
                        cmd.Parameters.AddWithValue("@PaymentDate", request.PaymentDate);
                        cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@TransactionReference", $"{batchRef}-{dividendId}");
                        cmd.Parameters.AddWithValue("@Remarks", $"Processed in batch {batchRef}");

                        await cmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    return Ok(new
                    {
                        message = "Payments processed successfully",
                        batchReference = batchRef,
                        paymentCount = request.DividendIds.Count
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, new
                    {
                        message = "Failed to process payments",
                        error = ex.Message
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to process payments",
                    error = ex.Message
                });
            }
        }

        [HttpPost("payments/{id}/process")]
        public async Task<IActionResult> ProcessSinglePayment(int id, [FromBody] ProcessDividendPaymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Generate payment reference
                var paymentReference = $"DIV-{DateTime.Now:yyyyMMddHHmmss}-{id}";

                var cmd = new SqlCommand(@"
                    UPDATE dp
                    SET 
                        dp.PaymentDate = @PaymentDate,
                        dp.PaymentMethod = @PaymentMethod,
                        dp.Status = 'Paid',
                        dp.PaymentNumber = CASE 
                            WHEN @PaymentMethod = 'Bank Transfer' THEN m.BankAccountNumber
                            WHEN @PaymentMethod = 'M-Pesa' THEN m.PhoneNumber
                            ELSE @PaymentReference
                        END,
                        dp.TransactionReference = @PaymentReference,
                        dp.Remarks = @Remarks
                    FROM DividendPayments dp
                    JOIN Members m ON dp.MemberId = m.MemberId
                    WHERE dp.DividendId = @DividendId AND dp.Status = 'Pending'", conn);

                cmd.Parameters.AddWithValue("@DividendId", id);
                cmd.Parameters.AddWithValue("@PaymentDate", request.PaymentDate);
                cmd.Parameters.AddWithValue("@PaymentMethod", request.PaymentMethod);
                cmd.Parameters.AddWithValue("@PaymentReference", paymentReference);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(request.Remarks) ?
                    $"Processed on {DateTime.Now}" :
                    request.Remarks);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Payment not found or already processed" });
                }

                return Ok(new
                {
                    message = "Payment processed successfully",
                    paymentReference
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to process payment",
                    error = ex.Message
                });
            }
        }

        [HttpPost("payments/{id}/fail")]
        public async Task<IActionResult> MarkPaymentFailed(int id, [FromBody] string remarks)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE DividendPayments 
                    SET 
                        Status = 'Failed',
                        Remarks = @Remarks
                    WHERE DividendId = @DividendId AND Status = 'Pending'", conn);

                cmd.Parameters.AddWithValue("@DividendId", id);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(remarks) ?
                    $"Marked as failed on {DateTime.Now}" :
                    remarks);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Payment not found or already processed" });
                }

                return Ok(new { message = "Payment marked as failed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to update payment status",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region Projection Endpoints

        [HttpGet("members/eligible-for-dividend")]
        public async Task<IActionResult> GetEligibleMembers([FromQuery] DateTime recordDate)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT 
                COUNT(DISTINCT s.MemberId) AS EligibleMembersCount,
                CAST(SUM(s.Units) AS DECIMAL(18,2)) AS TotalShares
            FROM Shares s
            JOIN Members m ON s.MemberId = m.MemberId
            WHERE s.Status = 'Active' 
            AND m.Status = 'Active'
            AND s.PurchaseDate <= @RecordDate", conn);

                // Use proper parameter formatting for SQL Server
                cmd.Parameters.Add(new SqlParameter("@RecordDate", SqlDbType.DateTime)
                {
                    Value = recordDate
                });

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var response = new EligibleMembersResponse
                    {
                        EligibleMembersCount = reader.GetInt32(0),
                        TotalShares = reader.GetDecimal(1)
                    };

                    return Ok(response);
                }

                return Ok(new EligibleMembersResponse
                {
                    EligibleMembersCount = 0,
                    TotalShares = 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to retrieve eligible members",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
        [HttpPost("calculate-projection")]
        public async Task<IActionResult> CalculateProjection([FromBody] ProjectionCalculatorDto calculator)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Calculate projection
                var totalDividends = calculator.EstimatedProfit * (calculator.PayoutRatio / 100);
                var perShareAmount = totalDividends / calculator.TotalShares;

                var result = new ProjectionResultDto
                {
                    TotalDividends = totalDividends,
                    DividendRate = perShareAmount,
                    PerShareAmount = perShareAmount,
                    PayoutAmount = totalDividends
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to calculate projection",
                    error = ex.Message
                });
            }
        }

        #endregion
    }

    public class ProcessDividendPaymentRequest
    {
        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; }
    }
}