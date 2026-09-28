using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SaccoManagementSystem.Models;

namespace SaccoManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;

        public AuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel login)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // 🔓 Plain text password check (temporary for testing)
                    string sql = "SELECT TOP 1 Id, Email FROM Users WHERE Email = @Email AND Password = @Password";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar).Value = login.Email;
                        cmd.Parameters.Add("@Password", System.Data.SqlDbType.NVarChar).Value = login.Password;  // ❌ No hashing here

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var userId = reader["Id"].ToString();
                                var email = reader["Email"].ToString();

                                return Ok(new
                                {
                                    success = true,
                                    message = "Login successful",
                                    user = new { id = userId, email = email }
                                });
                            }
                            else
                            {
                                return Unauthorized(new { success = false, message = "Invalid credentials" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Login failed", error = ex.Message });
            }
        }


        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterModel register)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(register.FirstName) ||
                    string.IsNullOrWhiteSpace(register.LastName) ||
                    string.IsNullOrWhiteSpace(register.Email) ||
                    string.IsNullOrWhiteSpace(register.Password))
                {
                    return BadRequest(new { success = false, message = "Please fill all required fields." });
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Check for existing email
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn);
                    checkCmd.Parameters.AddWithValue("@Email", register.Email);
                    int exists = (int)checkCmd.ExecuteScalar();

                    if (exists > 0)
                    {
                        return Conflict(new { success = false, message = "User with this email already exists." });
                    }

                    // Insert new user
                    var insertCmd = new SqlCommand(@"
                        INSERT INTO Users (FirstName, MiddleName, LastName, UserName, Password, Email, PhoneNumber, CreatedDate)
                        VALUES (@FirstName, @MiddleName, @LastName, @UserName, @Password, @Email, @PhoneNumber, GETDATE())", conn);

                    insertCmd.Parameters.AddWithValue("@FirstName", register.FirstName);
                    insertCmd.Parameters.AddWithValue("@MiddleName", string.IsNullOrEmpty(register.MiddleName) ? (object)DBNull.Value : register.MiddleName);
                    insertCmd.Parameters.AddWithValue("@LastName", register.LastName);
                    insertCmd.Parameters.AddWithValue("@UserName", string.IsNullOrEmpty(register.UserName) ? register.Email : register.UserName);
                    insertCmd.Parameters.AddWithValue("@Password", register.Password);
                    insertCmd.Parameters.AddWithValue("@Email", register.Email);
                    insertCmd.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrEmpty(register.PhoneNumber) ? (object)DBNull.Value : register.PhoneNumber);

                    int rows = insertCmd.ExecuteNonQuery();

                    if (rows > 0)
                    {
                        return Ok(new { success = true, message = "Registration successful!" });
                    }
                    else
                    {
                        return StatusCode(500, new { success = false, message = "Registration failed. Please try again." });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred.", error = ex.Message });
            }
        }


        [HttpGet("test-connection")]
        public IActionResult TestConnection()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    return Ok(new { success = true, message = "Connection successful!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Connection failed", error = ex.Message });
            }
        }
    }

    public class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
