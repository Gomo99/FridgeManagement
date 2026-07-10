using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text.Json;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "CUSTOMERLIAISON")]
    public class CustomerLiaisonManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;   // new

        public CustomerLiaisonManagementController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }


        // ==================== DASHBOARD ====================
        public async Task<IActionResult> DashBoard()
        {
            // ==================== KPI COUNTS ====================
            int totalCustomers = await _context.Customers.CountAsync();
            int activeCustomers = await _context.Customers.CountAsync(c => c.Status == Status.Active);
            int allocatedFridges = await _context.FridgeAllocations.CountAsync(a => a.Status == AllocationStatus.Active);
            int availableFridges = await _context.Fridges.CountAsync(f => f.Status == Status.Active
                && !_context.FridgeAllocations.Any(a => a.FridgeId == f.Id && a.Status == AllocationStatus.Active));
            int pendingFridgeRequests = await _context.FridgeRequests.CountAsync(r => r.Status == FridgeRequestStatus.Pending && !r.IsDeleted);
            int approvedRequests = await _context.FridgeRequests.CountAsync(r => r.Status == FridgeRequestStatus.Approved && !r.IsDeleted);
            int returnedFridges = await _context.FridgeAllocations.CountAsync(a => a.Status == AllocationStatus.Returned);

            // Average allocation time (days) – only for returned allocations with dates
            var returnedAllocations = await _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Returned && a.ReturnDate != null)
                .ToListAsync();
            double avgAllocationDays = 0;
            if (returnedAllocations.Any())
                avgAllocationDays = returnedAllocations.Average(a => (a.ReturnDate!.Value - a.AllocationDate).TotalDays);

            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.ActiveCustomers = activeCustomers;
            ViewBag.AllocatedFridges = allocatedFridges;
            ViewBag.AvailableFridges = availableFridges;
            ViewBag.PendingFridgeRequests = pendingFridgeRequests;
            ViewBag.ApprovedRequests = approvedRequests;
            ViewBag.ReturnedFridges = returnedFridges;
            ViewBag.AvgAllocationDays = avgAllocationDays.ToString("F1");

            // ==================== CHART DATA ====================
            // 1. Customers by Business Type (pie)
            var customersByType = await _context.Customers
                .Where(c => c.BusinessType != null)
                .GroupBy(c => c.BusinessType!.Value)
                .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            ViewBag.CustomerTypeLabels = JsonSerializer.Serialize(customersByType.Select(x => x.Type));
            ViewBag.CustomerTypeData = JsonSerializer.Serialize(customersByType.Select(x => x.Count));

            // 2. Allocations per Month (current year)
            var currentYear = DateTime.Today.Year;
            var monthlyAllocations = await _context.FridgeAllocations
                .Where(a => a.AllocationDate.Year == currentYear)
                .GroupBy(a => a.AllocationDate.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();
            var months = Enumerable.Range(1, 12).Select(m => new DateTime(currentYear, m, 1).ToString("MMM")).ToList();
            var allocCounts = months.Select(m => monthlyAllocations.FirstOrDefault(x => x.Month == DateTime.ParseExact(m, "MMM", null).Month)?.Count ?? 0).ToList();
            ViewBag.AllocMonthLabels = JsonSerializer.Serialize(months);
            ViewBag.AllocMonthData = JsonSerializer.Serialize(allocCounts);

            // 3. Top Customers (by active allocations)
            var topCustomers = await _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Active)
                .GroupBy(a => a.CustomerId)
                .Select(g => new
                {
                    CustomerName = _context.Customers.FirstOrDefault(c => c.Id == g.Key).Name,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();
            ViewBag.TopCustomersLabels = JsonSerializer.Serialize(topCustomers.Select(x => x.CustomerName));
            ViewBag.TopCustomersData = JsonSerializer.Serialize(topCustomers.Select(x => x.Count));

            // 4. Fridges by Brand (pie)
            var fridgesByBrand = await _context.Fridges
                .GroupBy(f => f.Brand ?? "Unknown")
                .Select(g => new { Brand = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.FridgeBrandLabels = JsonSerializer.Serialize(fridgesByBrand.Select(x => x.Brand));
            ViewBag.FridgeBrandData = JsonSerializer.Serialize(fridgesByBrand.Select(x => x.Count));

            // 5. Fridge Allocation Status (pie)
            var allocationStatuses = await _context.FridgeAllocations
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            ViewBag.AllocationStatusLabels = JsonSerializer.Serialize(allocationStatuses.Select(x => x.Status));
            ViewBag.AllocationStatusData = JsonSerializer.Serialize(allocationStatuses.Select(x => x.Count));

            // ==================== RECENT ACTIVITY ====================
            var activities = new List<object>();

            // Recent customer registrations
            var recentCustomers = await _context.Customers
                .Where(c => c.CreatedDate != null)
                .OrderByDescending(c => c.CreatedDate)
                .Take(5)
                .Select(c => new
                {
                    Date = c.CreatedDate!.Value,
                    Description = $"Customer {c.Name} registered",
                    Type = "Customer"
                }).ToListAsync();
            foreach (var a in recentCustomers)
                activities.Add(a);

            // Recent fridge allocations
            var recentAllocations = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Include(a => a.Fridge)
                .Where(a => a.Status == AllocationStatus.Active)
                .OrderByDescending(a => a.AllocationDate)
                .Take(5)
                .Select(a => new
                {
                    Date = a.AllocationDate,
                    Description = $"Fridge {a.Fridge!.SerialNumber} allocated to {a.Customer!.Name}",
                    Type = "Allocation"
                }).ToListAsync();
            foreach (var a in recentAllocations)
                activities.Add(a);

            // Recent fridge returns
            var recentReturns = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Include(a => a.Fridge)
                .Where(a => a.Status == AllocationStatus.Returned && a.ReturnDate != null)
                .OrderByDescending(a => a.ReturnDate)
                .Take(5)
                .Select(a => new
                {
                    Date = a.ReturnDate!.Value,
                    Description = $"Fridge {a.Fridge!.SerialNumber} returned from {a.Customer!.Name}",
                    Type = "Return"
                }).ToListAsync();
            foreach (var a in recentReturns)
                activities.Add(a);

            // Recent fridge requests
            var recentRequests = await _context.FridgeRequests
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.RequestDate)
                .Take(5)
                .Select(r => new
                {
                    Date = r.RequestDate,
                    Description = $"Fridge request from Customer ID {r.CustomerId} - {r.Reason}",
                    Type = "Request"
                }).ToListAsync();
            foreach (var a in recentRequests)
                activities.Add(a);

            // Sort combined list by date descending, take top 15
            var sortedActivities = activities.OrderByDescending(a => ((DateTime)a.GetType().GetProperty("Date")!.GetValue(a))).Take(15).ToList();
            ViewBag.RecentActivities = sortedActivities;

            return View();
        }

        // ==================== CUSTOMERS (View Only) ====================
        public async Task<IActionResult> Customers()
        {
            var customers = await _context.Customers
                .Where(c => c.Status == Status.Active)
                .ToListAsync();
            return View(customers);
        }

        public async Task<IActionResult> InactiveCustomers()
        {
            var customers = await _context.Customers
                .Where(c => c.Status == Status.Inactive)
                .ToListAsync();
            return View(customers);
        }

        // Customer details with allocated fridges
        public async Task<IActionResult> CustomerDetails(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == Status.Active);
            if (customer == null) return NotFound();

            // Allocated fridges (active)
            var allocatedFridges = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == id && a.Status == AllocationStatus.Active)
                .ToListAsync();

            // Allocation history (all)
            var allocationHistory = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            // Fault history
            var faultHistory = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .Where(f => f.ReportedByCustomerId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // Maintenance schedules
            var fridgeIds = allocatedFridges.Select(a => a.FridgeId).ToList();
            var maintenanceSchedules = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .Where(s => fridgeIds.Contains(s.FridgeId) && !s.IsDeleted)
                .OrderByDescending(s => s.ScheduledDate)
                .ToListAsync();

            // Fridge requests
            var fridgeRequests = await _context.FridgeRequests
                .Where(r => r.CustomerId == id && !r.IsDeleted)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Build unified timeline
            var timeline = new List<dynamic>();

            // Customer registration (if CreatedDate exists)
            if (customer.CreatedDate.HasValue)
            {
                timeline.Add(new
                {
                    Date = customer.CreatedDate.Value,
                    Type = "Registration",
                    Description = $"Customer {customer.Name} registered",
                    Icon = "fa-user-plus",
                    Color = "#5FD4E0"
                });
            }

            // Allocations and returns
            foreach (var alloc in allocationHistory)
            {
                timeline.Add(new
                {
                    Date = alloc.AllocationDate,
                    Type = "Allocation",
                    Description = $"Fridge {alloc.Fridge?.SerialNumber} allocated",
                    Icon = "fa-hand-holding",
                    Color = "#3ECF8E"
                });
                if (alloc.ReturnDate.HasValue)
                {
                    timeline.Add(new
                    {
                        Date = alloc.ReturnDate.Value,
                        Type = "Return",
                        Description = $"Fridge {alloc.Fridge?.SerialNumber} returned",
                        Icon = "fa-rotate-left",
                        Color = "#F5A623"
                    });
                }
            }

            // Faults
            foreach (var fault in faultHistory)
            {
                timeline.Add(new
                {
                    Date = fault.ReportedDate,
                    Type = "Fault",
                    Description = $"Fault on {fault.Fridge?.SerialNumber}: {fault.Description}",
                    Icon = "fa-exclamation-triangle",
                    Color = "#F5A623"
                });
            }

            // Maintenance (scheduled/completed)
            foreach (var sched in maintenanceSchedules)
            {
                timeline.Add(new
                {
                    Date = sched.ScheduledDate,
                    Type = "Maintenance",
                    Description = $"Maintenance scheduled for {sched.Fridge?.SerialNumber}",
                    Icon = "fa-calendar-check",
                    Color = "#5D9CEC"
                });
            }

            // Fridge requests
            foreach (var req in fridgeRequests)
            {
                timeline.Add(new
                {
                    Date = req.RequestDate,
                    Type = "Request",
                    Description = $"Fridge request: {req.Reason}",
                    Icon = "fa-clipboard-list",
                    Color = "#8FAEBE"
                });
            }

            timeline = timeline.OrderByDescending(e => e.Date).ToList();

            ViewBag.Customer = customer;
            ViewBag.AllocatedFridges = allocatedFridges;
            ViewBag.AllocationHistory = allocationHistory;
            ViewBag.FaultHistory = faultHistory;
            ViewBag.MaintenanceSchedules = maintenanceSchedules;
            ViewBag.FridgeRequests = fridgeRequests;
            ViewBag.Timeline = timeline;

            return View();
        }

        // ==================== FRIDGE ALLOCATIONS ====================
        // List all active allocations
        public async Task<IActionResult> Allocations()
        {
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Include(a => a.Customer)
                .Where(a => a.Status == AllocationStatus.Active)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            // ---- ADD THESE LINES ----
            var customers = await _context.Customers
                .Where(c => c.Status == Status.Active)
                .Select(c => new { value = c.Id.ToString(), text = c.Name })
                .ToListAsync();

            var allocatedFridgeIds = _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Active)
                .Select(a => a.FridgeId);

            var availableFridges = await _context.Fridges
                .Where(f => f.Status == Status.Active && !allocatedFridgeIds.Contains(f.Id))
                .Select(f => new { value = f.Id.ToString(), text = f.SerialNumber })
                .ToListAsync();

            ViewBag.CustomerJson = System.Text.Json.JsonSerializer.Serialize(customers);
            ViewBag.FridgeJson = System.Text.Json.JsonSerializer.Serialize(availableFridges);
            // ------------------------

            return View(allocations);
        }



        // Create new allocation (assign fridge to customer)
        public IActionResult CreateAllocation()
        {
            ViewBag.Customers = new SelectList(_context.Customers.Where(c => c.Status == Status.Active), "Id", "Name");
            // Only show fridges that are Active AND not currently allocated
            var allocatedFridgeIds = _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Active)
                .Select(a => a.FridgeId)
                .ToList();
            var availableFridges = _context.Fridges
                .Where(f => f.Status == Status.Active && !allocatedFridgeIds.Contains(f.Id))
                .ToList();
            ViewBag.Fridges = new SelectList(availableFridges, "Id", "SerialNumber");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAllocation(FridgeAllocation allocation)
        {
            if (ModelState.IsValid)
            {
                var alreadyAllocated = await _context.FridgeAllocations
                    .AnyAsync(a => a.FridgeId == allocation.FridgeId && a.Status == AllocationStatus.Active);
                if (alreadyAllocated)
                {
                    ModelState.AddModelError("FridgeId", "This fridge is already allocated.");
                    ViewBag.Customers = new SelectList(_context.Customers.Where(c => c.Status == Status.Active), "Id", "Name", allocation.CustomerId);
                    ViewBag.Fridges = new SelectList(_context.Fridges.Where(f => f.Status == Status.Active), "Id", "SerialNumber", allocation.FridgeId);
                    return View(allocation);
                }

                allocation.AllocationDate = DateTime.Now;
                allocation.Status = AllocationStatus.Active;
                _context.Add(allocation);
                await _context.SaveChangesAsync();

                // ───── NEW: Notify the customer ─────
                var customer = await _context.Customers.FindAsync(allocation.CustomerId);
                var fridge = await _context.Fridges.FindAsync(allocation.FridgeId);
                if (customer != null && fridge != null)
                {
                    // Find the User account linked to this customer (by email)
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == customer.Email &&
                                                  u.Role == UserRole.CUSTOMER &&
                                                  u.Status == Status.Active);
                    if (user != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            userId: user.Id,
                            title: "New Fridge Allocated",
                            message: $"A fridge ({fridge.SerialNumber} - {fridge.Model}) has been allocated to your shop.",
                            type: NotificationType.FridgeAllocated,
                            actionUrl: "/Customer/MyFridges"
                        );
                    }
                }
                // ─────────────────────────────────

                return RedirectToAction(nameof(Allocations));
            }

            ViewBag.Customers = new SelectList(_context.Customers.Where(c => c.Status == Status.Active), "Id", "Name", allocation.CustomerId);
            ViewBag.Fridges = new SelectList(_context.Fridges.Where(f => f.Status == Status.Active), "Id", "SerialNumber", allocation.FridgeId);
            return View(allocation);
        }


        // Return a fridge (end allocation)
        public async Task<IActionResult> ReturnAllocation(int? id)
        {
            if (id == null) return NotFound();
            var allocation = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id && a.Status == AllocationStatus.Active);
            if (allocation == null) return NotFound();
            return View(allocation);
        }

        [HttpPost, ActionName("ReturnAllocation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnAllocationConfirmed(int id)
        {
            var allocation = await _context.FridgeAllocations.FindAsync(id);
            if (allocation != null)
            {
                allocation.ReturnDate = DateTime.Now;
                allocation.Status = AllocationStatus.Returned;
                _context.Update(allocation);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Allocations));
        }

        // Allocation history for a specific fridge
        public async Task<IActionResult> FridgeHistory(int? fridgeId)
        {
            if (fridgeId == null) return NotFound();
            var history = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Where(a => a.FridgeId == fridgeId)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();
            ViewBag.Fridge = await _context.Fridges.FindAsync(fridgeId);
            return View(history);
        }


        public IActionResult AllocationWizard()
        {
            // Customers for step 1
            var customers = _context.Customers
                .Where(c => c.Status == Status.Active)
                .Select(c => new { c.Id, c.Name })
                .ToList();
            ViewBag.CustomersJson = JsonSerializer.Serialize(customers);

            // Available fridges for step 2 (active, not currently allocated)
            var allocatedIds = _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Active)
                .Select(a => a.FridgeId);
            var fridges = _context.Fridges
                .Where(f => f.Status == Status.Active && !allocatedIds.Contains(f.Id))
                .Select(f => new { f.Id, f.SerialNumber, f.Model, f.Brand })
                .ToList();
            ViewBag.FridgesJson = JsonSerializer.Serialize(fridges);

            return View();
        }

        public IActionResult FridgeAvailability()
        {
            // Available: active and not currently allocated
            var allocatedFridgeIds = _context.FridgeAllocations
                .Where(a => a.Status == AllocationStatus.Active)
                .Select(a => a.FridgeId);

            int available = _context.Fridges
                .Count(f => f.Status == Status.Active && !allocatedFridgeIds.Contains(f.Id));

            // Allocated
            int allocated = allocatedFridgeIds.Count();

            // Under Maintenance: fridges with a scheduled or in-progress maintenance
            var maintenanceFridgeIds = _context.MaintenanceSchedules
                .Where(s => !s.IsDeleted &&
                            (s.Status == MaintenanceStatus.Scheduled || s.Status == MaintenanceStatus.InProgress))
                .Select(s => s.FridgeId)
                .Distinct();
            int maintenance = _context.Fridges
                .Count(f => f.Status != Status.Inactive && maintenanceFridgeIds.Contains(f.Id));

            // Faulty: fridges with an open fault (not repaired or closed)
            var faultyFridgeIds = _context.FaultReports
                .Where(f => !f.IsDeleted &&
                            f.Status != FaultStatus.Repaired &&
                            f.Status != FaultStatus.Closed)
                .Select(f => f.FridgeId)
                .Distinct();
            int faulty = _context.Fridges
                .Count(f => f.Status != Status.Inactive && faultyFridgeIds.Contains(f.Id));

            // Scrapped
            int scrapped = _context.Fridges.Count(f => f.Status == Status.Inactive);

            // Reserved (placeholder for future use)
            int reserved = 0;

            // Build chart data
            var labels = new[] { "Available", "Allocated", "Maintenance", "Faulty", "Scrapped", "Reserved" };
            var data = new[] { available, allocated, maintenance, faulty, scrapped, reserved };
            var colors = new[] { "#3ECF8E", "#5FD4E0", "#F5A623", "#F0637A", "#8FAEBE", "#A78BFA" };

            ViewBag.Labels = JsonSerializer.Serialize(labels);
            ViewBag.Data = JsonSerializer.Serialize(data);
            ViewBag.Colors = JsonSerializer.Serialize(colors);

            return View();
        }


        public async Task<IActionResult> AllocationCalendar()
        {
            var events = new List<object>();

            // 1. Allocations (installations / deliveries)
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Include(a => a.Fridge)
                .ToListAsync();

            foreach (var a in allocations)
            {
                events.Add(new
                {
                    title = $"📦 {a.Fridge?.SerialNumber} → {a.Customer?.Name}",
                    start = a.AllocationDate.ToString("yyyy-MM-dd"),
                    color = "#3ECF8E",
                    textColor = "#fff"
                });

                // 2. Returns
                if (a.ReturnDate.HasValue)
                {
                    events.Add(new
                    {
                        title = $"↩️ {a.Fridge?.SerialNumber} from {a.Customer?.Name}",
                        start = a.ReturnDate.Value.ToString("yyyy-MM-dd"),
                        color = "#F5A623",
                        textColor = "#000"
                    });
                }
            }

            // 3. Maintenance visits (customer visits)
            var schedules = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .Where(s => !s.IsDeleted)
                .ToListAsync();

            foreach (var s in schedules)
            {
                events.Add(new
                {
                    title = $"🔧 {s.Fridge?.SerialNumber} maintenance",
                    start = s.ScheduledDate.ToString("yyyy-MM-dd"),
                    color = "#5FD4E0",
                    textColor = "#fff"
                });
            }

            ViewBag.CalendarEvents = JsonSerializer.Serialize(events);
            return View();
        }


        public async Task<IActionResult> AllocationQRCode(int id)
        {
            var allocation = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .ThenInclude(f => f!.Supplier)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id && a.Status == AllocationStatus.Active);

            if (allocation == null) return NotFound();

            var fridge = allocation.Fridge!;

            // Get warranty info from fridge model (if any)
            // We'll just pass the fridge, warranty is already on Fridge model (WarrantyExpiryDate)
            ViewBag.Fridge = fridge;
            ViewBag.Customer = allocation.Customer;
            ViewBag.Allocation = allocation;

            return View();
        }



        public IActionResult GenerateAllocationQRCode(int id)
        {
            // We'll use the fridge id from the allocation
            var allocation = _context.FridgeAllocations
                .FirstOrDefault(a => a.Id == id && a.Status == AllocationStatus.Active);
            if (allocation == null) return NotFound();

            var url=Url.Action("FridgeProfile", "Customer", new { id = allocation.FridgeId }, Request.Scheme);
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(10);

            return File(qrCodeBytes, "image/png");
        }


        public IActionResult GenerateBarcode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest();

            var barcode = new NetBarcode.Barcode(text, NetBarcode.Type.Code128);
            var bytes = barcode.GetByteArray();
            return File(bytes, "image/png");
        }


        [HttpGet]
        public async Task<IActionResult> CustomerNotes(int id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == Status.Active);
            if (customer == null) return NotFound();

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerNotes(int id, Customer model)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == Status.Active);
            if (customer == null) return NotFound();

            // Update only the note fields
            customer.Notes = model.Notes;
            customer.SpecialInstructions = model.SpecialInstructions;
            customer.OperatingHours = model.OperatingHours;
            customer.PreferredContact = model.PreferredContact;
            customer.DeliveryInstructions = model.DeliveryInstructions;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Customer notes updated.";
            return RedirectToAction(nameof(CustomerNotes), new { id });
        }





    }
}