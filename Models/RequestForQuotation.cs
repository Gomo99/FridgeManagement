using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class RequestForQuotation
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseRequestId { get; set; }

        [ForeignKey(nameof(PurchaseRequestId))]
        public PurchaseRequest? PurchaseRequest { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        public RFQStatus Status { get; set; } = RFQStatus.Draft;

        public bool IsDeleted { get; set; } = false;
    }
}