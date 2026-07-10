using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using System.Text.Json;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "FAULTTECHNICIAN")]
    public class FaultTechnicianController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FaultTechnicianController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper: Get current technician employee record
        private async Task<User?> GetCurrentTechnicianAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            return await _context.Users
                .FirstOrDefaultAsync(e => e.Email == userEmail && e.Role == UserRole.FAULTTECHNICIAN && e.Status == Status.Active);
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> DashBoard()
        {
            var tech = await GetCurrentTechnicianAsync();
            if (tech == null) return RedirectToAction("AccessDenied", "Account");

            // ---------- KPI counts ----------
            int totalOpen = await _context.FaultReports.CountAsync(f => !f.IsDeleted && f.Status != FaultStatus.Closed && f.Status != FaultStatus.Cancelled);
            int assignedToMe = await _context.FaultReports.CountAsync(f => f.AssignedTechnicianId == tech.Id && !f.IsDeleted && f.Status != FaultStatus.Closed);
            int highPriority = await _context.FaultReports.CountAsync(f => !f.IsDeleted && f.Priority >= FaultPriority.High && f.Status != FaultStatus.Closed);

            ViewBag.TotalOpen = totalOpen;
            ViewBag.AssignedToMe = assignedToMe;
            ViewBag.HighPriority = highPriority;

            // Average repair time (days) for resolved faults
            var resolvedFaults = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.ResolvedDate.HasValue && f.ReportedDate != null)
                .ToListAsync();
            double avgRepairDays = resolvedFaults.Any()
                ? resolvedFaults.Average(f => (f.ResolvedDate!.Value - f.ReportedDate).TotalDays)
                : 0;
            ViewBag.AvgRepairDays = avgRepairDays.ToString("F1");

            // ---------- CHART DATA ----------
            // 1. Faults by Status (doughnut)
            var statusCounts = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            ViewBag.FaultStatusLabels = JsonSerializer.Serialize(statusCounts.Select(x => x.Status));
            ViewBag.FaultStatusData = JsonSerializer.Serialize(statusCounts.Select(x => x.Count));

            // 2. Faults by Priority (bar)
            var priorityCounts = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.Priority)
                .Select(g => new { Priority = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            ViewBag.FaultPriorityLabels = JsonSerializer.Serialize(priorityCounts.Select(x => x.Priority));
            ViewBag.FaultPriorityData = JsonSerializer.Serialize(priorityCounts.Select(x => x.Count));

            // 3. Monthly Repairs (current year)
            var currentYear = DateTime.Today.Year;
            var monthlyRepairs = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.Status == FaultStatus.Repaired && f.ResolvedDate.HasValue && f.ResolvedDate.Value.Year == currentYear)
                .GroupBy(f => f.ResolvedDate!.Value.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();
            var months = Enumerable.Range(1, 12).Select(m => new DateTime(currentYear, m, 1).ToString("MMM")).ToList();
            var repairCounts = months.Select(m => monthlyRepairs.FirstOrDefault(x => x.Month == DateTime.ParseExact(m, "MMM", null).Month)?.Count ?? 0).ToList();
            ViewBag.RepairsMonthLabels = JsonSerializer.Serialize(months);
            ViewBag.RepairsMonthData = JsonSerializer.Serialize(repairCounts);

            // 4. Most Common Fault Types (top 10 descriptions)
            var topDescriptions = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.Description)
                .Select(g => new { Description = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();
            ViewBag.FaultDescLabels = JsonSerializer.Serialize(topDescriptions.Select(x => x.Description));
            ViewBag.FaultDescData = JsonSerializer.Serialize(topDescriptions.Select(x => x.Count));

            // 5. Repairs Per Technician (only repaired faults)
            var repairsPerTech = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.Status == FaultStatus.Repaired && f.AssignedTechnicianId != null)
                .GroupBy(f => f.AssignedTechnicianId)
                .Select(g => new {
                    TechnicianName = _context.Users.FirstOrDefault(u => u.Id == g.Key).FullName ?? "Unknown",
                    Count = g.Count()
                })
                .ToListAsync();
            ViewBag.TechLabels = JsonSerializer.Serialize(repairsPerTech.Select(x => x.TechnicianName));
            ViewBag.TechData = JsonSerializer.Serialize(repairsPerTech.Select(x => x.Count));

            // ---------- RECENT ACTIVITY ----------
            var activities = new List<object>();

            // Latest faults reported (top 5)
            var latestFaults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Where(f => !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .Take(5)
                .Select(f => new {
                    Date = f.ReportedDate,
                    Description = $"Fault #{f.Id} – {f.Fridge!.SerialNumber} ({f.ReportedByCustomer!.Name})",
                    Type = "Reported"
                }).ToListAsync();
            foreach (var a in latestFaults) activities.Add(a);

            // Recently repaired fridges (top 5)
            var recentRepairs = await _context.FaultReports
                .Include(f => f.Fridge)
                .Where(f => !f.IsDeleted && f.Status == FaultStatus.Repaired && f.ResolvedDate != null)
                .OrderByDescending(f => f.ResolvedDate)
                .Take(5)
                .Select(f => new {
                    Date = f.ResolvedDate!.Value,
                    Description = $"Fridge {f.Fridge!.SerialNumber} repaired",
                    Type = "Repaired"
                }).ToListAsync();
            foreach (var a in recentRepairs) activities.Add(a);

            // Customers waiting longest (oldest open faults)
            var oldestOpen = await _context.FaultReports
                .Include(f => f.ReportedByCustomer)
                .Include(f => f.Fridge)
                .Where(f => !f.IsDeleted && f.Status != FaultStatus.Closed && f.Status != FaultStatus.Cancelled && f.Status != FaultStatus.Repaired)
                .OrderBy(f => f.ReportedDate)
                .Take(5)
                .Select(f => new {
                    Date = f.ReportedDate,
                    Description = $"Waiting since {f.ReportedDate:yyyy-MM-dd} – {f.Fridge!.SerialNumber} ({f.ReportedByCustomer!.Name})",
                    Type = "Waiting"
                }).ToListAsync();
            foreach (var a in oldestOpen) activities.Add(a);

            // Upcoming repair schedule (scheduled but not yet repaired)
            var upcomingSchedule = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .Where(f => !f.IsDeleted && f.ScheduledRepairDate != null && f.Status != FaultStatus.Repaired && f.Status != FaultStatus.Closed)
                .OrderBy(f => f.ScheduledRepairDate)
                .Take(5)
                .Select(f => new {
                    Date = f.ScheduledRepairDate!.Value,
                    Description = $"Scheduled: {f.Fridge!.SerialNumber} by {f.AssignedTechnician!.FullName}",
                    Type = "Scheduled"
                }).ToListAsync();
            foreach (var a in upcomingSchedule) activities.Add(a);

            // Sort combined activity by date descending, take top 15
            var sortedActivities = activities.OrderByDescending(a => ((DateTime)a.GetType().GetProperty("Date")!.GetValue(a))).Take(15).ToList();
            ViewBag.RecentActivities = sortedActivities;

            // Recent faults table (top 10, as before)
            var recentFaults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Where(f => !f.IsDeleted)
                .OrderByDescending(f => f.Priority)
                .ThenByDescending(f => f.ReportedDate)
                .Take(10)
                .ToListAsync();

            return View(recentFaults);
        }





        // ==================== VIEW ALL FAULTS ====================
        public async Task<IActionResult> Index(string? statusFilter, string? priorityFilter)
        {
            IQueryable<FaultReport> query = _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Include(f => f.AssignedTechnician)
                .Where(f => !f.IsDeleted);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (Enum.TryParse<FaultStatus>(statusFilter, out var status))
                    query = query.Where(f => f.Status == status);
            }
            if (!string.IsNullOrEmpty(priorityFilter))
            {
                if (Enum.TryParse<FaultPriority>(priorityFilter, out var priority))
                    query = query.Where(f => f.Priority == priority);
            }

            var faults = await query.OrderByDescending(f => f.Priority).ThenBy(f => f.ReportedDate).ToListAsync();
            return View(faults);
        }

        public async Task<IActionResult> ProcessFault(int? id)
        {
            if (id == null) return NotFound();

            var fault = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Include(f => f.AssignedTechnician)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

            if (fault == null) return NotFound();

            ViewBag.WorkflowSteps = GetWorkflowSteps();
            ViewBag.CurrentStepIndex = GetWorkflowSteps().FindIndex(s => s.Status == fault.Status);

            // Checklist data
            var completedChecks = string.IsNullOrEmpty(fault.RepairChecklistJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(fault.RepairChecklistJson) ?? new List<string>();

            ViewBag.RequiredChecklistItems = RequiredRepairChecks;
            ViewBag.CompletedChecks = completedChecks;
            ViewBag.ChecklistItemsJson = JsonSerializer.Serialize(RequiredRepairChecks);

            // Show technician selection only when fault is Reported
            if (fault.Status == FaultStatus.Reported)
            {
                await PopulateTechnicianList(fault);
            }

            return View(fault);
        }

        // ==================== PROCESS FAULT ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessFault(int id, FaultReport model, string action)
        {
            var fault = await _context.FaultReports.FindAsync(id);
            if (fault == null) return NotFound();

            var workflowSteps = GetWorkflowSteps();
            int currentIdx = workflowSteps.FindIndex(s => s.Status == fault.Status);
            if (currentIdx < 0) currentIdx = 0;

            // 1) Save all editable fields
            fault.RepairNotes = model.RepairNotes;
            fault.TechnicianNotes = model.TechnicianNotes;
            fault.CustomerNotes = model.CustomerNotes;
            fault.ManagerNotes = model.ManagerNotes;
            fault.EstimatedArrivalTime = model.EstimatedArrivalTime;
            fault.TravelTimeMinutes = model.TravelTimeMinutes;
            fault.RepairDurationMinutes = model.RepairDurationMinutes;
            fault.ScheduledRepairDate = model.ScheduledRepairDate;
            fault.DiagnosisNotes = model.DiagnosisNotes;
            fault.ResolutionNotes = model.ResolutionNotes;

            // 2) Save checklist items (sent as form array "checklistItems[]")
            var checkedItems = Request.Form["checklistItems"].ToList();
            fault.RepairChecklistJson = JsonSerializer.Serialize(checkedItems);

            // ─── Assign technician (only when action == "assign") ───
            if (action == "assign")
            {
                if (model.AssignedTechnicianId.HasValue)
                {
                    fault.AssignedTechnicianId = model.AssignedTechnicianId.Value;
                    fault.Status = FaultStatus.Assigned;
                    TempData["SuccessMessage"] = "Technician assigned. Workflow advanced.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Please select a technician.";
                    await PopulateTechnicianList(fault);
                    ViewBag.WorkflowSteps = workflowSteps;
                    ViewBag.CurrentStepIndex = currentIdx;
                    ViewBag.RequiredChecklistItems = RequiredRepairChecks;
                    ViewBag.CompletedChecks = checkedItems;
                    ViewBag.ChecklistItemsJson = JsonSerializer.Serialize(RequiredRepairChecks);
                    return View(fault);
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ProcessFault), new { id });
            }

            // ─── Advance workflow step ───
            if (action == "advance")
            {
                // Cannot advance from closed/cancelled
                if (fault.Status == FaultStatus.Closed || fault.Status == FaultStatus.Cancelled)
                {
                    TempData["ErrorMessage"] = "This fault cannot be advanced further.";
                    ViewBag.WorkflowSteps = workflowSteps;
                    ViewBag.CurrentStepIndex = currentIdx;
                    ViewBag.RequiredChecklistItems = RequiredRepairChecks;
                    ViewBag.CompletedChecks = checkedItems;
                    ViewBag.ChecklistItemsJson = JsonSerializer.Serialize(RequiredRepairChecks);
                    return View(fault);
                }

                int nextIdx = currentIdx + 1;
                if (nextIdx >= workflowSteps.Count)
                {
                    TempData["InfoMessage"] = "This fault is already at the final step.";
                    ViewBag.WorkflowSteps = workflowSteps;
                    ViewBag.CurrentStepIndex = currentIdx;
                    ViewBag.RequiredChecklistItems = RequiredRepairChecks;
                    ViewBag.CompletedChecks = checkedItems;
                    ViewBag.ChecklistItemsJson = JsonSerializer.Serialize(RequiredRepairChecks);
                    return View(fault);
                }

                var nextStatus = workflowSteps[nextIdx].Status;

                // 🔒 REPAIR CHECKLIST GATE
                if (nextStatus == FaultStatus.Repaired ||
                    nextStatus == FaultStatus.CustomerConfirmed ||
                    nextStatus == FaultStatus.Closed)
                {
                    var completedChecks = string.IsNullOrEmpty(fault.RepairChecklistJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(fault.RepairChecklistJson) ?? new List<string>();

                    bool allDone = RequiredRepairChecks.All(c => completedChecks.Contains(c));
                    if (!allDone)
                    {
                        TempData["ErrorMessage"] = "You must complete the repair checklist before finishing the repair.";
                        ViewBag.WorkflowSteps = workflowSteps;
                        ViewBag.CurrentStepIndex = currentIdx;
                        ViewBag.RequiredChecklistItems = RequiredRepairChecks;
                        ViewBag.CompletedChecks = completedChecks;
                        ViewBag.ChecklistItemsJson = JsonSerializer.Serialize(RequiredRepairChecks);
                        return View(fault);
                    }
                }

                // Advance
                fault.Status = nextStatus;
                TempData["SuccessMessage"] = $"Status changed to {workflowSteps[nextIdx].Label}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid workflow action.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ProcessFault), new { id });
        }





        // ==================== MY ASSIGNED FAULTS ====================
        public async Task<IActionResult> MyAssignedFaults()
        {
            var tech = await GetCurrentTechnicianAsync();
            if (tech == null) return RedirectToAction("AccessDenied", "Account");

            var faults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Where(f => f.AssignedTechnicianId == tech.Id && !f.IsDeleted && f.Status != FaultStatus.Closed)
                .OrderByDescending(f => f.Priority)
                .ToListAsync();

            return View(faults);
        }

        // ==================== VIEW FAULT DETAILS ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var fault = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Include(f => f.AssignedTechnician)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (fault == null) return NotFound();
            return View(fault);
        }



        // ==================== SCHEDULING CALENDAR ====================
        public IActionResult ScheduleCalendar()
        {
            return View();
        }

        /// <summary>
        /// Returns JSON events for FullCalendar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFaultEvents()
        {
            var faults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .Where(f => !f.IsDeleted && f.ScheduledRepairDate.HasValue)
                .ToListAsync();

            var events = faults.Select(f => {
                DateTime start = f.ScheduledRepairDate!.Value;
                DateTime? end = f.RepairDurationMinutes.HasValue
                    ? start.AddMinutes(f.RepairDurationMinutes.Value)
                    : start.AddHours(1); // default 1 hour if no duration

                return new
                {
                    id = f.Id,
                    title = $"🔧 {f.Fridge?.SerialNumber} – {f.Description}",
                    start = start.ToString("o"),
                    end = end.Value.ToString("o"),
                    color = f.Priority >= FaultPriority.High ? "#F0637A" : "#5FD4E0",
                    extendedProps = new
                    {
                        technician = f.AssignedTechnician?.FullName ?? "Unassigned",
                        status = f.Status.ToString()
                    }
                };
            });

            return Json(events);
        }


        /// <summary>
        /// Updates a fault’s ScheduledRepairDate after drag‑and‑drop.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateFaultSchedule([FromBody] FaultScheduleUpdate model)
        {
            var fault = await _context.FaultReports.FindAsync(model.Id);
            if (fault == null) return NotFound();

            if (fault.AssignedTechnicianId.HasValue)
            {
                bool conflict = await _context.FaultReports
                    .AnyAsync(f => f.AssignedTechnicianId == fault.AssignedTechnicianId
                        && f.Id != model.Id
                        && !f.IsDeleted
                        && f.ScheduledRepairDate.HasValue
                        && f.ScheduledRepairDate.Value.Date == model.NewDate.Date
                        && Math.Abs((f.ScheduledRepairDate.Value - model.NewDate).TotalMinutes) < 60);
                if (conflict)
                    return Conflict("This technician already has a repair around this time. Please choose a different slot.");
            }

            fault.ScheduledRepairDate = model.NewDate;
            await _context.SaveChangesAsync();
            return Ok();
        }







        private async Task PopulateTechnicianList(FaultReport fault)
        {
            var technicians = await _context.Users
                .Where(e => e.Role == UserRole.FAULTTECHNICIAN && e.Status == Status.Active)
                .ToListAsync();

            var technicianList = new List<object>();
            foreach (var tech in technicians)
            {
                int openJobs = await _context.FaultReports
                    .CountAsync(f => f.AssignedTechnicianId == tech.Id && !f.IsDeleted
                        && f.Status != FaultStatus.Closed && f.Status != FaultStatus.Cancelled && f.Status != FaultStatus.Repaired);
                var lastCompleted = await _context.FaultReports
                    .Where(f => f.AssignedTechnicianId == tech.Id && f.Status == FaultStatus.Repaired && f.ResolvedDate.HasValue)
                    .OrderByDescending(f => f.ResolvedDate)
                    .FirstOrDefaultAsync();
                string lastCompletedDesc = lastCompleted != null
                    ? $"{lastCompleted.ResolvedDate:yyyy-MM-dd} – {lastCompleted.Fridge?.SerialNumber}"
                    : "None";

                technicianList.Add(new
                {
                    Id = tech.Id,
                    Name = tech.FullName,
                    OpenJobs = openJobs,
                    Rating = tech.Rating,
                    Availability = openJobs < 3 ? "Available" : "Busy",
                    LastCompletedJob = lastCompletedDesc,
                    IsCurrent = tech.Id == fault.AssignedTechnicianId
                });
            }
            var sorted = technicianList.OrderBy(t => t.GetType().GetProperty("OpenJobs")!.GetValue(t)).ToList();
            ViewBag.TechnicianList = sorted;
            ViewBag.RecommendedTechnicianId = sorted.First().GetType().GetProperty("Id")!.GetValue(sorted.First());
        }

        // ==================== QR CODE FRIDGE LOOKUP ====================
        public async Task<IActionResult> FridgeQRList()
        {
            var fridges = await _context.Fridges
                .Where(f => f.Status == Status.Active)
                .OrderBy(f => f.SerialNumber)
                .ToListAsync();
            return View(fridges);
        }

        public IActionResult GenerateFridgeQRCode(int id)
        {
            var fridge = _context.Fridges.FirstOrDefault(f => f.Id == id);
            if (fridge == null) return NotFound();

            var url = Url.Action("FridgeProfile", "Admin", new { id = fridge.Id }, Request.Scheme);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(10);

            return File(qrCodeBytes, "image/png");
        }


        public async Task<IActionResult> FridgeHistory(int id)
        {
            var fridge = await _context.Fridges
                .Include(f => f.Supplier)
                .FirstOrDefaultAsync(f => f.Id == id && f.Status == Status.Active);

            if (fridge == null) return NotFound();

            // Allocation history
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Where(a => a.FridgeId == id)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            // Fault history with technician
            var faults = await _context.FaultReports
                .Include(f => f.AssignedTechnician)
                .Where(f => f.FridgeId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // Maintenance history (schedules that have a technician assigned)
            var maintenanceSchedules = await _context.MaintenanceSchedules
                .Include(s => s.AssignedTechnician)
                .Where(s => s.FridgeId == id && !s.IsDeleted)
                .OrderByDescending(s => s.ScheduledDate)
                .ToListAsync();

            // Completed maintenance logs
            var maintenanceLogs = await _context.MaintenanceLogs
                .Include(l => l.Technician)
                .Where(l => l.MaintenanceSchedule!.FridgeId == id && !l.IsDeleted)
                .OrderByDescending(l => l.CompletedDate)
                .ToListAsync();

            // Build timeline
            var timeline = new List<dynamic>();

            // Purchased event
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

            // Allocations
            foreach (var alloc in allocations)
            {
                timeline.Add(new
                {
                    Date = alloc.AllocationDate,
                    Type = alloc.Status == AllocationStatus.Active ? "Allocated" : "Returned",
                    Description = alloc.Status == AllocationStatus.Active
                        ? $"Allocated to {alloc.Customer?.Name}"
                        : $"Returned from {alloc.Customer?.Name}",
                    Icon = alloc.Status == AllocationStatus.Active ? "fa-hand-holding" : "fa-rotate-left",
                    Color = alloc.Status == AllocationStatus.Active ? "#3ECF8E" : "#F5A623"
                });
            }

            // Faults & repairs
            foreach (var fault in faults)
            {
                timeline.Add(new
                {
                    Date = fault.ReportedDate,
                    Type = "Fault Reported",
                    Description = $"{fault.Description} – Technician: {fault.AssignedTechnician?.FullName ?? "Unassigned"}",
                    Icon = "fa-exclamation-triangle",
                    Color = "#F5A623"
                });

                if (fault.Status == FaultStatus.Repaired && fault.ResolvedDate.HasValue)
                {
                    timeline.Add(new
                    {
                        Date = fault.ResolvedDate.Value,
                        Type = "Fault Repaired",
                        Description = $"Repair completed by {fault.AssignedTechnician?.FullName ?? "technician"}",
                        Icon = "fa-circle-check",
                        Color = "#3ECF8E"
                    });
                }
            }

            // Scheduled maintenance
            foreach (var sched in maintenanceSchedules)
            {
                timeline.Add(new
                {
                    Date = sched.ScheduledDate,
                    Type = "Maintenance Scheduled",
                    Description = $"Technician: {sched.AssignedTechnician?.FullName ?? "Unassigned"}",
                    Icon = "fa-calendar-check",
                    Color = "#5D9CEC"
                });
            }

            // Completed maintenance logs
            foreach (var log in maintenanceLogs)
            {
                timeline.Add(new
                {
                    Date = log.CompletedDate ?? log.CompletedDate,
                    Type = "Maintenance Completed",
                    Description = $"{log.ServiceNotes ?? "Routine maintenance"} – Technician: {log.Technician?.FullName ?? "Unknown"}",
                    Icon = "fa-wrench",
                    Color = "#5D9CEC"
                });
            }

            // Sort newest first
            timeline = timeline.OrderByDescending(e => e.Date).ToList();

            ViewBag.Fridge = fridge;
            ViewBag.Timeline = timeline;
            ViewBag.TimelineCount = timeline.Count;

            return View();
        }

        public async Task<IActionResult> FaultAnalytics()
        {
            // Top failing fridge models (most faults)
            var topFailingModels = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .Include(f => f.Fridge)
                .GroupBy(f => f.Fridge!.Model ?? "Unknown")
                .Select(g => new { Model = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            // Most expensive repairs (assuming RepairCost is filled)
            var expensiveRepairs = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.RepairCost.HasValue)
                .OrderByDescending(f => f.RepairCost)
                .Take(5)
                .Select(f => new
                {
                    f.Id,
                    f.Fridge!.SerialNumber,
                    f.Description,
                    f.RepairCost
                })
                .ToListAsync();

            // Average repair cost
            decimal avgRepairCost = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.RepairCost.HasValue)
                .AverageAsync(f => f.RepairCost!.Value);

            // Average response time (from Reported to Assigned, in hours)
            var assignedFaults = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.AssignedDate.HasValue)
                .ToListAsync();
            double avgResponseHours = 0;
            if (assignedFaults.Any())
                avgResponseHours = assignedFaults
                    .Average(f => (f.AssignedDate!.Value - f.ReportedDate).TotalHours);

            // Average completion time (from Reported to Resolved, in days)
            var resolvedFaults = await _context.FaultReports
                .Where(f => !f.IsDeleted && f.ResolvedDate.HasValue)
                .ToListAsync();
            double avgCompletionDays = 0;
            if (resolvedFaults.Any())
                avgCompletionDays = resolvedFaults
                    .Average(f => (f.ResolvedDate!.Value - f.ReportedDate).TotalDays);

            // Repeat faults (fridges with >1 fault)
            var repeatFridges = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.FridgeId)
                .Select(g => new { FridgeId = g.Key, Count = g.Count() })
                .Where(x => x.Count > 1)
                .ToListAsync();
            int repeatFaultCount = repeatFridges.Sum(r => r.Count - 1); // total extra faults beyond the first

            // Most common fault description
            var commonFaults = await _context.FaultReports
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.Description)
                .Select(g => new { Description = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            ViewBag.TopFailingModels = topFailingModels;
            ViewBag.ExpensiveRepairs = expensiveRepairs;
            ViewBag.AvgRepairCost = avgRepairCost.ToString("C");
            ViewBag.AvgResponseHours = avgResponseHours.ToString("F1");
            ViewBag.AvgCompletionDays = avgCompletionDays.ToString("F1");
            ViewBag.RepeatFaultCount = repeatFaultCount;
            ViewBag.CommonFaults = commonFaults;

            // For chart
            ViewBag.TopModelsLabels = JsonSerializer.Serialize(topFailingModels.Select(x => x.Model));
            ViewBag.TopModelsData = JsonSerializer.Serialize(topFailingModels.Select(x => x.Count));
            ViewBag.CommonFaultLabels = JsonSerializer.Serialize(commonFaults.Select(x => x.Description));
            ViewBag.CommonFaultData = JsonSerializer.Serialize(commonFaults.Select(x => x.Count));

            return View();
        }





        private List<WorkflowStep> GetWorkflowSteps()
        {
            return new List<WorkflowStep>
    {
        new() { Status = FaultStatus.Reported, Label = "Reported", Icon = "fa-paper-plane" },
        new() { Status = FaultStatus.Assigned, Label = "Assigned", Icon = "fa-user-check" },
        new() { Status = FaultStatus.Scheduled, Label = "Scheduled", Icon = "fa-calendar-alt" },
        new() { Status = FaultStatus.Travelling, Label = "Travelling", Icon = "fa-truck" },
        new() { Status = FaultStatus.OnSite, Label = "On Site", Icon = "fa-location-dot" },
        new() { Status = FaultStatus.Diagnosing, Label = "Diagnosing", Icon = "fa-stethoscope" },
        new() { Status = FaultStatus.WaitingForParts, Label = "Waiting for Parts", Icon = "fa-box" },
        new() { Status = FaultStatus.Repairing, Label = "Repairing", Icon = "fa-wrench" },
        new() { Status = FaultStatus.Testing, Label = "Testing", Icon = "fa-vial" },
        new() { Status = FaultStatus.Repaired, Label = "Repaired", Icon = "fa-circle-check" },
        new() { Status = FaultStatus.CustomerConfirmed, Label = "Customer Confirmed", Icon = "fa-thumbs-up" },
        new() { Status = FaultStatus.Closed, Label = "Closed", Icon = "fa-lock" }
    };
        }


        private static readonly List<string> RequiredRepairChecks = new()
{
    "Power checked",
    "Compressor checked",
    "Gas pressure checked",
    "Temperature tested",
    "Door seal inspected",
    "Fan working",
    "Lights working"
};






    }




    
}