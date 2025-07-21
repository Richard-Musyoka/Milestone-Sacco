using System;
using System.ComponentModel.DataAnnotations;

namespace SaccoManagementSystem.Models
{
    public class MemberModel
    {
        public int MemberId { get; set; }

        [Required]
        public string MemberNo { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        public string NationalID { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string MaritalStatus { get; set; }
        public string Occupation { get; set; }
        public string Employer { get; set; }
        public string Address { get; set; }
        public string ProfileImageUrl { get; set; }
        public DateTime? JoinDate { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
    }
}
