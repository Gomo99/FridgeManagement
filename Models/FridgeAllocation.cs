using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class FridgeAllocation
    {
        public int Id { get; set; }

        [Required]
        public int FridgeId { get; set; }

        [ForeignKey(nameof(FridgeId))]
        public Fridge? Fridge { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [Required]
        public DateTime AllocationDate { get; set; } = DateTime.Now;

        public DateTime? ReturnDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public AllocationStatus Status { get; set; } = AllocationStatus.Active;
    }
}