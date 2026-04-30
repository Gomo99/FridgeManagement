using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class MaintenanceSchedule
    {
        public int Id { get; set; }

        [Required]
        public int FridgeId { get; set; }

        [ForeignKey(nameof(FridgeId))]
        public Fridge? Fridge { get; set; }

        [Required]
        public DateTime ScheduledDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int AssignedTechnicianId { get; set; }

        [ForeignKey(nameof(AssignedTechnicianId))]
        public User? AssignedTechnician { get; set; }

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
    }


}