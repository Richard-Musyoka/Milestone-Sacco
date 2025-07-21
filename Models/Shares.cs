// Models/Share.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Sacco_Management_System.Pages.Members.ListMember;

namespace SaccoManagementSystem.Models
{
    public class Share
    {
        [Key]
        public int ShareId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [ForeignKey("MemberId")]
        public Member Member { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Units must be at least 1")]
        public int Units { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalValue { get; private set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        [Required]
        [StringLength(50)]
        public string ShareType { get; set; } = "Ordinary"; // Default to Ordinary shares

        [StringLength(255)]
        public string Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class ShareViewModel
    {
        public int ShareId { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; }
        public string MemberNumber { get; set; }
        public int Units { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string Status { get; set; }
        public string ShareType { get; set; }
        public string Remarks { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ShareSummaryDto
    {
        public int TotalShares { get; set; }
        public decimal TotalValue { get; set; }
        public int ShareholdersCount { get; set; }
        public decimal CurrentSharePrice { get; set; }
    }

    public class SharePurchaseDto
    {
        [Required]
        public int MemberId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Units must be at least 1")]
        public int Units { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        [Required]
        public string ShareType { get; set; } = "Ordinary";

        public string? Status { get; set; }
        public string? Remarks { get; set; }
    }

    public class ShareTransferDto
    {
        [Required]
        public int FromMemberId { get; set; }

        [Required]
        public int ToMemberId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Units must be at least 1")]
        public int Units { get; set; }

        [Required]
        public string ShareType { get; set; } = "Ordinary";

        public string? Remarks { get; set; }
    }

    public class ShareUpdateDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Units must be at least 1")]
        public int Units { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        [Required]
        public string ShareType { get; set; } = "Ordinary";

        public string? Remarks { get; set; }
    }

    public static class ShareTypes
    {
        public static readonly List<string> All = new List<string>
        {
            "Ordinary",
            "Preference",
            "Non-Voting",
            "Redeemable",
            "Employee"
        };
    }
}