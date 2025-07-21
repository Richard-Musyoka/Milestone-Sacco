using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using SaccoManagementSystem.Models;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

namespace SaccoManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuarantorsController : ControllerBase
    {
        private readonly string _connectionString;

        public GuarantorsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/guarantors/potential
        [HttpGet("potential")]
        public async Task<IActionResult> GetPotentialGuarantors()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var guarantors = await conn.QueryAsync<MemberDto>(@"
                    SELECT 
                        MemberId, 
                        MemberNo, 
                        FirstName, 
                        LastName, 
                        Email 
                    FROM Members 
                    WHERE IsActive = 1 AND Shares >= 100
                    ORDER BY LastName, FirstName");

                return Ok(guarantors);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving potential guarantors: {ex.Message}");
            }
        }

        // GET: api/Guarantors
        [HttpGet]
        public async Task<IActionResult> GetAllGuarantors()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var guarantors = await conn.QueryAsync<Guarantor>("SELECT * FROM Guarantors ORDER BY LastName, FirstName");
                return Ok(guarantors);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving guarantors: {ex.Message}");
            }
        }

        // GET: api/Guarantors/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGuarantor(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var guarantor = await conn.QueryFirstOrDefaultAsync<Guarantor>(
                    "SELECT * FROM Guarantors WHERE GuarantorId = @Id", new { Id = id });

                if (guarantor == null)
                {
                    return NotFound();
                }

                return Ok(guarantor);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving guarantor: {ex.Message}");
            }
        }

        // POST: api/Guarantors
        [HttpPost]
        public async Task<IActionResult> CreateGuarantor([FromBody] GuarantorCreateDto guarantorDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);

                var sql = @"
                    INSERT INTO Guarantors 
                        (FirstName, LastName, PhoneNumber, Email, DateOfBirth, 
                         IDNumber, PhysicalAddress, IsActive, CreatedDate, CreatedBy, Remarks)
                    VALUES 
                        (@FirstName, @LastName, @PhoneNumber, @Email, @DateOfBirth, 
                         @IDNumber, @PhysicalAddress, 1, GETDATE(), @CreatedBy, @Remarks);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                var guarantorId = await conn.ExecuteScalarAsync<int>(sql,
                    new
                    {
                        guarantorDto.FirstName,
                        guarantorDto.LastName,
                        guarantorDto.PhoneNumber,
                        guarantorDto.Email,
                        guarantorDto.DateOfBirth,
                        guarantorDto.IDNumber,
                        guarantorDto.PhysicalAddress,
                        guarantorDto.CreatedBy,
                        guarantorDto.Remarks
                    });

                return CreatedAtAction(nameof(GetGuarantor), new { id = guarantorId },
                    new { message = "Guarantor created successfully", guarantorId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating guarantor: {ex.Message}");
            }
        }

        // PUT: api/Guarantors/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGuarantor(int id, [FromBody] GuarantorUpdateDto guarantorDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);

                var affectedRows = await conn.ExecuteAsync(@"
                    UPDATE Guarantors SET
                        FirstName = @FirstName,
                        LastName = @LastName,
                        PhoneNumber = @PhoneNumber,
                        Email = @Email,
                        DateOfBirth = @DateOfBirth,
                        IDNumber = @IDNumber,
                        PhysicalAddress = @PhysicalAddress,
                        LastModifiedDate = GETDATE(),
                        IsActive = @IsActive,
                        Remarks = @Remarks
                    WHERE GuarantorId = @GuarantorId",
                    new
                    {
                        GuarantorId = id,
                        guarantorDto.FirstName,
                        guarantorDto.LastName,
                        guarantorDto.PhoneNumber,
                        guarantorDto.Email,
                        guarantorDto.DateOfBirth,
                        guarantorDto.IDNumber,
                        guarantorDto.PhysicalAddress,
                        guarantorDto.IsActive,
                        guarantorDto.Remarks
                    });

                if (affectedRows == 0)
                {
                    return NotFound();
                }

                return Ok(new { message = "Guarantor updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating guarantor: {ex.Message}");
            }
        }

        // DELETE: api/Guarantors/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGuarantor(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // First check if guarantor is used in any loans
                var isUsed = await conn.ExecuteScalarAsync<bool>(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM LoanGuarantors WHERE GuarantorId = @Id) THEN 1 ELSE 0 END",
                    new { Id = id });

                if (isUsed)
                {
                    return BadRequest("Cannot delete guarantor - they are associated with existing loans");
                }

                var affectedRows = await conn.ExecuteAsync(
                    "DELETE FROM Guarantors WHERE GuarantorId = @Id",
                    new { Id = id });

                if (affectedRows == 0)
                {
                    return NotFound();
                }

                return Ok(new { message = "Guarantor deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting guarantor: {ex.Message}");
            }
        }

        // GET: api/Guarantors/search?query=john
        [HttpGet("search")]
        public async Task<IActionResult> SearchGuarantors([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest("Search query must be at least 2 characters");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var guarantors = await conn.QueryAsync<GuarantorSearchResult>(@"
                    SELECT TOP 20
                        GuarantorId, FirstName, LastName, IDNumber, 
                        PhoneNumber, Email, PhysicalAddress
                    FROM Guarantors 
                    WHERE 
                        FirstName LIKE @Query OR 
                        LastName LIKE @Query OR 
                        IDNumber LIKE @Query OR
                        PhoneNumber LIKE @Query
                    ORDER BY LastName, FirstName",
                    new { Query = $"%{query}%" });

                return Ok(guarantors);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error searching guarantors: {ex.Message}");
            }
        }
    }

    // DTO Classes
  

    public class GuarantorCreateDto
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [Required]
        [StringLength(50)]
        public string IDNumber { get; set; }

        [StringLength(200)]
        public string PhysicalAddress { get; set; }

       

        public long? CreatedBy { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; }
    }

    public class GuarantorUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [Required]
        [StringLength(50)]
        public string IDNumber { get; set; }

        [StringLength(200)]
        public string PhysicalAddress { get; set; }



        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string Remarks { get; set; }
    }

    public class GuarantorSearchResult
    {
        public int GuarantorId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string IDNumber { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string PhysicalAddress { get; set; }
    }
}