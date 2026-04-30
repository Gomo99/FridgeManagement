using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class FridgeRequest
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Range(1, 100)]
        public int QuantityRequested { get; set; } = 1;

        public FridgeRequestStatus Status { get; set; } = FridgeRequestStatus.Pending;

        [StringLength(500)]
        public string? AdminNotes { get; set; }

        public DateTime? ProcessedDate { get; set; }

        // Soft delete flag (use bool for simplicity)
        public bool IsDeleted { get; set; } = false;
    }
}