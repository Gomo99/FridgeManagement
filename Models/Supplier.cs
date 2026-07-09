using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.Models
{
    public class Supplier
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
    }
}