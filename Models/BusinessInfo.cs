using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.Models
{
    public class BusinessInfo
    {
        public int Id { get; set; } = 1;

        [Required, StringLength(100)]
        public string CompanyName { get; set; } = "Beverage Manufacturer";

        [StringLength(200)]
        public string? Address { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress, StringLength(100)]
        public string? Email { get; set; }

        [StringLength(100)]
        public string? Website { get; set; }

        [StringLength(50)]
        public string? TaxId { get; set; }
    }
}