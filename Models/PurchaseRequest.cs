using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class PurchaseRequest
    {
        public int Id { get; set; }

        [Required]
        public int RequestedById { get; set; }

        [ForeignKey(nameof(RequestedById))]
        public User? RequestedBy { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        // NEW: link directly to the fridge being purchased
        public int? FridgeId { get; set; }

        [ForeignKey("FridgeId")]
        public virtual Fridge? Fridge { get; set; }


        [Required]
        [Range(1, 100)]
        public int QuantityRequested { get; set; }

        public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Pending;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}