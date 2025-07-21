// Models/Guarantor.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaccoManagementSystem.Models
{
    public class Guarantor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GuarantorId { get; set; }

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

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string Remarks { get; set; }

        public long? CreatedBy { get; set; }

        // Navigation properties
        [ForeignKey("CreatedBy")]
        public virtual User CreatedByUser { get; set; }

        public virtual ICollection<LoanGuarantor> LoanGuarantors { get; set; }
    }
}