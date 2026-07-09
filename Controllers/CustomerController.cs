using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "CUSTOMER")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper: Get current Customer record from logged-in user
        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == userEmail && c.Status == Status.Active);
        }

        public async Task<IActionResult> DashBoard()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            // ---------- Card statistics (same as before) ----------
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .ToListAsync();
            int allocatedFridges = allocations.Count;

            var customerFaults = await _context.FaultReports
                .Where(f => f.ReportedByCustomerId == customer.Id && !f.IsDeleted)
                .ToListAsync();

            int pendingFaults = customerFaults.Count(f => f.Status == FaultStatus.Reported);
            int openFaults = customerFaults.Count(f => f.Status == FaultStatus.Assigned || f.Status == FaultStatus.InProgress);
            int completedRepairs = customerFaults.Count(f => f.Status == FaultStatus.Repaired);

            var fridgeIds = allocations.Select(a => a.FridgeId).ToList();

            int upcomingMaintenance = await _context.MaintenanceSchedules
                .CountAsync(s => fridgeIds.Contains(s.FridgeId) && !s.IsDeleted &&
                                 s.Status == MaintenanceStatus.Scheduled && s.ScheduledDate >= DateTime.Today);

            var customerRequests = await _context.FridgeRequests
                .Where(r => r.CustomerId == customer.Id && !r.IsDeleted)
                .ToListAsync();

            int pendingRequests = customerRequests.Count(r => r.Status == FridgeRequestStatus.Pending);
            int approvedRequests = customerRequests.Count(r => r.Status == FridgeRequestStatus.Approved);

            int completedServices = await _context.MaintenanceLogs
                .CountAsync(l => l.MaintenanceSchedule != null && fridgeIds.Contains(l.MaintenanceSchedule.FridgeId) && !l.IsDeleted);

            var recentFaults = customerFaults.OrderByDescending(f => f.ReportedDate).Take(5).ToList();

            var fridgeListJson = System.Text.Json.JsonSerializer.Serialize(
                allocations.Select(a => new { id = a.FridgeId, serial = a.Fridge?.SerialNumber }));

            // ==================== CHART DATA ====================
            // ---- 1. Faults This Year (monthly breakdown for current year) ----
            var currentYear = DateTime.Today.Year;
            var faultsThisYear = _context.FaultReports
                .Where(f => f.ReportedByCustomerId == customer.Id && f.ReportedDate.Year == currentYear && !f.IsDeleted)
                .GroupBy(f => f.ReportedDate.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();
            var months = Enumerable.Range(1, 12)
                .Select(m => new DateTime(currentYear, m, 1).ToString("MMM"))
                .ToList();
            var faultCounts = Enumerable.Range(1, 12)
                .Select(m => faultsThisYear.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToList();
            ViewBag.FaultsYearLabels = System.Text.Json.JsonSerializer.Serialize(months);
            ViewBag.FaultsYearData = System.Text.Json.JsonSerializer.Serialize(faultCounts);

            // ---- 2. Maintenance History (monthly completed logs for current year) ----
            var maintLogsThisYear = _context.MaintenanceLogs
                .Where(l => l.MaintenanceSchedule != null && fridgeIds.Contains(l.MaintenanceSchedule.FridgeId)
                            && !l.IsDeleted && l.CompletedDate.HasValue && l.CompletedDate.Value.Year == currentYear)
                .GroupBy(l => l.CompletedDate!.Value.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();
            var maintCounts = Enumerable.Range(1, 12)
                .Select(m => maintLogsThisYear.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToList();
            ViewBag.MaintHistoryLabels = System.Text.Json.JsonSerializer.Serialize(months);
            ViewBag.MaintHistoryData = System.Text.Json.JsonSerializer.Serialize(maintCounts);

            // ---- 3. Repairs per Month (completed faults per month this year) ----
            var repairsThisYear = _context.FaultReports
                .Where(f => f.ReportedByCustomerId == customer.Id && f.ReportedDate.Year == currentYear
                            && f.Status == FaultStatus.Repaired && !f.IsDeleted)
                .GroupBy(f => f.ReportedDate.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();
            var repairCounts = Enumerable.Range(1, 12)
                .Select(m => repairsThisYear.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
                .ToList();
            ViewBag.RepairsMonthLabels = System.Text.Json.JsonSerializer.Serialize(months);
            ViewBag.RepairsMonthData = System.Text.Json.JsonSerializer.Serialize(repairCounts);

            // ---- 4. Request Status Distribution ----
            var requestStatusCounts = _context.FridgeRequests
                .Where(r => r.CustomerId == customer.Id && !r.IsDeleted)
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToList();
            ViewBag.RequestStatusLabels = System.Text.Json.JsonSerializer.Serialize(requestStatusCounts.Select(x => x.Status));
            ViewBag.RequestStatusData = System.Text.Json.JsonSerializer.Serialize(requestStatusCounts.Select(x => x.Count));

            // ==================== CALENDAR DATA ====================
            var calendarEvents = new List<object>();

            // Upcoming maintenance (future scheduled)
            var upcomingMaintEvents = await _context.MaintenanceSchedules
                .Where(s => fridgeIds.Contains(s.FridgeId) && !s.IsDeleted &&
                            s.Status == MaintenanceStatus.Scheduled &&
                            s.ScheduledDate >= DateTime.Today)
                .Select(s => new {
                    title = $"🔧 Service: {s.Fridge!.SerialNumber}",
                    start = s.ScheduledDate.ToString("yyyy-MM-dd"),
                    color = "#5FD4E0",   // cyan
                    textColor = "#fff"
                })
                .ToListAsync();
            calendarEvents.AddRange(upcomingMaintEvents);

            // Completed maintenance logs
            var completedMaintEvents = await _context.MaintenanceLogs
                .Where(l => l.MaintenanceSchedule != null &&
                            fridgeIds.Contains(l.MaintenanceSchedule.FridgeId) &&
                            !l.IsDeleted && l.CompletedDate.HasValue)
                .Select(l => new {
                    title = $"✅ Service: {l.MaintenanceSchedule!.Fridge!.SerialNumber}",
                    start = l.CompletedDate!.Value.ToString("yyyy-MM-dd"),
                    color = "#3ECF8E",   // green
                    textColor = "#fff"
                })
                .ToListAsync();
            calendarEvents.AddRange(completedMaintEvents);

            // Faults (reported date as a proxy for repair event)
            var faultEvents = await _context.FaultReports
                .Where(f => fridgeIds.Contains(f.FridgeId) && !f.IsDeleted)
                .Select(f => new {
                    title = $"⚠️ Fault: {f.Fridge!.SerialNumber}",
                    start = f.ReportedDate.ToString("yyyy-MM-dd"),
                    color = "#F5A623",   // amber
                    textColor = "#000"
                })
                .ToListAsync();
            calendarEvents.AddRange(faultEvents);


            // ==================== TOMORROW'S MAINTENANCE REMINDER ====================
            var tomorrow = DateTime.Today.AddDays(1);
            var tomorrowMaintenance = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .FirstOrDefaultAsync(s => fridgeIds.Contains(s.FridgeId) && !s.IsDeleted &&
                                          s.Status == MaintenanceStatus.Scheduled &&
                                          s.ScheduledDate.Date == tomorrow);
            ViewBag.TomorrowMaintenance = tomorrowMaintenance;

            ViewBag.CalendarEvents = System.Text.Json.JsonSerializer.Serialize(calendarEvents);


            // Pass everything to the view
            ViewBag.Customer = customer;
            ViewBag.AllocatedFridges = allocations;
            ViewBag.AllocatedFridgesCount = allocatedFridges;
            ViewBag.PendingFaults = pendingFaults;
            ViewBag.OpenFaults = openFaults;
            ViewBag.CompletedRepairs = completedRepairs;
            ViewBag.UpcomingMaintenance = upcomingMaintenance;
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.ApprovedRequests = approvedRequests;
            ViewBag.CompletedServices = completedServices;
            ViewBag.RecentFaults = recentFaults;
            ViewBag.PendingReqList = customerRequests.Where(r => r.Status == FridgeRequestStatus.Pending || r.Status == FridgeRequestStatus.Approved).ToList();
            ViewBag.FridgeListJson = fridgeListJson;

            return View();
        }

        // ==================== MY FRIDGES ====================
        public async Task<IActionResult> MyFridges()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .ToListAsync();

            return View(allocations);
        }

        public async Task<IActionResult> InactiveMyFridges()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Cancelled)
                .ToListAsync();

            return View(allocations);
        }



        // ==================== FAULT REPORTS ====================
        public async Task<IActionResult> MyFaults()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var faults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .Where(f => f.ReportedByCustomerId == customer.Id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            var activeFridges = await _context.FridgeAllocations
    .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
    .Include(a => a.Fridge)
    .Select(a => new { id = a.FridgeId, serial = a.Fridge!.SerialNumber })
    .ToListAsync();
            ViewBag.FridgeListJson = System.Text.Json.JsonSerializer.Serialize(activeFridges);

            return View(faults);
        }

        public async Task<IActionResult> ReportFault()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var allocatedFridges = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .Select(a => a.Fridge)
                .ToListAsync();

            ViewBag.Fridges = allocatedFridges;
            return View(new FaultReport { ReportedByCustomerId = customer.Id });
        }


        public async Task<IActionResult> InactiveReportFault()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var allocatedFridges = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Cancelled)
                .Select(a => a.Fridge)
                .ToListAsync();

            ViewBag.Fridges = allocatedFridges;
            return View(new FaultReport { ReportedByCustomerId = customer.Id });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportFault(FaultReport faultReport, bool isCritical = false)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var allocation = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == faultReport.FridgeId && a.CustomerId == customer.Id && a.Status == AllocationStatus.Active);
            if (!allocation)
            {
                ModelState.AddModelError("FridgeId", "Invalid fridge selection.");
                var allocatedFridges = await _context.FridgeAllocations
                    .Include(a => a.Fridge)
                    .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                    .Select(a => a.Fridge)
                    .ToListAsync();
                ViewBag.Fridges = allocatedFridges;
                return View(faultReport);
            }

            if (ModelState.IsValid)
            {
                // Override priority if emergency checkbox was ticked
                if (isCritical)
                    faultReport.Priority = FaultPriority.Critical;

                faultReport.ReportedByCustomerId = customer.Id;
                faultReport.ReportedDate = DateTime.Now;
                faultReport.Status = FaultStatus.Reported;
                faultReport.IsDeleted = false;
                _context.Add(faultReport);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fault reported successfully. A technician will be assigned soon.";
                return RedirectToAction(nameof(MyFaults));
            }

            var fridges = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .Select(a => a.Fridge)
                .ToListAsync();
            ViewBag.Fridges = fridges;
            return View(faultReport);
        }

        public async Task<IActionResult> FaultDetails(int? id)
        {
            if (id == null) return NotFound();
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var fault = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)   // already loaded
                .FirstOrDefaultAsync(f => f.Id == id && f.ReportedByCustomerId == customer.Id && !f.IsDeleted);
            if (fault == null) return NotFound();

            // Build the progress steps (same as before)
            var steps = new[]
            {
        new { Key = "Reported",       Label = "Reported",        Icon = "fa-paper-plane",   Completed = fault.ReportedDate != null },
        new { Key = "Assigned",       Label = "Assigned",        Icon = "fa-user-check",    Completed = fault.AssignedDate != null },
        new { Key = "Travelling",     Label = "Travelling",      Icon = "fa-truck",         Completed = fault.TechnicianTravelingAt != null },
        new { Key = "Arrived",        Label = "Arrived",         Icon = "fa-location-dot",  Completed = fault.TechnicianArrivedAt != null },
        new { Key = "RepairStarted",  Label = "Repair Started",  Icon = "fa-wrench",        Completed = fault.RepairStartedAt != null },
        new { Key = "RepairCompleted",Label = "Repair Completed",Icon = "fa-circle-check",  Completed = fault.RepairCompletedAt != null || fault.Status == FaultStatus.Repaired },
        new { Key = "Closed",         Label = "Closed",          Icon = "fa-lock",          Completed = fault.Status == FaultStatus.Closed || fault.IsDeleted }
    };

            int activeIndex = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (!steps[i].Completed)
                {
                    activeIndex = i;
                    break;
                }
            }

            // Technician information – if assigned, fetch the full User record
            User? technician = null;
            if (fault.AssignedTechnicianId != null)
            {
                technician = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == fault.AssignedTechnicianId.Value);
            }

            ViewBag.Steps = steps;
            ViewBag.ActiveIndex = activeIndex;
            ViewBag.Fault = fault;
            ViewBag.Technician = technician;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelFault(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var fault = await _context.FaultReports
                .FirstOrDefaultAsync(f => f.Id == id && f.ReportedByCustomerId == customer.Id);
            if (fault != null && (fault.Status == FaultStatus.Reported || fault.Status == FaultStatus.Assigned))
            {
                fault.Status = FaultStatus.Cancelled;
                fault.IsDeleted = true;
                _context.Update(fault);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fault report cancelled.";
            }
            return RedirectToAction(nameof(MyFaults));
        }


  


        // ==================== FRIDGE REQUESTS ====================
        public async Task<IActionResult> MyFridgeRequests()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var requests = await _context.FridgeRequests
                .Where(r => r.CustomerId == customer.Id && !r.IsDeleted)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }




        public IActionResult RequestFridge()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestFridge(FridgeRequest request)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            if (ModelState.IsValid)
            {
                request.CustomerId = customer.Id;
                request.RequestDate = DateTime.Now;
                request.Status = FridgeRequestStatus.Pending;
                request.IsDeleted = false;
                _context.Add(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fridge request submitted successfully.";
                return RedirectToAction(nameof(MyFridgeRequests));
            }
            return View(request);
        }

        public async Task<IActionResult> FridgeRequestDetails(int? id)
        {
            if (id == null) return NotFound();
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var request = await _context.FridgeRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customer.Id && !r.IsDeleted);
            if (request == null) return NotFound();

            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelFridgeRequest(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var request = await _context.FridgeRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customer.Id);
            if (request != null && request.Status == FridgeRequestStatus.Pending)
            {
                request.Status = FridgeRequestStatus.Cancelled;
                request.IsDeleted = true;
                _context.Update(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fridge request cancelled.";
            }
            return RedirectToAction(nameof(MyFridgeRequests));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreFridgeRequest(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var request = await _context.FridgeRequests
                .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customer.Id);
            if (request != null && request.Status == FridgeRequestStatus.Pending)
            {
                request.Status = FridgeRequestStatus.Approved;
                request.IsDeleted = false;
                _context.Update(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fridge request cancelled.";
            }
            return RedirectToAction(nameof(MyFridgeRequests));
        }




        // ==================== MAINTENANCE VISIBILITY ====================
        public async Task<IActionResult> UpcomingMaintenance()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            // Get customer's allocated fridge IDs
            var fridgeIds = await _context.FridgeAllocations
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .Select(a => a.FridgeId)
                .ToListAsync();

            var schedules = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .Where(s => fridgeIds.Contains(s.FridgeId) && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            return View(schedules);
        }

        public async Task<IActionResult> FridgeMaintenanceHistory(int? fridgeId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            if (fridgeId == null)
            {
                // Show all customer's fridges for selection
                var allocations = await _context.FridgeAllocations
                    .Include(a => a.Fridge)
                    .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                    .ToListAsync();
                return View("SelectFridgeForHistory", allocations);
            }

            // Verify fridge belongs to customer
            var allocation = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == fridgeId && a.CustomerId == customer.Id);
            if (!allocation) return Forbid();

            var logs = await _context.MaintenanceLogs
                .Include(l => l.MaintenanceSchedule)
                .Include(l => l.Technician)
                .Where(l => l.MaintenanceSchedule!.FridgeId == fridgeId && !l.IsDeleted)
                .OrderByDescending(l => l.CompletedDate)
                .ToListAsync();

            ViewBag.Fridge = await _context.Fridges.FindAsync(fridgeId);
            return View(logs);
        }


        // ==================== FRIDGE PROFILE (TABBED) ====================
        public async Task<IActionResult> FridgeProfile(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            // Fetch the allocation + fridge + supplier
            var allocation = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                    .ThenInclude(f => f!.Supplier)
                .FirstOrDefaultAsync(a => a.FridgeId == id && a.CustomerId == customer.Id
                                          && a.Status == AllocationStatus.Active);
            if (allocation == null) return NotFound();

            var fridge = allocation.Fridge!;

            // ---------- Overview data ----------
            var lastService = await _context.MaintenanceLogs
                .Include(l => l.Technician)
                .Where(l => l.MaintenanceSchedule!.FridgeId == id && !l.IsDeleted && l.CompletedDate != null)
                .OrderByDescending(l => l.CompletedDate)
                .FirstOrDefaultAsync();

            var nextService = await _context.MaintenanceSchedules
                .Include(s => s.AssignedTechnician)
                .Where(s => s.FridgeId == id && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled
                            && s.ScheduledDate >= DateTime.Today)
                .OrderBy(s => s.ScheduledDate)
                .FirstOrDefaultAsync();

            string? technicianName = nextService?.AssignedTechnician?.FullName
                                     ?? lastService?.Technician?.FullName
                                     ?? (await _context.FaultReports
                                           .Include(f => f.AssignedTechnician)
                                           .Where(f => f.FridgeId == id && !f.IsDeleted && f.AssignedTechnician != null)
                                           .OrderByDescending(f => f.ReportedDate)
                                           .FirstOrDefaultAsync())?.AssignedTechnician?.FullName;

            // ---------- Faults (for both Faults tab & timeline) ----------
            var faults = await _context.FaultReports
                .Include(f => f.AssignedTechnician)
                .Where(f => f.FridgeId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // ---------- Maintenance (for Maintenance tab & timeline) ----------
            var upcomingSchedules = await _context.MaintenanceSchedules
                .Include(s => s.AssignedTechnician)
                .Where(s => s.FridgeId == id && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled
                            && s.ScheduledDate >= DateTime.Today)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            var completedLogs = await _context.MaintenanceLogs
                .Include(l => l.Technician)
                .Where(l => l.MaintenanceSchedule!.FridgeId == id && !l.IsDeleted && l.CompletedDate != null)
                .OrderByDescending(l => l.CompletedDate)
                .ToListAsync();

            // ---------- All allocation history (for timeline) ----------
            var allocationHistory = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Where(a => a.FridgeId == id)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            // ================= BUILD TIMELINE =================
            var timeline = new List<dynamic>();

            // 1. Purchased
            if (fridge.PurchaseDate.HasValue)
            {
                timeline.Add(new
                {
                    Date = fridge.PurchaseDate.Value,
                    Type = "Purchased",
                    Description = $"Fridge purchased from {fridge.Supplier?.Name ?? "unknown supplier"}",
                    Icon = "fa-cart-shopping",
                    Color = "#5FD4E0"
                });
            }

            // 2. Allocations (each allocation is an event)
            foreach (var alloc in allocationHistory)
            {
                timeline.Add(new
                {
                    Date = alloc.AllocationDate,
                    Type = alloc.Status == AllocationStatus.Active ? "Allocated" : "Deallocated",
                    Description = alloc.Status == AllocationStatus.Active
                        ? $"Allocated to {alloc.Customer?.Name}"
                        : $"Removed from {alloc.Customer?.Name}",
                    Icon = "fa-hand-holding",
                    Color = alloc.Status == AllocationStatus.Active ? "#3ECF8E" : "#F5A623"
                });
            }

            // 3. Faults (each fault reported)
            foreach (var fault in faults)
            {
                timeline.Add(new
                {
                    Date = fault.ReportedDate,
                    Type = "Fault Reported",
                    Description = $"{fault.Description} (Status: {fault.Status})",
                    Icon = "fa-exclamation-triangle",
                    Color = "#F5A623"
                });

                // If fault is repaired, we add a separate repair event (using today as placeholder – you can extend later)
                if (fault.Status == FaultStatus.Repaired)
                {
                    timeline.Add(new
                    {
                        Date = fault.ReportedDate.AddDays(2), // approximate; real system would store completion date
                        Type = "Fault Repaired",
                        Description = $"Repair completed by {fault.AssignedTechnician?.FullName ?? "technician"}",
                        Icon = "fa-circle-check",
                        Color = "#3ECF8E"
                    });
                }
            }

            // 4. Completed maintenance services
            foreach (var log in completedLogs)
            {
                timeline.Add(new
                {
                    Date = log.CompletedDate!.Value,
                    Type = "Service Completed",
                    Description = log.ServiceNotes ?? "Routine maintenance",
                    Icon = "fa-wrench",
                    Color = "#5D9CEC"
                });
            }

            // 5. Upcoming maintenance (future events – optional)
            foreach (var sched in upcomingSchedules)
            {
                timeline.Add(new
                {
                    Date = sched.ScheduledDate,
                    Type = "Maintenance Scheduled",
                    Description = $"Scheduled maintenance by {sched.AssignedTechnician?.FullName ?? "technician"}",
                    Icon = "fa-calendar-check",
                    Color = "#5D9CEC"
                });
            }

            // 6. Scrapped (if fridge status is inactive, we pretend it was scrapped today – adjust later)
            if (fridge.Status == Status.Inactive)
            {
                timeline.Add(new
                {
                    Date = DateTime.Now,
                    Type = "Scrapped",
                    Description = "Fridge has been decommissioned",
                    Icon = "fa-trash",
                    Color = "#F0637A"
                });
            }

            // Sort all events descending by date
            timeline = timeline.OrderByDescending(e => e.Date).ToList();

            // ---------- Pass to ViewBag ----------
            ViewBag.Fridge = fridge;
            ViewBag.Allocation = allocation;
            ViewBag.LastService = lastService;
            ViewBag.NextService = nextService;
            ViewBag.TechnicianName = technicianName;
            ViewBag.Faults = faults;
            ViewBag.UpcomingSchedules = upcomingSchedules;
            ViewBag.CompletedLogs = completedLogs;
            ViewBag.Timeline = timeline;          // <-- the timeline list

            return View();
        }


        // ==================== QR CODE ====================
        public IActionResult GenerateFridgeQRCode(int id)
        {
            // Optional: verify the fridge belongs to the current customer
            var customerId = GetCurrentCustomerAsync().Result?.Id;
            if (customerId == null) return Unauthorized();

            var allocation = _context.FridgeAllocations
                .Any(a => a.FridgeId == id && a.CustomerId == customerId && a.Status == AllocationStatus.Active);
            if (!allocation) return Forbid();

            // URL that the QR code will open – points to customer's fridge profile
            var url = Url.Action("FridgeProfile", "Customer", new { id }, Request.Scheme);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(10);

            return File(qrCodeBytes, "image/png");
        }


        // ==================== MY PROFILE ====================
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyProfile(Customer model)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            // Ensure we are updating the correct entity
            if (model.Id != customer.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(model);

            // Update allowed fields
            customer.Name = model.Name;
            customer.ContactPerson = model.ContactPerson;
            customer.PhoneNumber = model.PhoneNumber;
            customer.Email = model.Email;
            customer.Address = model.Address;
            customer.OperatingHours = model.OperatingHours;
            customer.EmergencyContact = model.EmergencyContact;
            customer.Owner = model.Owner;
            customer.BusinessType = model.BusinessType;
            customer.Latitude = model.Latitude;
            customer.Longitude = model.Longitude;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(MyProfile));
        }

        // ==================== BUSINESS INFORMATION ====================
        public async Task<IActionResult> BusinessInfo()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            return View(customer);
        }


        // ==================== REPAIR FEEDBACK ====================
        [HttpGet]
        public async Task<IActionResult> CreateFeedback(int faultId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var fault = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .FirstOrDefaultAsync(f => f.Id == faultId && f.ReportedByCustomerId == customer.Id
                                         && f.Status == FaultStatus.Repaired && !f.IsDeleted);
            if (fault == null) return NotFound();

            // Prevent duplicate feedback
            if (await _context.RepairFeedbacks.AnyAsync(fb => fb.FaultReportId == faultId))
            {
                TempData["InfoMessage"] = "You have already submitted feedback for this repair.";
                return RedirectToAction("FaultDetails", new { id = faultId });
            }

            ViewBag.Fault = fault;
            return View(new RepairFeedback { FaultReportId = faultId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFeedback(RepairFeedback feedback)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var fault = await _context.FaultReports
                .FirstOrDefaultAsync(f => f.Id == feedback.FaultReportId && f.ReportedByCustomerId == customer.Id
                                         && f.Status == FaultStatus.Repaired && !f.IsDeleted);
            if (fault == null) return NotFound("Invalid fault.");

            if (await _context.RepairFeedbacks.AnyAsync(fb => fb.FaultReportId == feedback.FaultReportId))
            {
                TempData["InfoMessage"] = "Feedback already submitted.";
                return RedirectToAction("FaultDetails", new { id = feedback.FaultReportId });
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Fault = fault;
                return View(feedback);
            }

            feedback.CreatedAt = DateTime.Now;
            _context.RepairFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you! Your feedback has been recorded.";
            return RedirectToAction("FaultDetails", new { id = feedback.FaultReportId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmMaintenanceAttendance(int scheduleId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var schedule = await _context.MaintenanceSchedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled);
            if (schedule == null) return NotFound();

            // Verify this schedule belongs to the customer
            var allocation = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == schedule.FridgeId && a.CustomerId == customer.Id && a.Status == AllocationStatus.Active);
            if (!allocation) return Forbid();

            schedule.IsConfirmed = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Attendance confirmed. The technician will be expecting you.";
            return RedirectToAction(nameof(DashBoard));
        }

        // ==================== RESCHEDULE MAINTENANCE ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleMaintenance(int scheduleId, string quickOption, DateTime? customDate, string customTime)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var schedule = await _context.MaintenanceSchedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled);
            if (schedule == null) return NotFound();

            // Verify ownership
            var allocation = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == schedule.FridgeId && a.CustomerId == customer.Id && a.Status == AllocationStatus.Active);
            if (!allocation) return Forbid();

            DateTime newDate = schedule.ScheduledDate;

            if (!string.IsNullOrEmpty(quickOption))
            {
                var today = DateTime.Today;
                switch (quickOption)
                {
                    case "Tomorrow":
                        newDate = today.AddDays(1);
                        break;
                    case "Friday":
                        int diff = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
                        if (diff == 0) diff = 7; // next Friday, not today
                        newDate = today.AddDays(diff);
                        break;
                    case "NextWeek":
                        newDate = today.AddDays(7);
                        break;
                    case "Morning":
                        newDate = schedule.ScheduledDate.Date.AddHours(9);
                        break;
                    case "Afternoon":
                        newDate = schedule.ScheduledDate.Date.AddHours(14);
                        break;
                }
            }
            else if (customDate.HasValue)
            {
                newDate = customDate.Value.Date;
                if (!string.IsNullOrEmpty(customTime))
                    if (TimeSpan.TryParse(customTime, out var ts))
                        newDate = newDate.Add(ts);
                    else
                        newDate = newDate.Add(schedule.ScheduledDate.TimeOfDay); // keep original time
            }

            schedule.ScheduledDate = newDate;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Maintenance rescheduled successfully.";
            return RedirectToAction(nameof(DashBoard));
        }

        // ==================== CANCEL MAINTENANCE VISIT ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMaintenanceVisit(int scheduleId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            var schedule = await _context.MaintenanceSchedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled);
            if (schedule == null) return NotFound();

            var allocation = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == schedule.FridgeId && a.CustomerId == customer.Id && a.Status == AllocationStatus.Active);
            if (!allocation) return Forbid();

            schedule.IsDeleted = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Maintenance visit cancelled.";
            return RedirectToAction(nameof(DashBoard));
        }


    }
}