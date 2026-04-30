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

            // --- Seed Users (plain text passwords) ---
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin@fridge.com", FullName = "System Administrator", PasswordHash = "admin123", Role = UserRole.ADMINISTRATOR, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 2, Username = "customer.liaison@fridge.com", FullName = "Alice Johnson", PasswordHash = "liaison123", Role = UserRole.CUSTOMERLIAISON, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 3, Username = "inventory.liaison@fridge.com", FullName = "Bob Smith", PasswordHash = "inventory123", Role = UserRole.INVENTORYLIAISON, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 4, Username = "customer@spaza.com", FullName = "Spaza Shop Owner", PasswordHash = "customer123", Role = UserRole.CUSTOMER, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 5, Username = "fault.tech@fridge.com", FullName = "Charlie Brown", PasswordHash = "tech123", Role = UserRole.FAULTTECHNICIAN, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 6, Username = "maint.tech@fridge.com", FullName = "Diana Prince", PasswordHash = "maint123", Role = UserRole.MAINTENANCETECHNICIAN, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 7, Username = "purchasing@fridge.com", FullName = "Eve Adams", PasswordHash = "purchase123", Role = UserRole.PURCHASINGMANAGER, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 8, Username = "supplier@coolers.com", FullName = "Cooler Supplier Inc.", PasswordHash = "supplier123", Role = UserRole.SUPPLIER, Gender = GenderType.Male, Status = Status.Active }
            );

            // --- Seed Customers ---
            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, Name = "Spaza Shop A", ContactPerson = "John Doe", PhoneNumber = "0123456789", Email = "spazaA@example.com", Address = "123 Main St", Status = Status.Active },
                new Customer { Id = 2, Name = "Shebeen B", ContactPerson = "Jane Smith", PhoneNumber = "0987654321", Email = "shebeenB@example.com", Address = "456 Oak Ave", Status = Status.Active }
            );

           

            // --- Seed Locations ---
            modelBuilder.Entity<Location>().HasData(
                new Location { Id = 1, Name = "Main Warehouse", Address = "1 Industrial Rd", City = "Johannesburg", Status = Status.Active },
                new Location { Id = 2, Name = "Cape Town Depot", Address = "22 Harbour St", City = "Cape Town", Status = Status.Active }
            );

            // --- Seed Fridges ---
            modelBuilder.Entity<Fridge>().HasData(
                new Fridge { Id = 1, SerialNumber = "FRIDGE-001", Model = "Cooler X100", Brand = "Frosty", PurchaseDate = DateTime.Now.AddMonths(-6), Status = Status.Active },
                new Fridge { Id = 2, SerialNumber = "FRIDGE-002", Model = "ChillMaster 500", Brand = "IceCold", PurchaseDate = DateTime.Now.AddMonths(-3), Status = Status.Active },
                new Fridge { Id = 3, SerialNumber = "FRIDGE-003", Model = "Cooler X100", Brand = "Frosty", Status = Status.Inactive } // Scrapped
            );

            // --- Seed Suppliers ---
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, Name = "Frosty Appliances", ContactPerson = "Mike Johnson", PhoneNumber = "0112223333", Email = "sales@frosty.com", Address = "789 Industrial Pkwy", Status = Status.Active },
                new Supplier { Id = 2, Name = "IceCold Ltd", ContactPerson = "Sarah Connor", PhoneNumber = "0215556666", Email = "orders@icecold.com", Address = "321 Cooler Ln", Status = Status.Active }
            );

            // --- Seed BusinessInfo (single record) ---
            modelBuilder.Entity<BusinessInfo>().HasData(
                new BusinessInfo { Id = 1, CompanyName = "Beverage Manufacturer Inc.", Address = "100 Corporate Blvd, Johannesburg", PhoneNumber = "0800 123 456", Email = "info@beverageman.com", Website = "www.beverageman.com", TaxId = "TAX123456" }
            );




            // Configure FridgeAllocation relationships
            modelBuilder.Entity<FridgeAllocation>()
                .HasOne(a => a.Customer)
                .WithMany(c => c.FridgeAllocations)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            modelBuilder.Entity<FridgeAllocation>()
                .HasOne(a => a.Fridge)
                .WithMany(f => f.FridgeAllocations)
                .HasForeignKey(a => a.FridgeId)
                .OnDelete(DeleteBehavior.Restrict);



            // FaultReport configuration
            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.Fridge)
                .WithMany()
                .HasForeignKey(f => f.FridgeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.ReportedByCustomer)
                .WithMany()
                .HasForeignKey(f => f.ReportedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FaultReport>()
                .HasOne(f => f.AssignedTechnician)
                .WithMany()
                .HasForeignKey(f => f.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            // FridgeRequest configuration
            modelBuilder.Entity<FridgeRequest>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed Employees (Technicians)
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, FullName = "Alice Johnson", Email = "customer.liaison@fridge.com", Role = UserRole.CUSTOMERLIAISON, Gender = GenderType.Female, Status = Status.Active },
                new User { Id = 2, FullName = "Bob Smith", Email = "inventory.liaison@fridge.com", Role = UserRole.INVENTORYLIAISON, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 3, FullName = "Charlie Brown", Email = "fault.tech@fridge.com", Role = UserRole.FAULTTECHNICIAN, Gender = GenderType.Male, Status = Status.Active },
                new User { Id = 4, FullName = "Diana Prince", Email = "maint.tech@fridge.com", Role = UserRole.MAINTENANCETECHNICIAN, Gender = GenderType.Female, Status = Status.Active }
            );

            // Seed sample allocations (so customers have fridges to report faults on)
            modelBuilder.Entity<FridgeAllocation>().HasData(
                new FridgeAllocation { Id = 1, FridgeId = 1, CustomerId = 1, AllocationDate = DateTime.Now.AddDays(-30), Status = AllocationStatus.Active },
                new FridgeAllocation { Id = 2, FridgeId = 2, CustomerId = 2, AllocationDate = DateTime.Now.AddDays(-15), Status = AllocationStatus.Active }
            );

            // Seed sample fault reports
            modelBuilder.Entity<FaultReport>().HasData(
                new FaultReport { Id = 1, FridgeId = 1, ReportedByCustomerId = 1, ReportedDate = DateTime.Now.AddDays(-5), Description = "Fridge not cooling properly", Priority = FaultPriority.High, Status = FaultStatus.Assigned, AssignedTechnicianId = 3 },
                new FaultReport { Id = 2, FridgeId = 2, ReportedByCustomerId = 2, ReportedDate = DateTime.Now.AddDays(-2), Description = "Strange noise from compressor", Priority = FaultPriority.Medium, Status = FaultStatus.Reported }
            );

            // Seed sample fridge requests
            modelBuilder.Entity<FridgeRequest>().HasData(
                new FridgeRequest { Id = 1, CustomerId = 1, RequestDate = DateTime.Now.AddDays(-10), Reason = "Need additional fridge for new stock", QuantityRequested = 2, Status = FridgeRequestStatus.Pending },
                new FridgeRequest { Id = 2, CustomerId = 2, RequestDate = DateTime.Now.AddDays(-1), Reason = "Current fridge is too small", QuantityRequested = 1, Status = FridgeRequestStatus.Approved, AdminNotes = "Will deliver next week" }
            );


            // ── ENUM → STRING CONVERSIONS ─────────────────────────
            // Apply to all enum properties across all entities
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var clrType = property.ClrType;

                    if (clrType.IsEnum)
                    {
                        var converterType = typeof(EnumToStringConverter<>)
                            .MakeGenericType(clrType);
                        var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                        property.SetValueConverter(converter);
                    }

                    // Bool → "True"/"False" string in DB
                    if (clrType == typeof(bool) || clrType == typeof(bool?))
                    {
                        // Only apply to actual mapped columns — skip computed props
                        if (property.PropertyInfo != null
                            && property.PropertyInfo.CanWrite)
                        {
                            if (clrType == typeof(bool))
                            {
                                property.SetValueConverter(new ValueConverter<bool, string>(
                                    v => v.ToString(),
                                    v => bool.Parse(v)));
                                property.SetMaxLength(5);
                            }
                        }
                    }


                    modelBuilder.Entity<MaintenanceSchedule>()
        .HasOne(m => m.Fridge)
        .WithMany()
        .HasForeignKey(m => m.FridgeId)
        .OnDelete(DeleteBehavior.Restrict);

                    modelBuilder.Entity<MaintenanceSchedule>()
                        .HasOne(m => m.AssignedTechnician)
                        .WithMany()
                        .HasForeignKey(m => m.AssignedTechnicianId)
                        .OnDelete(DeleteBehavior.Restrict);

                    modelBuilder.Entity<MaintenanceLog>()
                        .HasOne(l => l.MaintenanceSchedule)
                        .WithMany()
                        .HasForeignKey(l => l.MaintenanceScheduleId)
                        .OnDelete(DeleteBehavior.Restrict);

                    modelBuilder.Entity<MaintenanceLog>()
                        .HasOne(l => l.Technician)
                        .WithMany()
                        .HasForeignKey(l => l.TechnicianId)
                        .OnDelete(DeleteBehavior.Restrict);

                    // Seed sample schedules
                    modelBuilder.Entity<MaintenanceSchedule>().HasData(
                        new MaintenanceSchedule { Id = 1, FridgeId = 1, ScheduledDate = DateTime.Now.AddDays(7), Notes = "Quarterly maintenance", AssignedTechnicianId = 4, Status = MaintenanceStatus.Scheduled },
                        new MaintenanceSchedule { Id = 2, FridgeId = 2, ScheduledDate = DateTime.Now.AddDays(3), Notes = "Check cooling efficiency", AssignedTechnicianId = 4, Status = MaintenanceStatus.Scheduled }
                    );
                }
            }
            // RFQ relationships
            modelBuilder.Entity<RFQSupplier>()
                .HasOne(rs => rs.RFQ)
                .WithMany()
                .HasForeignKey(rs => rs.RFQId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RFQSupplier>()
                .HasOne(rs => rs.Supplier)
                .WithMany()
                .HasForeignKey(rs => rs.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quotation relationships
            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.RFQ)
                .WithMany()
                .HasForeignKey(q => q.RFQId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.Supplier)
                .WithMany()
                .HasForeignKey(q => q.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // PurchaseOrder relationships
            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Quotation)
                .WithMany()
                .HasForeignKey(po => po.QuotationId)
                .OnDelete(DeleteBehavior.Restrict);

            // DeliveryNote relationships
            modelBuilder.Entity<DeliveryNote>()
                .HasOne(d => d.PurchaseOrder)
                .WithMany()
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed some purchase requests (if not already present)
            modelBuilder.Entity<PurchaseRequest>().HasData(
                new PurchaseRequest { Id = 1, RequestedById = 2, Reason = "Low stock of Cooler X100", QuantityRequested = 5, RequestDate = DateTime.Now.AddDays(-5), Status = PurchaseRequestStatus.Pending },
                new PurchaseRequest { Id = 2, RequestedById = 2, Reason = "Need additional fridges for new customers", QuantityRequested = 10, RequestDate = DateTime.Now.AddDays(-2), Status = PurchaseRequestStatus.Approved }
            );



        }


    }
}
