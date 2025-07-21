
// Models/InstallmentViewModel.cs
namespace SaccoManagementSystem.Models
{
    public class InstallmentViewModel
    {
        public int InstallmentId { get; set; }
        public int LoanId { get; set; }
        public int InstallmentNumber { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal TotalDue { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Paid, Overdue
        public DateTime? PaymentDate { get; set; }
    }
}