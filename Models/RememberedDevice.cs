namespace FridgeManagement.Models
{
    public class RememberedDevice
    {
        public int Id { get; set; }

        public int UserId { get; set; }          // FK to Account.UserID
        public string TokenHash { get; set; } = null!; // SHA256(Base64) of the raw token
        public DateTime ExpiresAt { get; set; }

        public string? DeviceName { get; set; }   // optional: "Chrome on Windows", etc.
        public string? UserAgent { get; set; }    // optional: Request UA for admin/user display
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool Revoked { get; set; } = false;

        public User? User { get; set; }
    }
}
