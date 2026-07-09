using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class MaintenanceLog
    {
        public int Id { get; set; }

        [Required]
        public int MaintenanceScheduleId { get; set; }

        [ForeignKey(nameof(MaintenanceScheduleId))]
        public MaintenanceSchedule? MaintenanceSchedule { get; set; }

        public DateTime? CompletedDate { get; set; } = DateTime.Now;

        [StringLength(1000)]
        public string? ServiceNotes { get; set; }

        public string? ChecklistResults { get; set; }

        public int TechnicianId { get; set; }

        [ForeignKey(nameof(TechnicianId))]
        public User? Technician { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}