using System;
using System.ComponentModel.DataAnnotations;

namespace Sacco_Management_System.Models
{
    public class SaccoSettings : ICloneable
    {
        // General Settings
        [Required(ErrorMessage = "SACCO name is required")]
        public string SACCOName { get; set; } = "Our SACCO";

        [Required(ErrorMessage = "Registration number is required")]
        public string RegistrationNumber { get; set; } = "";

        [Required(ErrorMessage = "Fiscal year start date is required")]
        public DateTime FiscalYearStart { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);

        [Required(ErrorMessage = "Fiscal year end date is required")]
        public DateTime FiscalYearEnd { get; set; } = new DateTime(DateTime.Now.Year, 12, 31);

        [Required(ErrorMessage = "Currency is required")]
        public string Currency { get; set; } = "KES";

        [Required(ErrorMessage = "Timezone is required")]
        public string Timezone { get; set; } = "Africa/Nairobi";

        // Financial Settings
        [Required]
        [Range(10, 10000, ErrorMessage = "Share price must be between 10 and 10,000")]
        public decimal SharePrice { get; set; } = 100;

        [Required]
        [Range(0, 100, ErrorMessage = "Dividend rate must be between 0 and 100")]
        public decimal DividendRate { get; set; } = 5;

        [Required]
        [Range(1, 1000, ErrorMessage = "Minimum shares must be between 1 and 1,000")]
        public int MinimumShares { get; set; } = 10;

        [Required]
        [Range(1, 100000, ErrorMessage = "Maximum shares must be between 1 and 100,000")]
        public int MaximumShares { get; set; } = 1000;

        [Required(ErrorMessage = "Interest calculation method is required")]
        public string InterestCalculationMethod { get; set; } = "Reducing Balance";

        [Required]
        [Range(0, 100, ErrorMessage = "Penalty rate must be between 0 and 100")]
        public decimal PenaltyRate { get; set; } = 2;

        // Loan Settings
        [Required]
        [Range(1, 10, ErrorMessage = "Loan multiple must be between 1 and 10")]
        public int MaxLoanAmountMultiple { get; set; } = 3;

        [Required]
        [Range(0, 10, ErrorMessage = "Loan processing fee must be between 0 and 10")]
        public decimal LoanProcessingFee { get; set; } = 1;

        [Required]
        [Range(1, 30, ErrorMessage = "Loan interest rate must be between 1 and 30")]
        public decimal LoanInterestRate { get; set; } = 12;

        [Required]
        [Range(1, 60, ErrorMessage = "Maximum repayment period must be between 1 and 60 months")]
        public int MaxLoanRepaymentPeriod { get; set; } = 24;

        [Required]
        [Range(1, 5, ErrorMessage = "Minimum guarantors must be between 1 and 5")]
        public int MinGuarantorsRequired { get; set; } = 2;

        [Required]
        [Range(1, 24, ErrorMessage = "Eligibility period must be between 1 and 24 months")]
        public int LoanEligibilityPeriod { get; set; } = 6;

        // Notification Settings
        public bool EmailNotificationsEnabled { get; set; } = true;
        public bool SMSNotificationsEnabled { get; set; } = true;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string DefaultNotificationEmail { get; set; } = "admin@sacco.com";
        public string SMSSenderId { get; set; } = "SACCO";

        [Range(0, 30, ErrorMessage = "Contribution reminder days must be between 0 and 30")]
        public int ContributionReminderDays { get; set; } = 7;

        [Range(0, 30, ErrorMessage = "Loan due reminder days must be between 0 and 30")]
        public int LoanDueReminderDays { get; set; } = 7;

        // Security Settings
        [Required]
        [Range(30, 365, ErrorMessage = "Password expiry must be between 30 and 365 days")]
        public int PasswordExpiryDays { get; set; } = 90;

        [Required]
        [Range(1, 10, ErrorMessage = "Failed attempts must be between 1 and 10")]
        public int FailedAttemptsBeforeLockout { get; set; } = 5;

        [Required]
        [Range(5, 1440, ErrorMessage = "Session timeout must be between 5 and 1440 minutes")]
        public int SessionTimeoutMinutes { get; set; } = 30;
        public bool TwoFactorAuthenticationRequired { get; set; } = false;

        [Required(ErrorMessage = "Password complexity is required")]
        public string PasswordComplexity { get; set; } = "Medium";

        [Required]
        [Range(0, 24, ErrorMessage = "Password history must be between 0 and 24")]
        public int PasswordHistoryCount { get; set; } = 5;

        public static SaccoSettings GetDefaultSettings() => new SaccoSettings();

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            if (obj is not SaccoSettings other) return false;

            // Compare all properties
            return SACCOName == other.SACCOName &&
                   RegistrationNumber == other.RegistrationNumber &&
                   FiscalYearStart == other.FiscalYearStart &&
                   FiscalYearEnd == other.FiscalYearEnd &&
                   Currency == other.Currency &&
                   Timezone == other.Timezone &&
                   SharePrice == other.SharePrice &&
                   DividendRate == other.DividendRate &&
                   MinimumShares == other.MinimumShares &&
                   MaximumShares == other.MaximumShares &&
                   InterestCalculationMethod == other.InterestCalculationMethod &&
                   PenaltyRate == other.PenaltyRate &&
                   MaxLoanAmountMultiple == other.MaxLoanAmountMultiple &&
                   LoanProcessingFee == other.LoanProcessingFee &&
                   LoanInterestRate == other.LoanInterestRate &&
                   MaxLoanRepaymentPeriod == other.MaxLoanRepaymentPeriod &&
                   MinGuarantorsRequired == other.MinGuarantorsRequired &&
                   LoanEligibilityPeriod == other.LoanEligibilityPeriod &&
                   EmailNotificationsEnabled == other.EmailNotificationsEnabled &&
                   SMSNotificationsEnabled == other.SMSNotificationsEnabled &&
                   DefaultNotificationEmail == other.DefaultNotificationEmail &&
                   SMSSenderId == other.SMSSenderId &&
                   ContributionReminderDays == other.ContributionReminderDays &&
                   LoanDueReminderDays == other.LoanDueReminderDays &&
                   PasswordExpiryDays == other.PasswordExpiryDays &&
                   FailedAttemptsBeforeLockout == other.FailedAttemptsBeforeLockout &&
                   SessionTimeoutMinutes == other.SessionTimeoutMinutes &&
                   TwoFactorAuthenticationRequired == other.TwoFactorAuthenticationRequired &&
                   PasswordComplexity == other.PasswordComplexity &&
                   PasswordHistoryCount == other.PasswordHistoryCount;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(SACCOName);
            hash.Add(RegistrationNumber);
            hash.Add(FiscalYearStart);
            hash.Add(FiscalYearEnd);
            hash.Add(Currency);
            hash.Add(Timezone);
            hash.Add(SharePrice);
            hash.Add(DividendRate);
            hash.Add(MinimumShares);
            hash.Add(MaximumShares);
            hash.Add(InterestCalculationMethod);
            hash.Add(PenaltyRate);
            hash.Add(MaxLoanAmountMultiple);
            hash.Add(LoanProcessingFee);
            hash.Add(LoanInterestRate);
            hash.Add(MaxLoanRepaymentPeriod);
            hash.Add(MinGuarantorsRequired);
            hash.Add(LoanEligibilityPeriod);
            hash.Add(EmailNotificationsEnabled);
            hash.Add(SMSNotificationsEnabled);
            hash.Add(DefaultNotificationEmail);
            hash.Add(SMSSenderId);
            hash.Add(ContributionReminderDays);
            hash.Add(LoanDueReminderDays);
            hash.Add(PasswordExpiryDays);
            hash.Add(FailedAttemptsBeforeLockout);
            hash.Add(SessionTimeoutMinutes);
            hash.Add(TwoFactorAuthenticationRequired);
            hash.Add(PasswordComplexity);
            hash.Add(PasswordHistoryCount);
            return hash.ToHashCode();
        }
    }
}