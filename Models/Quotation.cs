using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class Quotation
    {
        public int Id { get; set; }

        [Required]
        public int RFQId { get; set; }

        [ForeignKey(nameof(RFQId))]
        public RequestForQuotation? RFQ { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [Required]
        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public int? EstimatedDeliveryDays { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public QuotationStatus Status { get; set; } = QuotationStatus.Received;

        public bool IsDeleted { get; set; } = false;
    }
}