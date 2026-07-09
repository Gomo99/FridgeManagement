using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string Surname { get; set; }

        [NotMapped]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public GenderType Gender { get; set; }

        public Status Status { get; set; } = Status.Active;

        public string? ResetPin { get; set; }

        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecretKey { get; set; }
        public string? TwoFactorRecoveryCodes { get; set; }

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public DateTime? ResetPinExpiration { get; set; }


        [StringLength(300)]
        public string? PhotoUrl { get; set; }

        [StringLength(100)]
        public string? Vehicle { get; set; }

        public double Rating { get; set; } = 0;   // scale 0–5
    }
}