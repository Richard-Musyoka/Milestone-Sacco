using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sacco_Management_System.Models;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(IConfiguration configuration, ILogger<SettingsController> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<SaccoSettings>> Get()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT TOP 1 * FROM SaccoSettings ORDER BY Id DESC";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return Ok(MapSettingsFromReader(reader));
            }

            return Ok(SaccoSettings.GetDefaultSettings());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SACCO settings");
            return StatusCode(500, "An error occurred while retrieving settings");
        }
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SaccoSettings settings)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if settings exist
            var checkQuery = "SELECT TOP 1 Id FROM SaccoSettings ORDER BY Id DESC";
            var settingsId = await new SqlCommand(checkQuery, connection).ExecuteScalarAsync();

            string query;
            if (settingsId == null)
            {
                query = @"
                INSERT INTO SaccoSettings (
                    SACCOName, RegistrationNumber, FiscalYearStart, FiscalYearEnd, 
                    Currency, Timezone, SharePrice, DividendRate, MinimumShares, 
                    MaximumShares, InterestCalculationMethod, PenaltyRate,
                    MaxLoanAmountMultiple, LoanProcessingFee, LoanInterestRate,
                    MaxLoanRepaymentPeriod, MinGuarantorsRequired, LoanEligibilityPeriod,
                    EmailNotificationsEnabled, SMSNotificationsEnabled, DefaultNotificationEmail,
                    SMSSenderId, ContributionReminderDays, LoanDueReminderDays,
                    PasswordExpiryDays, FailedAttemptsBeforeLockout, SessionTimeoutMinutes,
                    TwoFactorAuthenticationRequired, PasswordComplexity, PasswordHistoryCount,
                    CreatedBy
                ) VALUES (
                    @SACCOName, @RegistrationNumber, @FiscalYearStart, @FiscalYearEnd, 
                    @Currency, @Timezone, @SharePrice, @DividendRate, @MinimumShares, 
                    @MaximumShares, @InterestCalculationMethod, @PenaltyRate,
                    @MaxLoanAmountMultiple, @LoanProcessingFee, @LoanInterestRate,
                    @MaxLoanRepaymentPeriod, @MinGuarantorsRequired, @LoanEligibilityPeriod,
                    @EmailNotificationsEnabled, @SMSNotificationsEnabled, @DefaultNotificationEmail,
                    @SMSSenderId, @ContributionReminderDays, @LoanDueReminderDays,
                    @PasswordExpiryDays, @FailedAttemptsBeforeLockout, @SessionTimeoutMinutes,
                    @TwoFactorAuthenticationRequired, @PasswordComplexity, @PasswordHistoryCount,
                    1  -- CreatedBy (replace with actual user ID)
                )";
            }
            else
            {
                query = @"
                UPDATE SaccoSettings SET
                    SACCOName = @SACCOName,
                    RegistrationNumber = @RegistrationNumber,
                    FiscalYearStart = @FiscalYearStart,
                    FiscalYearEnd = @FiscalYearEnd,
                    Currency = @Currency,
                    Timezone = @Timezone,
                    SharePrice = @SharePrice,
                    DividendRate = @DividendRate,
                    MinimumShares = @MinimumShares,
                    MaximumShares = @MaximumShares,
                    InterestCalculationMethod = @InterestCalculationMethod,
                    PenaltyRate = @PenaltyRate,
                    MaxLoanAmountMultiple = @MaxLoanAmountMultiple,
                    LoanProcessingFee = @LoanProcessingFee,
                    LoanInterestRate = @LoanInterestRate,
                    MaxLoanRepaymentPeriod = @MaxLoanRepaymentPeriod,
                    MinGuarantorsRequired = @MinGuarantorsRequired,
                    LoanEligibilityPeriod = @LoanEligibilityPeriod,
                    EmailNotificationsEnabled = @EmailNotificationsEnabled,
                    SMSNotificationsEnabled = @SMSNotificationsEnabled,
                    DefaultNotificationEmail = @DefaultNotificationEmail,
                    SMSSenderId = @SMSSenderId,
                    ContributionReminderDays = @ContributionReminderDays,
                    LoanDueReminderDays = @LoanDueReminderDays,
                    PasswordExpiryDays = @PasswordExpiryDays,
                    FailedAttemptsBeforeLockout = @FailedAttemptsBeforeLockout,
                    SessionTimeoutMinutes = @SessionTimeoutMinutes,
                    TwoFactorAuthenticationRequired = @TwoFactorAuthenticationRequired,
                    PasswordComplexity = @PasswordComplexity,
                    PasswordHistoryCount = @PasswordHistoryCount,
                    ModifiedDate = GETDATE(),
                    ModifiedBy = 1  -- Replace with actual user ID
                WHERE Id = @Id";

                using var idCommand = new SqlCommand("SELECT TOP 1 Id FROM SaccoSettings ORDER BY Id DESC", connection);
                settingsId = await idCommand.ExecuteScalarAsync();
            }

            using var command = new SqlCommand(query, connection);
            if (settingsId != null)
            {
                command.Parameters.AddWithValue("@Id", settingsId);
            }
            AddSettingsParameters(command, settings);

            await command.ExecuteNonQueryAsync();

            return Ok(new { message = "Settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving SACCO settings");
            return StatusCode(500, "An error occurred while saving settings");
        }
    }

    private SaccoSettings MapSettingsFromReader(SqlDataReader reader)
    {
        return new SaccoSettings
        {
            // General Settings
            SACCOName = reader.GetString(reader.GetOrdinal("SACCOName")),
            RegistrationNumber = reader.GetString(reader.GetOrdinal("RegistrationNumber")),
            FiscalYearStart = reader.GetDateTime(reader.GetOrdinal("FiscalYearStart")),
            FiscalYearEnd = reader.GetDateTime(reader.GetOrdinal("FiscalYearEnd")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            Timezone = reader.GetString(reader.GetOrdinal("Timezone")),

            // Financial Settings
            SharePrice = reader.GetDecimal(reader.GetOrdinal("SharePrice")),
            DividendRate = reader.GetDecimal(reader.GetOrdinal("DividendRate")),
            MinimumShares = reader.GetInt32(reader.GetOrdinal("MinimumShares")),
            MaximumShares = reader.GetInt32(reader.GetOrdinal("MaximumShares")),
            InterestCalculationMethod = reader.GetString(reader.GetOrdinal("InterestCalculationMethod")),
            PenaltyRate = reader.GetDecimal(reader.GetOrdinal("PenaltyRate")),

            // Loan Settings
            MaxLoanAmountMultiple = reader.GetInt32(reader.GetOrdinal("MaxLoanAmountMultiple")),
            LoanProcessingFee = reader.GetDecimal(reader.GetOrdinal("LoanProcessingFee")),
            LoanInterestRate = reader.GetDecimal(reader.GetOrdinal("LoanInterestRate")),
            MaxLoanRepaymentPeriod = reader.GetInt32(reader.GetOrdinal("MaxLoanRepaymentPeriod")),
            MinGuarantorsRequired = reader.GetInt32(reader.GetOrdinal("MinGuarantorsRequired")),
            LoanEligibilityPeriod = reader.GetInt32(reader.GetOrdinal("LoanEligibilityPeriod")),

            // Notification Settings
            EmailNotificationsEnabled = reader.GetBoolean(reader.GetOrdinal("EmailNotificationsEnabled")),
            SMSNotificationsEnabled = reader.GetBoolean(reader.GetOrdinal("SMSNotificationsEnabled")),
            DefaultNotificationEmail = reader.IsDBNull(reader.GetOrdinal("DefaultNotificationEmail")) ?
                string.Empty : reader.GetString(reader.GetOrdinal("DefaultNotificationEmail")),
            SMSSenderId = reader.IsDBNull(reader.GetOrdinal("SMSSenderId")) ?
                string.Empty : reader.GetString(reader.GetOrdinal("SMSSenderId")),
            ContributionReminderDays = reader.GetInt32(reader.GetOrdinal("ContributionReminderDays")),
            LoanDueReminderDays = reader.GetInt32(reader.GetOrdinal("LoanDueReminderDays")),

            // Security Settings
            PasswordExpiryDays = reader.GetInt32(reader.GetOrdinal("PasswordExpiryDays")),
            FailedAttemptsBeforeLockout = reader.GetInt32(reader.GetOrdinal("FailedAttemptsBeforeLockout")),
            SessionTimeoutMinutes = reader.GetInt32(reader.GetOrdinal("SessionTimeoutMinutes")),
            TwoFactorAuthenticationRequired = reader.GetBoolean(reader.GetOrdinal("TwoFactorAuthenticationRequired")),
            PasswordComplexity = reader.GetString(reader.GetOrdinal("PasswordComplexity")),
            PasswordHistoryCount = reader.GetInt32(reader.GetOrdinal("PasswordHistoryCount"))
        };
    }

    private void AddSettingsParameters(SqlCommand command, SaccoSettings settings)
    {
        // General Settings
        command.Parameters.AddWithValue("@SACCOName", settings.SACCOName);
        command.Parameters.AddWithValue("@RegistrationNumber", settings.RegistrationNumber);
        command.Parameters.AddWithValue("@FiscalYearStart", settings.FiscalYearStart);
        command.Parameters.AddWithValue("@FiscalYearEnd", settings.FiscalYearEnd);
        command.Parameters.AddWithValue("@Currency", settings.Currency);
        command.Parameters.AddWithValue("@Timezone", settings.Timezone);

        // Financial Settings
        command.Parameters.AddWithValue("@SharePrice", settings.SharePrice);
        command.Parameters.AddWithValue("@DividendRate", settings.DividendRate);
        command.Parameters.AddWithValue("@MinimumShares", settings.MinimumShares);
        command.Parameters.AddWithValue("@MaximumShares", settings.MaximumShares);
        command.Parameters.AddWithValue("@InterestCalculationMethod", settings.InterestCalculationMethod);
        command.Parameters.AddWithValue("@PenaltyRate", settings.PenaltyRate);

        // Loan Settings
        command.Parameters.AddWithValue("@MaxLoanAmountMultiple", settings.MaxLoanAmountMultiple);
        command.Parameters.AddWithValue("@LoanProcessingFee", settings.LoanProcessingFee);
        command.Parameters.AddWithValue("@LoanInterestRate", settings.LoanInterestRate);
        command.Parameters.AddWithValue("@MaxLoanRepaymentPeriod", settings.MaxLoanRepaymentPeriod);
        command.Parameters.AddWithValue("@MinGuarantorsRequired", settings.MinGuarantorsRequired);
        command.Parameters.AddWithValue("@LoanEligibilityPeriod", settings.LoanEligibilityPeriod);

        // Notification Settings
        command.Parameters.AddWithValue("@EmailNotificationsEnabled", settings.EmailNotificationsEnabled);
        command.Parameters.AddWithValue("@SMSNotificationsEnabled", settings.SMSNotificationsEnabled);
        command.Parameters.AddWithValue("@DefaultNotificationEmail",
            string.IsNullOrEmpty(settings.DefaultNotificationEmail) ? DBNull.Value : settings.DefaultNotificationEmail);
        command.Parameters.AddWithValue("@SMSSenderId",
            string.IsNullOrEmpty(settings.SMSSenderId) ? DBNull.Value : settings.SMSSenderId);
        command.Parameters.AddWithValue("@ContributionReminderDays", settings.ContributionReminderDays);
        command.Parameters.AddWithValue("@LoanDueReminderDays", settings.LoanDueReminderDays);

        // Security Settings
        command.Parameters.AddWithValue("@PasswordExpiryDays", settings.PasswordExpiryDays);
        command.Parameters.AddWithValue("@FailedAttemptsBeforeLockout", settings.FailedAttemptsBeforeLockout);
        command.Parameters.AddWithValue("@SessionTimeoutMinutes", settings.SessionTimeoutMinutes);
        command.Parameters.AddWithValue("@TwoFactorAuthenticationRequired", settings.TwoFactorAuthenticationRequired);
        command.Parameters.AddWithValue("@PasswordComplexity", settings.PasswordComplexity);
        command.Parameters.AddWithValue("@PasswordHistoryCount", settings.PasswordHistoryCount);
    }
}