using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SaccoManagementSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/shares")]
    [ApiController]
    public class SharesController : ControllerBase
    {
        private readonly string _connectionString;

        public SharesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllShares()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        s.ShareId,
                        s.MemberId,
                        m.FirstName + ' ' + m.LastName AS MemberName,
                        m.MemberNo,
                        s.Units,
                        s.UnitPrice,
                        s.TotalValue,
                        s.PurchaseDate,
                        s.Status,
                        s.ShareType,
                        s.Remarks,
                        s.CreatedDate
                    FROM Shares s
                    JOIN Members m ON s.MemberId = m.MemberId
                    ORDER BY s.PurchaseDate DESC", conn);

                var shares = new List<ShareViewModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    shares.Add(new ShareViewModel
                    {
                        ShareId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNumber = reader.GetString(3),
                        Units = reader.GetInt32(4),
                        UnitPrice = reader.GetDecimal(5),
                        TotalValue = reader.GetDecimal(6),
                        PurchaseDate = reader.GetDateTime(7),
                        Status = reader.GetString(8),
                        ShareType = reader.GetString(9),
                        Remarks = reader.IsDBNull(10) ? null : reader.GetString(10),
                        CreatedDate = reader.GetDateTime(11)
                    });
                }

                return Ok(shares);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve shares.", error = ex.Message });
            }
        }

        [HttpGet("member/{memberId}")]
        public async Task<IActionResult> GetMemberShares(int memberId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        s.ShareId,
                        s.MemberId,
                        m.FirstName + ' ' + m.LastName AS MemberName,
                        m.MemberNo,
                        s.Units,
                        s.UnitPrice,
                        s.TotalValue,
                        s.PurchaseDate,
                        s.Status,
                        s.ShareType,
                        s.Remarks,
                        s.CreatedDate
                    FROM Shares s
                    JOIN Members m ON s.MemberId = m.MemberId
                    WHERE s.MemberId = @MemberId
                    ORDER BY s.PurchaseDate DESC", conn);

                cmd.Parameters.AddWithValue("@MemberId", memberId);

                var shares = new List<ShareViewModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    shares.Add(new ShareViewModel
                    {
                        ShareId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNumber = reader.GetString(3),
                        Units = reader.GetInt32(4),
                        UnitPrice = reader.GetDecimal(5),
                        TotalValue = reader.GetDecimal(6),
                        PurchaseDate = reader.GetDateTime(7),
                        Status = reader.GetString(8),
                        ShareType = reader.GetString(9),
                        Remarks = reader.IsDBNull(10) ? null : reader.GetString(10),
                        CreatedDate = reader.GetDateTime(11)
                    });
                }

                return Ok(shares);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve member shares.", error = ex.Message });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSharesSummary()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        SUM(Units) AS TotalShares,
                        SUM(TotalValue) AS TotalValue,
                        COUNT(DISTINCT MemberId) AS ShareholdersCount,
                        (SELECT TOP 1 UnitPrice FROM Shares WHERE Status = 'Active' ORDER BY PurchaseDate DESC) AS CurrentSharePrice
                    FROM Shares
                    WHERE Status = 'Active'", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var summary = new ShareSummaryDto
                    {
                        TotalShares = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        TotalValue = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        ShareholdersCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        CurrentSharePrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
                    };

                    return Ok(summary);
                }

                return Ok(new ShareSummaryDto());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve shares summary.", error = ex.Message });
            }
        }

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetShareById(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT 
                        s.ShareId,
                        s.MemberId,
                        m.FirstName + ' ' + m.LastName AS MemberName,
                        m.MemberNo,
                        s.Units,
                        s.UnitPrice,
                        s.TotalValue,
                        s.PurchaseDate,
                        s.Status,
                        s.ShareType,
                        s.Remarks,
                        s.CreatedDate
                    FROM Shares s
                    JOIN Members m ON s.MemberId = m.MemberId
                    WHERE s.ShareId = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var share = new ShareViewModel
                    {
                        ShareId = reader.GetInt32(0),
                        MemberId = reader.GetInt32(1),
                        MemberName = reader.GetString(2),
                        MemberNumber = reader.GetString(3),
                        Units = reader.GetInt32(4),
                        UnitPrice = reader.GetDecimal(5),
                        TotalValue = reader.GetDecimal(6),
                        PurchaseDate = reader.GetDateTime(7),
                        Status = reader.GetString(8),
                        ShareType = reader.GetString(9),
                        Remarks = reader.IsDBNull(10) ? null : reader.GetString(10),
                        CreatedDate = reader.GetDateTime(11)
                    };

                    return Ok(share);
                }

                return NotFound(new { message = "Share not found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve share.", error = ex.Message });
            }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddShare([FromBody] SharePurchaseDto purchaseDto)
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
                if (!await MemberExists(conn, purchaseDto.MemberId))
                {
                    return BadRequest("Specified member does not exist");
                }

                var cmd = new SqlCommand(@"
                    INSERT INTO Shares 
                        (MemberId, Units, UnitPrice, PurchaseDate, Status, ShareType, Remarks, CreatedDate)
                    VALUES 
                        (@MemberId, @Units, @UnitPrice, @PurchaseDate, @Status, @ShareType, @Remarks, @CreatedDate);
                    SELECT SCOPE_IDENTITY();", conn);

                cmd.Parameters.AddWithValue("@MemberId", purchaseDto.MemberId);
                cmd.Parameters.AddWithValue("@Units", purchaseDto.Units);
                cmd.Parameters.AddWithValue("@UnitPrice", purchaseDto.UnitPrice);
                cmd.Parameters.AddWithValue("@PurchaseDate", purchaseDto.PurchaseDate);
                cmd.Parameters.AddWithValue("@Status", purchaseDto.Status ?? "Active");
                cmd.Parameters.AddWithValue("@ShareType", purchaseDto.ShareType);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(purchaseDto.Remarks) ? DBNull.Value : (object)purchaseDto.Remarks);
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                var newId = await cmd.ExecuteScalarAsync();

                return Ok(new
                {
                    message = "Share purchase recorded successfully",
                    shareId = newId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to record share purchase", error = ex.Message });
            }
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> TransferShares([FromBody] ShareTransferDto transferDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if from member has enough shares
                var fromMemberShares = await GetMemberActiveShares(conn, transferDto.FromMemberId);
                if (fromMemberShares < transferDto.Units)
                {
                    return BadRequest("Insufficient shares to transfer");
                }

                // Validate members exist
                if (!await MemberExists(conn, transferDto.FromMemberId))
                {
                    return BadRequest("From member does not exist");
                }

                if (!await MemberExists(conn, transferDto.ToMemberId))
                {
                    return BadRequest("To member does not exist");
                }

                // Get shares to transfer (FIFO)
                var sharesToTransfer = await GetSharesForTransfer(conn, transferDto.FromMemberId, transferDto.Units);

                using var transaction = conn.BeginTransaction();
                try
                {
                    // Transfer shares
                    foreach (var share in sharesToTransfer)
                    {
                        if (share.UnitsToTransfer > 0)
                        {
                            // Update original share (reduce units or mark as transferred)
                            var updateCmd = new SqlCommand(
                                share.UnitsToTransfer == share.OriginalUnits
                                    ? "UPDATE Shares SET Status = 'Transferred', Remarks = @Remarks WHERE ShareId = @ShareId"
                                    : "UPDATE Shares SET Units = Units - @Units, Remarks = @Remarks WHERE ShareId = @ShareId",
                                conn, transaction);

                            updateCmd.Parameters.AddWithValue("@ShareId", share.ShareId);
                            updateCmd.Parameters.AddWithValue("@Units", share.UnitsToTransfer);
                            updateCmd.Parameters.AddWithValue("@Remarks",
                                $"Transferred {share.UnitsToTransfer} units to member {transferDto.ToMemberId}. " +
                                (string.IsNullOrEmpty(transferDto.Remarks) ? "" : transferDto.Remarks));

                            await updateCmd.ExecuteNonQueryAsync();

                            // Create new share for recipient
                            var insertCmd = new SqlCommand(@"
                                INSERT INTO Shares 
                                    (MemberId, Units, UnitPrice, PurchaseDate, Status, ShareType, Remarks, CreatedDate)
                                VALUES 
                                    (@MemberId, @Units, @UnitPrice, @PurchaseDate, @Status, @ShareType, @Remarks, @CreatedDate)",
                                conn, transaction);

                            insertCmd.Parameters.AddWithValue("@MemberId", transferDto.ToMemberId);
                            insertCmd.Parameters.AddWithValue("@Units", share.UnitsToTransfer);
                            insertCmd.Parameters.AddWithValue("@UnitPrice", share.UnitPrice);
                            insertCmd.Parameters.AddWithValue("@PurchaseDate", DateTime.Now);
                            insertCmd.Parameters.AddWithValue("@Status", "Active");
                            insertCmd.Parameters.AddWithValue("@ShareType", transferDto.ShareType);
                            insertCmd.Parameters.AddWithValue("@Remarks",
                                $"Transferred from member {transferDto.FromMemberId}. " +
                                (string.IsNullOrEmpty(transferDto.Remarks) ? "" : transferDto.Remarks));
                            insertCmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();
                    return Ok(new { message = "Shares transferred successfully" });
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to transfer shares", error = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateShare(int id, [FromBody] ShareUpdateDto updateDto)
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
                    UPDATE Shares SET 
                        Units = @Units,
                        UnitPrice = @UnitPrice,
                        PurchaseDate = @PurchaseDate,
                        Status = @Status,
                        ShareType = @ShareType,
                        Remarks = @Remarks
                    WHERE ShareId = @ShareId", conn);

                cmd.Parameters.AddWithValue("@ShareId", id);
                cmd.Parameters.AddWithValue("@Units", updateDto.Units);
                cmd.Parameters.AddWithValue("@UnitPrice", updateDto.UnitPrice);
                cmd.Parameters.AddWithValue("@PurchaseDate", updateDto.PurchaseDate);
                cmd.Parameters.AddWithValue("@Status", updateDto.Status);
                cmd.Parameters.AddWithValue("@ShareType", updateDto.ShareType);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(updateDto.Remarks) ? DBNull.Value : (object)updateDto.Remarks);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Share not found." });
                }

                return Ok(new { message = "Share updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update share", error = ex.Message });
            }
        }

        [HttpDelete("cancel/{id}")]
        public async Task<IActionResult> CancelShare(int id, [FromBody] string remarks)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE Shares SET 
                        Status = 'Cancelled',
                        Remarks = @Remarks
                    WHERE ShareId = @ShareId AND Status = 'Active'", conn);

                cmd.Parameters.AddWithValue("@ShareId", id);
                cmd.Parameters.AddWithValue("@Remarks", $"Cancelled on {DateTime.Now}. " + (string.IsNullOrEmpty(remarks) ? "" : remarks));

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return NotFound(new { message = "Share not found or not active." });
                }

                return Ok(new { message = "Share cancelled successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to cancel share", error = ex.Message });
            }
        }

        private async Task<bool> MemberExists(SqlConnection conn, int memberId)
        {
            var cmd = new SqlCommand("SELECT 1 FROM Members WHERE MemberId = @MemberId", conn);
            cmd.Parameters.AddWithValue("@MemberId", memberId);
            return await cmd.ExecuteScalarAsync() != null;
        }

        private async Task<int> GetMemberActiveShares(SqlConnection conn, int memberId)
        {
            var cmd = new SqlCommand("SELECT SUM(Units) FROM Shares WHERE MemberId = @MemberId AND Status = 'Active'", conn);
            cmd.Parameters.AddWithValue("@MemberId", memberId);
            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private async Task<List<ShareTransferInfo>> GetSharesForTransfer(SqlConnection conn, int memberId, int unitsToTransfer)
        {
            var shares = new List<ShareTransferInfo>();
            var cmd = new SqlCommand(
                "SELECT ShareId, Units, UnitPrice, ShareType FROM Shares " +
                "WHERE MemberId = @MemberId AND Status = 'Active' " +
                "ORDER BY PurchaseDate", conn);

            cmd.Parameters.AddWithValue("@MemberId", memberId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync() && unitsToTransfer > 0)
            {
                var shareId = reader.GetInt32(0);
                var availableUnits = reader.GetInt32(1);
                var unitPrice = reader.GetDecimal(2);
                var shareType = reader.GetString(3);

                var units = Math.Min(availableUnits, unitsToTransfer);
                shares.Add(new ShareTransferInfo
                {
                    ShareId = shareId,
                    OriginalUnits = availableUnits,
                    UnitsToTransfer = units,
                    UnitPrice = unitPrice,
                    ShareType = shareType
                });

                unitsToTransfer -= units;
            }

            return shares;
        }

        private class ShareTransferInfo
        {
            public int ShareId { get; set; }
            public int OriginalUnits { get; set; }
            public int UnitsToTransfer { get; set; }
            public decimal UnitPrice { get; set; }
            public string ShareType { get; set; }
        }
    }
}