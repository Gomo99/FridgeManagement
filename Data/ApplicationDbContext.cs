using FridgeManagement.AppStatus;
using FridgeManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FridgeManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Fridge> Fridges { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<BusinessInfo> BusinessInfos { get; set; }
        public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; }
        public DbSet<FridgeAllocation> FridgeAllocations { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<FaultReport> FaultReports { get; set; }
        public DbSet<FridgeRequest> FridgeRequests { get; set; }
        public DbSet<RememberedDevice> RememberedDevices { get; set; }
        public DbSet<RequestForQuotation> RequestForQuotations { get; set; }
        public DbSet<RFQSupplier> RFQSuppliers { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<DeliveryNote> DeliveryNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ────────── Relationships ──────────
            modelBuilder.Entity<FridgeAllocation>()
                .HasOne(a => a.Customer).WithMany(c => c.FridgeAllocations)
                .HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<FridgeAllocation>()
                .HasOne(a => a.Fridge).WithMany(f => f.FridgeAllocations)
                .HasForeignKey(a => a.FridgeId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.Fridge).WithMany().HasForeignKey(f => f.FridgeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.ReportedByCustomer).WithMany().HasForeignKey(f => f.ReportedByCustomerId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.AssignedTechnician).WithMany().HasForeignKey(f => f.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FridgeRequest>()
                .HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaintenanceSchedule>()
                .HasOne(m => m.Fridge).WithMany().HasForeignKey(m => m.FridgeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MaintenanceSchedule>()
                .HasOne(m => m.AssignedTechnician).WithMany().HasForeignKey(m => m.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MaintenanceLog>()
                .HasOne(l => l.MaintenanceSchedule).WithMany().HasForeignKey(l => l.MaintenanceScheduleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MaintenanceLog>()
                .HasOne(l => l.Technician).WithMany().HasForeignKey(l => l.TechnicianId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RFQSupplier>()
                .HasOne(rs => rs.RFQ).WithMany().HasForeignKey(rs => rs.RFQId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RFQSupplier>()
                .HasOne(rs => rs.Supplier).WithMany().HasForeignKey(rs => rs.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.RFQ).WithMany().HasForeignKey(q => q.RFQId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.Supplier).WithMany().HasForeignKey(q => q.SupplierId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Quotation).WithMany().HasForeignKey(po => po.QuotationId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeliveryNote>()
                .HasOne(d => d.PurchaseOrder).WithMany().HasForeignKey(d => d.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);

            // ────────── Seed Users (Employees / System Accounts) ──────────
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Title = "Mr", Name = "System", Surname = "Administrator", Username = "admin@fridge.com", Email = "admin@fridge.com", PasswordHash = "admin123", Role = UserRole.ADMINISTRATOR, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 2, Title = "Ms", Name = "Alice", Surname = "Johnson", Username = "customer.liaison@fridge.com", Email = "customer.liaison@fridge.com", PasswordHash = "liaison123", Role = UserRole.CUSTOMERLIAISON, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 3, Title = "Mr", Name = "Bob", Surname = "Smith", Username = "inventory.liaison@fridge.com", Email = "inventory.liaison@fridge.com", PasswordHash = "inventory123", Role = UserRole.INVENTORYLIAISON, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 4, Title = "Mr", Name = "Spaza", Surname = "Owner", Username = "customer@spaza.com", Email = "customer@spaza.com", PasswordHash = "customer123", Role = UserRole.CUSTOMER, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 5, Title = "Mr", Name = "Charlie", Surname = "Brown", Username = "fault.tech@fridge.com", Email = "fault.tech@fridge.com", PasswordHash = "tech123", Role = UserRole.FAULTTECHNICIAN, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 6, Title = "Ms", Name = "Diana", Surname = "Prince", Username = "maint.tech@fridge.com", Email = "maint.tech@fridge.com", PasswordHash = "maint123", Role = UserRole.MAINTENANCETECHNICIAN, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 7, Title = "Ms", Name = "Eve", Surname = "Adams", Username = "purchasing@fridge.com", Email = "purchasing@fridge.com", PasswordHash = "purchase123", Role = UserRole.PURCHASINGMANAGER, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 8, Title = "Mr", Name = "Cooler", Surname = "Supplier", Username = "supplier@coolers.com", Email = "supplier@coolers.com", PasswordHash = "supplier123", Role = UserRole.SUPPLIER, Gender = GenderType.Male, Status = Status.Active }
            );

            // ────────── Customers ──────────
            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, Name = "Spaza Shop A", ContactPerson = "John Doe", PhoneNumber = "0123456789", Email = "spazaA@example.com", Address = "123 Main St", Status = Status.Active },
                new Customer { Id = 2, Name = "Shebeen B", ContactPerson = "Jane Smith", PhoneNumber = "0987654321", Email = "shebeenB@example.com", Address = "456 Oak Ave", Status = Status.Active }
            );

            // ────────── Locations ──────────
            modelBuilder.Entity<Location>().HasData(
                new Location { Id = 1, Name = "Main Warehouse", Address = "1 Industrial Rd", City = "Johannesburg", Status = Status.Active },
                new Location { Id = 2, Name = "Cape Town Depot", Address = "22 Harbour St", City = "Cape Town", Status = Status.Active }
            );

            // ────────── Fridges ──────────
            modelBuilder.Entity<Fridge>().HasData(
                new Fridge { Id = 1, SerialNumber = "FRIDGE-001", Model = "Cooler X100", Brand = "Frosty", PurchaseDate = new DateTime(2024, 11, 1), Status = Status.Active },
                new Fridge { Id = 2, SerialNumber = "FRIDGE-002", Model = "ChillMaster 500", Brand = "IceCold", PurchaseDate = new DateTime(2025, 2, 1), Status = Status.Active },
                new Fridge { Id = 3, SerialNumber = "FRIDGE-003", Model = "Cooler X100", Brand = "Frosty", Status = Status.Inactive }
            );

            // ────────── Fridge Allocations ──────────
            modelBuilder.Entity<FridgeAllocation>().HasData(
                new FridgeAllocation { Id = 1, FridgeId = 1, CustomerId = 1, AllocationDate = new DateTime(2025, 4, 1), Status = AllocationStatus.Active },
                new FridgeAllocation { Id = 2, FridgeId = 2, CustomerId = 2, AllocationDate = new DateTime(2025, 4, 16), Status = AllocationStatus.Active }
            );

            // ────────── Fault Reports ──────────
            modelBuilder.Entity<FaultReport>().HasData(
                new FaultReport { Id = 1, FridgeId = 1, ReportedByCustomerId = 1, ReportedDate = new DateTime(2025, 4, 26), Description = "Fridge not cooling properly", Priority = FaultPriority.High, Status = FaultStatus.Assigned, AssignedTechnicianId = 5 },
                new FaultReport { Id = 2, FridgeId = 2, ReportedByCustomerId = 2, ReportedDate = new DateTime(2025, 4, 29), Description = "Strange noise from compressor", Priority = FaultPriority.Medium, Status = FaultStatus.Reported }
            );

            // ────────── Fridge Requests ──────────
            modelBuilder.Entity<FridgeRequest>().HasData(
                new FridgeRequest { Id = 1, CustomerId = 1, RequestDate = new DateTime(2025, 4, 21), Reason = "Need additional fridge for new stock", QuantityRequested = 2, Status = FridgeRequestStatus.Pending },
                new FridgeRequest { Id = 2, CustomerId = 2, RequestDate = new DateTime(2025, 4, 30), Reason = "Current fridge is too small", QuantityRequested = 1, Status = FridgeRequestStatus.Approved, AdminNotes = "Will deliver next week" }
            );

            // ────────── Maintenance Schedules ──────────
            modelBuilder.Entity<MaintenanceSchedule>().HasData(
                new MaintenanceSchedule { Id = 1, FridgeId = 1, ScheduledDate = new DateTime(2025, 5, 8), Notes = "Quarterly maintenance", AssignedTechnicianId = 6, Status = MaintenanceStatus.Scheduled },
                new MaintenanceSchedule { Id = 2, FridgeId = 2, ScheduledDate = new DateTime(2025, 5, 4), Notes = "Check cooling efficiency", AssignedTechnicianId = 6, Status = MaintenanceStatus.Scheduled }
            );

            // ────────── Purchase Requests ──────────
            modelBuilder.Entity<PurchaseRequest>().HasData(
                new PurchaseRequest { Id = 1, RequestedById = 3, Reason = "Low stock of Cooler X100", QuantityRequested = 5, RequestDate = new DateTime(2025, 4, 26), Status = PurchaseRequestStatus.Pending },
                new PurchaseRequest { Id = 2, RequestedById = 3, Reason = "Need additional fridges for new customers", QuantityRequested = 10, RequestDate = new DateTime(2025, 4, 29), Status = PurchaseRequestStatus.Approved }
            );

            // ────────── Enum → String conversion (all entities) ──────────
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType.IsEnum)
                    {
                        var converterType = typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType);
                        var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                        property.SetValueConverter(converter);
                    }
                }
            }
        }








    }
}