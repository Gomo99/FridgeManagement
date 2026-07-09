namespace FridgeManagement.Models
{
    public class RememberedDevice
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }

        public string? DeviceName { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool Revoked { get; set; } = false;

        public User? User { get; set; }
    }
}