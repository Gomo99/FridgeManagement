using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class DeliveryNote
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }

        [ForeignKey(nameof(PurchaseOrderId))]
        public PurchaseOrder? PurchaseOrder { get; set; }

        [Required]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        [Required]
        public int QuantityDelivered { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}