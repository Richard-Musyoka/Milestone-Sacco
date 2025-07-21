// Models/Contribution.cs
using System;
using System.ComponentModel.DataAnnotations;
using static Sacco_Management_System.Pages.Members.ListMember;

namespace SaccoManagementSystem.Models
{
    public class Contribution
    {
        [Key]
        public int ContributionId { get; set; }

        [Required(ErrorMessage = "Member is required")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "Contribution type is required")]
        [StringLength(50)]
        public string ContributionType { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateTime DateContributed { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(100)]
        public string TransactionRef { get; set; }

        public long? CreatedBy { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [StringLength(255)]
        public string Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    // ViewModel for the contributions list page
    public class ContributionViewModel
    {
        public int ContributionId { get; set; }
        public string ReceiptNumber => $"CT-{ContributionId.ToString("D3")}";
        public int MemberId { get; set; }
        public string MemberName { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; } // Added this property
        public string TransactionRef { get; set; } // Added this property
        public string Remarks { get; set; } // Added this property
    }
}