using FridgeManagement.AppStatus;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FridgeManagement.Models
{
    public class BusinessInfo
    {
        public int Id { get; set; } = 1; // Single record

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


    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress, StringLength(100)]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public Status Status { get; set; } = Status.Active;

        // Navigation property for allocations
        public virtual ICollection<FridgeAllocation>? FridgeAllocations { get; set; }
    }

    public class DeliveryNote
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }

        [ForeignKey(nameof(PurchaseOrderId))]
        public PurchaseOrder? PurchaseOrder { get; set; }

        [Required]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        [Required]
        public int QuantityDelivered { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

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


    public class Fridge
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Model { get; set; }

        [StringLength(100)]
        public string? Brand { get; set; }

        public DateTime? PurchaseDate { get; set; }


        public Status Status { get; set; } = Status.Active; // Active = in stock/working, Inactive = scrapped



        public virtual ICollection<FridgeAllocation>? FridgeAllocations { get; set; }
    }



    public class FridgeAllocation
    {
        public int Id { get; set; }

        [Required]
        public int FridgeId { get; set; }

        [ForeignKey(nameof(FridgeId))]
        public Fridge? Fridge { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [Required]
        public DateTime AllocationDate { get; set; } = DateTime.Now;

        public DateTime? ReturnDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public AllocationStatus Status { get; set; } = AllocationStatus.Active;


    }



    public class FridgeRequest
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Range(1, 100)]
        public int QuantityRequested { get; set; } = 1;

        public FridgeRequestStatus Status { get; set; } = FridgeRequestStatus.Pending;

        [StringLength(500)]
        public string? AdminNotes { get; set; }

        public DateTime? ProcessedDate { get; set; }

        // Soft delete flag (use bool for simplicity)
        public bool IsDeleted { get; set; } = false;
    }



    public class Location
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        public Status Status { get; set; } = Status.Active;
    }




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

        // Store checklist results as JSON string (e.g., "Compressor:Pass;Thermostat:Fail")
        public string? ChecklistResults { get; set; }

        public int TechnicianId { get; set; }

        [ForeignKey(nameof(TechnicianId))]
        public User? Technician { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; } = false;
    }




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



    public class PurchaseOrder
    {
        public int Id { get; set; }

        [Required]
        public int QuotationId { get; set; }

        [ForeignKey(nameof(QuotationId))]
        public Quotation? Quotation { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime? ExpectedDeliveryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Ordered;

        public bool IsDeleted { get; set; } = false;
    }



    public class PurchaseRequest
    {
        public int Id { get; set; }

        [Required]
        public int RequestedById { get; set; } // Employee Id of Inventory Liaison

        [ForeignKey(nameof(RequestedById))]
        public User? RequestedBy { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [Range(1, 100)]
        public int QuantityRequested { get; set; }

        public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Pending;

        [StringLength(500)]
        public string? Notes { get; set; }
    }



    public class Quotation
    {
        public int Id { get; set; }

        [Required]
        public int RFQId { get; set; }

        [ForeignKey(nameof(RFQId))]
        public RequestForQuotation? RFQ { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [Required]
        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public int? EstimatedDeliveryDays { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public QuotationStatus Status { get; set; } = QuotationStatus.Received;

        public bool IsDeleted { get; set; } = false;
    }



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


    public class RequestForQuotation
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseRequestId { get; set; }

        [ForeignKey(nameof(PurchaseRequestId))]
        public PurchaseRequest? PurchaseRequest { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        public RFQStatus Status { get; set; } = RFQStatus.Draft;

        public bool IsDeleted { get; set; } = false;
    }



    public class RFQSupplier
    {
        public int Id { get; set; }

        public int RFQId { get; set; }

        [ForeignKey(nameof(RFQId))]
        public RequestForQuotation? RFQ { get; set; }

        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        public bool IsDeleted { get; set; } = false;


    }


    public class Supplier
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [EmailAddress, StringLength(100)]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        public Status Status { get; set; } = Status.Active;
    }



    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]

        public string Title { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;   // Email or Username

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
    }







}
