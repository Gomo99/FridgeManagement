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

        public bool IsDeleted { get; set; } = false;


        public int? TravelTimeMinutes { get; set; }

        // Expected repair duration in minutes
        public int? RepairDurationMinutes { get; set; }

        public DateTime? PartsArrivedDate { get; set; }

        [StringLength(2000)]
        public string? RepairChecklistJson { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RepairCost { get; set; }

        public DateTime? AssignedDate { get; set; }
        public DateTime? TechnicianTravelingAt { get; set; }
        public DateTime? TechnicianArrivedAt { get; set; }
        public DateTime? RepairStartedAt { get; set; }
        public DateTime? RepairCompletedAt { get; set; }

        public DateTime? EstimatedArrivalTime { get; set; }

        [StringLength(2000)]
        public string? RepairNotes { get; set; }

        [StringLength(2000)]
        public string? TechnicianNotes { get; set; }

        [StringLength(2000)]
        public string? CustomerNotes { get; set; }

        [StringLength(2000)]
        public string? ManagerNotes { get; set; }

        // Estimated repair duration in minutes
        public int? EstimatedDuration { get; set; }

        // Travel time from previous job in minutes (can be set manually or calculated later)
        public int? TravelTime { get; set; }

    }
}