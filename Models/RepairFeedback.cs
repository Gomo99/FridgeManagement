using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class RepairFeedback
    {
        public int Id { get; set; }

        [Required]
        public int FaultReportId { get; set; }

        [ForeignKey("FaultReportId")]
        public FaultReport? FaultReport { get; set; }

        [Required, Range(1, 5)]
        public int TechnicianRating { get; set; }

        [Required, Range(1, 5)]
        public int RepairQualityRating { get; set; }

        [Required, Range(1, 5)]
        public int FriendlinessRating { get; set; }

        [Required, Range(1, 5)]
        public int SpeedRating { get; set; }

        [StringLength(500)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}