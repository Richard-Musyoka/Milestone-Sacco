// Models/Loan.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Sacco_Management_System.Pages.Members.ListMember;

namespace SaccoManagementSystem.Models
{
    public class Loan
    {
        [Key]
        public int LoanId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        [StringLength(100)]
        public string LoanType { get; set; } // Emergency, Education, Business, Personal

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrincipalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(5, 2)")]
        public decimal InterestRate { get; set; }

        [Required]
        public int TermMonths { get; set; }

        [Required]
        public DateTime ApplicationDate { get; set; } = DateTime.Now;

        public DateTime? ApprovalDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Disbursed, Completed

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MonthlyInstallment { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotalPayable { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? OutstandingBalance { get; set; }

        public int? Guarantor1Id { get; set; }
        public int? Guarantor2Id { get; set; }

        [StringLength(255)]
        public string Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("MemberId")]
        public virtual Member Member { get; set; }

        [ForeignKey("Guarantor1Id")]
        public virtual Member Guarantor1 { get; set; }

        [ForeignKey("Guarantor2Id")]
        public virtual Member Guarantor2 { get; set; }
    }

    public class LoanViewModel
    {
        public int LoanId { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; }
        public string MemberNo { get; set; }  // Added missing property
        public string LoanNumber { get; set; }
        public string LoanType { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int TermMonths { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public decimal? MonthlyInstallment { get; set; }
        public decimal? TotalPayable { get; set; }
        public decimal? OutstandingBalance { get; set; }
        public string Remarks { get; set; }
        public int? Guarantor1Id { get; set; }  // Changed from string to int?
        public int? Guarantor2Id { get; set; }  // Changed from string to int?
        public string Guarantor1Name { get; set; }
        public string Guarantor2Name { get; set; }
    
}

}