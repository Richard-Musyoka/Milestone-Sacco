// Models/LoanGuarantor.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Sacco_Management_System.Pages.Members.ListMember;

namespace SaccoManagementSystem.Models
{
    public class LoanGuarantor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanGuarantorId { get; set; }

        [Required]
        public int LoanId { get; set; }

        [Required]
        public int GuarantorId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [StringLength(50)]
        public string Relationship { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GuaranteedAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime? ApprovalDate { get; set; }

        public long? ApprovedBy { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string Remarks { get; set; }

        // Navigation properties
        [ForeignKey("LoanId")]
        public virtual Loan Loan { get; set; }

        [ForeignKey("GuarantorId")]
        public virtual Guarantor Guarantor { get; set; }

        [ForeignKey("MemberId")]
        public virtual Member Member { get; set; }

        [ForeignKey("ApprovedBy")]
        public virtual User ApprovedByUser { get; set; }
    }
}