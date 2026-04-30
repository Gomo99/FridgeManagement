using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;

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


        public Status Status { get; set; } = Status.Active; // Active = in stock/working, Inactive = scrapped



        public virtual ICollection<FridgeAllocation>? FridgeAllocations { get; set; }
    }
}