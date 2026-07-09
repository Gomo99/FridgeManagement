using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class Fridge
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Model { get; set; }

        [StringLength(100)]
        public string? Brand { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public Status Status { get; set; } = Status.Active;

        // ----- NEW fields -----
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        public DateTime? WarrantyExpiryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(300)]
        public string? ImageUrl { get; set; }

        // existing navigation
        public virtual ICollection<FridgeAllocation>? FridgeAllocations { get; set; }
    }
}