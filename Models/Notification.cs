using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        public bool IsRead { get; set; } = false;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReadAt { get; set; }

        // Optional: Link to related entity
        public string? RelatedEntityType { get; set; } // e.g., "FaultReport", "MaintenanceSchedule"
        public int? RelatedEntityId { get; set; }

        [StringLength(500)]
        public string? ActionUrl { get; set; } // URL to navigate when clicked
    }

   
}