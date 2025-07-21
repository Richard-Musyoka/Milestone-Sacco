using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SaccoManagementSystem.Models
{
    public class Dividend
    {
        public int DividendId { get; set; }
        public int MemberId { get; set; }
        public int DeclarationId { get; set; }
        public string FinancialYear { get; set; }
        public decimal Amount { get; set; }
        public decimal Shares { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentNumber { get; set; }
        public string TransactionReference { get; set; }
        public string Status { get; set; } = "Pending";
        public string Remarks { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    
        public class DividendDeclarationRequest
        {
            [Required(ErrorMessage = "Financial Year is required")]
            [StringLength(10)]
            public string FinancialYear { get; set; }

            [Required(ErrorMessage = "Declaration Date is required")]
            public DateTime DeclarationDate { get; set; }

            [Required(ErrorMessage = "Record Date is required")]
            public DateTime RecordDate { get; set; }

            public DateTime? PaymentDate { get; set; }

            [Required(ErrorMessage = "Dividend Rate is required")]
            [Range(0.0001, 1)]
            public decimal Rate { get; set; }

            public decimal TotalAmount { get; set; }

            public string Notes { get; set; }
        }

    // Other related models...
    public class DividendDeclarationDto
    {
        public int DeclarationId { get; set; }
        public string DeclarationNumber { get; set; }
        public string FinancialYear { get; set; }
        public decimal Rate { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime DeclarationDate { get; set; }
        public DateTime RecordDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public int PaymentCount { get; set; }
        public decimal PaidAmount { get; set; }
    }

    public class DividendDeclaration
    {
        public int DeclarationId { get; set; }
        public string DeclarationNumber { get; set; }
        public string FinancialYear { get; set; }
        public decimal Rate { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime DeclarationDate { get; set; }
        public DateTime RecordDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string Notes { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }



    public class DividendPaymentDto
    {
        public int DividendId { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; }
        public string MemberNumber { get; set; }
        public int DeclarationId { get; set; }
        public string FinancialYear { get; set; }
        public decimal Amount { get; set; }
        public decimal Shares { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentNumber { get; set; }
        public string TransactionReference { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string BankAccountNumber { get; set; }  
        public string PhoneNumber { get; set; }

        // Computed properties
        public int Id => DividendId;
        public string DeclarationYear => FinancialYear;
    }
    public class DividendSummaryDto
    {
        public decimal TotalDividends { get; set; }
        public decimal PaidDividends { get; set; }
        public decimal PendingDividends { get; set; }
        public int PaidMembersCount { get; set; }
        public int PendingMembersCount { get; set; }
        public decimal? CurrentDividendRate { get; set; }
        public string CurrentDividendYear { get; set; }
        public DateTime? CurrentDeclarationDate { get; set; }
    }

    public class ProjectionCalculatorDto
    {
        public decimal EstimatedProfit { get; set; }
        public int TotalShares { get; set; }
        public decimal PayoutRatio { get; set; } = 60;
    }

    public class ProjectionResultDto
    {
        public decimal TotalDividends { get; set; }
        public decimal DividendRate { get; set; }
        public decimal PerShareAmount { get; set; }
        public decimal PayoutAmount { get; set; }
    }

    public class EligibleMembersResponse
    {
        public int EligibleMembersCount { get; set; }
        public decimal TotalShares { get; set; }  // Changed to decimal
    }

   
    public class ProcessDividendPaymentsRequest
    {
        public List<int> DividendIds { get; set; } = new List<int>();
        public DateTime PaymentDate { get; set; }
        public string BatchReference { get; set; }
        public Dictionary<int, string> PaymentMethods { get; set; } = new Dictionary<int, string>();
    }



}