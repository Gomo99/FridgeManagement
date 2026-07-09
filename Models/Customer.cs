using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress, StringLength(100)]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public Status Status { get; set; } = Status.Active;

        public virtual ICollection<FridgeAllocation>? FridgeAllocations { get; set; }

        [StringLength(200)]
        public string? OperatingHours { get; set; }

        [StringLength(100)]
        public string? EmergencyContact { get; set; }

        public string? Owner { get; set; }

        public BusinessType? BusinessType { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}