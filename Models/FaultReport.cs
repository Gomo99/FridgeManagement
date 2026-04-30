using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class FaultReport
    {
        public int Id { get; set; }

        [Required]
        public int FridgeId { get; set; }

        [ForeignKey(nameof(FridgeId))]
        public Fridge? Fridge { get; set; }

        [Required]
        public int ReportedByCustomerId { get; set; }

        [ForeignKey(nameof(ReportedByCustomerId))]
        public Customer? ReportedByCustomer { get; set; }

        [Required]
        public DateTime ReportedDate { get; set; } = DateTime.Now;

        [Required, StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public FaultPriority Priority { get; set; } = FaultPriority.Medium;

        public FaultStatus Status { get; set; } = FaultStatus.Reported;

        public int? AssignedTechnicianId { get; set; }

        [ForeignKey(nameof(AssignedTechnicianId))]
        public User? AssignedTechnician { get; set; }

        public DateTime? ScheduledRepairDate { get; set; }

        [StringLength(1000)]
        public string? DiagnosisNotes { get; set; }

        [StringLength(1000)]
        public string? ResolutionNotes { get; set; }

        public DateTime? ResolvedDate { get; set; }

        // Soft delete flag
        public bool IsDeleted { get; set; } = false;
    }

   
}