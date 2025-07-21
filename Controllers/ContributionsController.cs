using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SaccoManagementSystem.Models;
using System.Data;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/contributions")]
    [ApiController]
    public class ContributionsController : ControllerBase
    {
        private readonly string _connectionString;

        public ContributionsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllContributions()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                SELECT 
                    c.ContributionId,
                    c.MemberId,
                    m.FirstName + ' ' + m.LastName AS MemberName,
                    m.MemberNo,
                    c.ContributionType AS Type,
                    c.Amount,
                    c.DateContributed AS Date,
                    CASE 
                        WHEN c.Status = 'Confirmed' THEN 'Paid'
                        WHEN c.Status = 'Pending' THEN 'Pending'
                        WHEN c.Status = 'Overdue' THEN 'Overdue'
                        ELSE c.Status
                    END AS Status,
                    c.PaymentMethod,
                    c.TransactionRef,
                    c.Remarks
                FROM Contributions c
                JOIN Members m ON c.MemberId = m.MemberId
                ORDER BY c.DateContributed DESC", conn);

                var contributions = new List<ContributionViewModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    contributions.Add(new ContributionViewModel
                    {
                        ContributionId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        Type = reader.GetString(4),
                        Amount = reader.GetDecimal(5),
                        Date = reader.GetDateTime(6),
                        Status = reader.GetString(7),
                        PaymentMethod = reader.IsDBNull(8) ? null : reader.GetString(8),
                        TransactionRef = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Remarks = reader.IsDBNull(10) ? null : reader.GetString(10)
                    });
                }

                return Ok(contributions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve contributions.", error = ex.Message });
            }
        }

       [HttpPost("add")]
public async Task<IActionResult> AddContribution([FromBody] Contribution contribution)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    try
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand(@"
            INSERT INTO Contributions 
                (MemberId, ContributionType, Amount, DateContributed, PaymentMethod, 
                 TransactionRef, Status, Remarks, CreatedDate)
            VALUES 
                (@MemberId, @ContributionType, @Amount, @DateContributed, @PaymentMethod, 
                 @TransactionRef, @Status, @Remarks, @CreatedDate);
            SELECT SCOPE_IDENTITY();", conn);

        cmd.Parameters.AddWithValue("@MemberId", contribution.MemberId);
        cmd.Parameters.AddWithValue("@ContributionType", contribution.ContributionType ?? string.Empty);
        cmd.Parameters.AddWithValue("@Amount", contribution.Amount);
        cmd.Parameters.AddWithValue("@DateContributed", contribution.DateContributed);
        cmd.Parameters.AddWithValue("@PaymentMethod", (object?)contribution.PaymentMethod ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TransactionRef", (object?)contribution.TransactionRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", contribution.Status ?? "Pending");
        cmd.Parameters.AddWithValue("@Remarks", (object?)contribution.Remarks ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedDate", contribution.CreatedDate);

        var newId = await cmd.ExecuteScalarAsync();

        return Ok(new
        {
            message = "Contribution added successfully",
            contributionId = newId
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            message = "Failed to add contribution",
            error = ex.Message
        });
    }
}

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetContributionById(int id)
        {
            ContributionViewModel contribution = null;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT c.*, m.FirstName + ' ' + m.LastName AS MemberName,
                    CASE WHEN c.Status = 'Confirmed' THEN 'Paid' ELSE c.Status END AS Status
                    FROM Contributions c
                    JOIN Members m ON c.MemberId = m.MemberId
                    WHERE c.ContributionId = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    contribution = MapReaderToContribution(reader);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve contribution.", error = ex.Message });
            }

            if (contribution == null) return NotFound(new { message = "Contribution not found." });

            return Ok(contribution);
        }

       

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateContribution(int id, [FromBody] Contribution contribution)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE Contributions SET 
                        MemberId = @MemberId, 
                        ContributionType = @ContributionType, 
                        Amount = @Amount, 
                        DateContributed = @DateContributed, 
                        PaymentMethod = @PaymentMethod, 
                        TransactionRef = @TransactionRef, 
                        Status = @Status, 
                        Remarks = @Remarks
                    WHERE ContributionId = @ContributionId", conn);

                AddContributionParameters(cmd, contribution);
                cmd.Parameters.AddWithValue("@ContributionId", id);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    return NotFound(new { message = "Contribution not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update contribution.", error = ex.Message });
            }

            return Ok(new { message = "Contribution updated successfully." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteContribution(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand("DELETE FROM Contributions WHERE ContributionId = @ContributionId", conn);
                cmd.Parameters.AddWithValue("@ContributionId", id);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "Contribution not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete contribution.", error = ex.Message });
            }

            return Ok(new { message = "Contribution deleted successfully." });
        }

        private ContributionViewModel MapReaderToContribution(SqlDataReader reader)
        {
            return new ContributionViewModel
            {
                ContributionId = reader["ContributionId"] != DBNull.Value ? Convert.ToInt32(reader["ContributionId"]) : 0,
                MemberId = reader["MemberId"] != DBNull.Value ? Convert.ToInt32(reader["MemberId"]) : 0,
                MemberName = reader["MemberName"]?.ToString() ?? string.Empty,
                Type = reader["ContributionType"]?.ToString() ?? string.Empty,
                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                Date = reader["DateContributed"] != DBNull.Value ? Convert.ToDateTime(reader["DateContributed"]) : DateTime.Now,
                Status = reader["Status"]?.ToString() ?? "Pending",
                PaymentMethod = reader["PaymentMethod"]?.ToString(),
                TransactionRef = reader["TransactionRef"]?.ToString(),
                Remarks = reader["Remarks"]?.ToString()
            };
        }

        private void AddContributionParameters(SqlCommand cmd, Contribution contribution)
        {
            cmd.Parameters.AddWithValue("@MemberId", contribution.MemberId);
            cmd.Parameters.AddWithValue("@ContributionType", contribution.ContributionType ?? string.Empty);
            cmd.Parameters.AddWithValue("@Amount", contribution.Amount);
            cmd.Parameters.AddWithValue("@DateContributed", contribution.DateContributed);
            cmd.Parameters.AddWithValue("@PaymentMethod", (object?)contribution.PaymentMethod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TransactionRef", (object?)contribution.TransactionRef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", (object?)contribution.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", contribution.Status == "Confirmed" ? "Paid" : contribution.Status);
            cmd.Parameters.AddWithValue("@Remarks", (object?)contribution.Remarks ?? DBNull.Value);
        }
    }
}
