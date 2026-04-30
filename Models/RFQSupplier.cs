using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class RFQSupplier
    {
        public int Id { get; set; }

        public int RFQId { get; set; }

        [ForeignKey(nameof(RFQId))]
        public RequestForQuotation? RFQ { get; set; }

        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}