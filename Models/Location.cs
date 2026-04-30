using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        public Status Status { get; set; } = Status.Active;
    }
}