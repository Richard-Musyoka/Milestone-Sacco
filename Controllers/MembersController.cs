using Microsoft.AspNetCore.Mvc;
using SaccoManagementSystem.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using System.ComponentModel.DataAnnotations;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/members")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private readonly string _connectionString;

        public MembersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/members
        [HttpGet]
        public async Task<IActionResult> GetMembers()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var members = await conn.QueryAsync<MemberDto>(@"
                    SELECT 
                        MemberId AS MemberId,
                        MemberNo AS MemberNo,
                        FirstName AS FirstName,
                        LastName AS LastName,
                        Email AS Email
                    FROM Members");

                return Ok(members);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving members: {ex.Message}");
            }
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllMembers()
        {
            var members = new List<MemberModel>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new SqlCommand("SELECT * FROM Members", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    members.Add(MapReaderToMember(reader));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve members.", error = ex.Message });
            }

            return Ok(members);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchMembers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var members = await conn.QueryAsync<MemberDto>(@"
                    SELECT 
                        MemberId AS MemberId, 
                        MemberNo AS MemberNo, 
                        FirstName AS FirstName, 
                        LastName AS LastName, 
                        Email AS Email 
                    FROM Members 
                    WHERE FirstName LIKE @query OR LastName LIKE @query OR MemberNo LIKE @query
                    ORDER BY LastName, FirstName
                    OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY",
                    new { query = $"%{query}%" });

                return Ok(members);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error searching members: {ex.Message}");
            }
        }

        [HttpGet("get-dropdown")]
        public async Task<IActionResult> GetMembersForDropdown()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var members = await conn.QueryAsync<MemberDropdownItem>(@"
                    SELECT 
                        MemberId AS MemberId, 
                        CONCAT(FirstName, ' ', LastName) AS FullName 
                    FROM Members");

                return Ok(members);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve members dropdown.", error = ex.Message });
            }
        }

        public class MemberDropdownItem
        {
            public int MemberId { get; set; }
            public string FullName { get; set; } = string.Empty;
        }

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetMemberById(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var member = await conn.QueryFirstOrDefaultAsync<MemberModel>(
                    "SELECT * FROM Members WHERE MemberId = @id",
                    new { id });

                if (member == null)
                    return NotFound(new { message = "Member not found." });

                return Ok(member);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve member.", error = ex.Message });
            }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddMember([FromBody] MemberModel member)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var sql = @"
                    INSERT INTO Members 
                        (MemberNo, FirstName, LastName, Email, PhoneNumber, NationalID, 
                         DateOfBirth, Gender, MaritalStatus, Occupation, Employer, Address, 
                         ProfileImageUrl, JoinDate, Status, CreatedDate, CreatedBy)
                    VALUES 
                        (@MemberNo, @FirstName, @LastName, @Email, @PhoneNumber, @NationalID, 
                         @DateOfBirth, @Gender, @MaritalStatus, @Occupation, @Employer, @Address, 
                         @ProfileImageUrl, @JoinDate, @Status, GETDATE(), @CreatedBy)";

                await conn.ExecuteAsync(sql, member);
                return Ok(new { message = "Member added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to add member.", error = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateMember(int id, [FromBody] MemberModel member)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var sql = @"
                    UPDATE Members SET 
                        FirstName = @FirstName, LastName = @LastName, Email = @Email, 
                        PhoneNumber = @PhoneNumber, NationalID = @NationalID, DateOfBirth = @DateOfBirth, 
                        Gender = @Gender, MaritalStatus = @MaritalStatus, Occupation = @Occupation, 
                        Employer = @Employer, Address = @Address, ProfileImageUrl = @ProfileImageUrl, 
                        JoinDate = @JoinDate, Status = @Status
                    WHERE MemberId = @MemberId";

                member.MemberId = id;
                var rowsAffected = await conn.ExecuteAsync(sql, member);

                if (rowsAffected == 0)
                    return NotFound(new { message = "Member not found." });

                return Ok(new { message = "Member updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update member.", error = ex.Message });
            }
        }

        [HttpGet("get-last-member-number")]
        public async Task<IActionResult> GetLastMemberNumber()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var lastNumber = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT TOP 1 MemberNo FROM Members ORDER BY MemberId DESC");

                return Ok(lastNumber ?? "TSC0000");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve last member number.", error = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMember(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var rowsAffected = await conn.ExecuteAsync(
                    "DELETE FROM Members WHERE MemberId = @id",
                    new { id });

                if (rowsAffected == 0)
                    return NotFound(new { message = "Member not found." });

                return Ok(new { message = "Member deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete member.", error = ex.Message });
            }
        }

        private MemberModel MapReaderToMember(SqlDataReader reader)
        {
            return new MemberModel
            {
                MemberId = reader["MemberId"] != DBNull.Value ? Convert.ToInt32(reader["MemberId"]) : 0,
                MemberNo = reader["MemberNo"]?.ToString() ?? string.Empty,
                FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                LastName = reader["LastName"]?.ToString() ?? string.Empty,
                Email = reader["Email"]?.ToString() ?? string.Empty,
                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? string.Empty,
                NationalID = reader["NationalID"]?.ToString() ?? string.Empty,
                DateOfBirth = reader["DateOfBirth"] != DBNull.Value ? Convert.ToDateTime(reader["DateOfBirth"]) : (DateTime?)null,
                Gender = reader["Gender"]?.ToString() ?? string.Empty,
                MaritalStatus = reader["MaritalStatus"]?.ToString() ?? string.Empty,
                Occupation = reader["Occupation"]?.ToString() ?? string.Empty,
                Employer = reader["Employer"]?.ToString() ?? string.Empty,
                Address = reader["Address"]?.ToString() ?? string.Empty,
                ProfileImageUrl = reader["ProfileImageUrl"]?.ToString() ?? string.Empty,
                JoinDate = reader["JoinDate"] != DBNull.Value ? Convert.ToDateTime(reader["JoinDate"]) : (DateTime?)null,
                Status = reader["Status"]?.ToString() ?? "Active",
                CreatedDate = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : (DateTime?)null,
                CreatedBy = reader["CreatedBy"] != DBNull.Value ? Convert.ToInt32(reader["CreatedBy"]) : (int?)null
            };
        }
    }

    public class MemberDto
    {
        public int MemberId { get; set; }
        public string MemberNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}