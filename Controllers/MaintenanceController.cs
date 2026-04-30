using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "MAINTENANCETECHNICIAN")]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaintenanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper: Get current technician
        private async Task<User?> GetCurrentTechnicianAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            return await _context.Users
                .FirstOrDefaultAsync(e => e.Email == userEmail && e.Role == UserRole.MAINTENANCETECHNICIAN && e.Status == Status.Active);
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            var tech = await GetCurrentTechnicianAsync();
            if (tech == null) return RedirectToAction("AccessDenied", "Account");

            // Upcoming schedules (next 7 days)
            var upcoming = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .Where(s => !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled && s.ScheduledDate >= DateTime.Today)
                .OrderBy(s => s.ScheduledDate)
                .Take(10)
                .ToListAsync();

            // My assigned schedules
            var mySchedules = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Where(s => s.AssignedTechnicianId == tech.Id && !s.IsDeleted && s.Status == MaintenanceStatus.Scheduled)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            ViewBag.Upcoming = upcoming;
            ViewBag.MySchedules = mySchedules;

            return View();
        }

        // ==================== VIEW CUSTOMER FRIDGES ====================
        public async Task<IActionResult> CustomerFridges()
        {
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Include(a => a.Customer)
                .Where(a => a.Status == AllocationStatus.Active)
                .OrderBy(a => a.Customer.Name)
                .ToListAsync();
            return View(allocations);
        }

        // ==================== MAINTENANCE SCHEDULES ====================
        public async Task<IActionResult> Index(string? statusFilter)
        {
            IQueryable<MaintenanceSchedule> query = _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Include(s => s.AssignedTechnician)
                .Where(s => !s.IsDeleted);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (Enum.TryParse<MaintenanceStatus>(statusFilter, out var status))
                    query = query.Where(s => s.Status == status);
            }

            var schedules = await query.OrderBy(s => s.ScheduledDate).ToListAsync();
            return View(schedules);
        }

        // Create schedule (GET)
        public async Task<IActionResult> CreateSchedule()
        {
            var fridges = await _context.Fridges
                .Where(f => f.Status == Status.Active)
                .ToListAsync();
            ViewBag.Fridges = new SelectList(fridges, "Id", "SerialNumber");

            var technicians = await _context.Users
                .Where(e => e.Role == UserRole.MAINTENANCETECHNICIAN && e.Status == Status.Active)
                .ToListAsync();
            ViewBag.Technicians = new SelectList(technicians, "Id", "FullName");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(MaintenanceSchedule schedule)
        {
            if (ModelState.IsValid)
            {
                schedule.Status = MaintenanceStatus.Scheduled;
                schedule.IsDeleted = false;
                _context.Add(schedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Maintenance scheduled.";
                return RedirectToAction(nameof(Index));
            }

            var fridges = await _context.Fridges.Where(f => f.Status == Status.Active).ToListAsync();
            ViewBag.Fridges = new SelectList(fridges, "Id", "SerialNumber", schedule.FridgeId);
            var technicians = await _context.Users.Where(e => e.Role == UserRole.MAINTENANCETECHNICIAN && e.Status == Status.Active).ToListAsync();
            ViewBag.Technicians = new SelectList(technicians, "Id", "FullName", schedule.AssignedTechnicianId);
            return View(schedule);
        }

        // Edit schedule (GET)
        public async Task<IActionResult> EditSchedule(int? id)
        {
            if (id == null) return NotFound();
            var schedule = await _context.MaintenanceSchedules.FindAsync(id);
            if (schedule == null || schedule.IsDeleted) return NotFound();

            var fridges = await _context.Fridges.Where(f => f.Status == Status.Active).ToListAsync();
            ViewBag.Fridges = new SelectList(fridges, "Id", "SerialNumber", schedule.FridgeId);
            var technicians = await _context.Users.Where(e => e.Role == UserRole.MAINTENANCETECHNICIAN && e.Status == Status.Active).ToListAsync();
            ViewBag.Technicians = new SelectList(technicians, "Id", "FullName", schedule.AssignedTechnicianId);

            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(int id, MaintenanceSchedule schedule)
        {
            if (id != schedule.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                var fridges = await _context.Fridges.Where(f => f.Status == Status.Active).ToListAsync();
                ViewBag.Fridges = new SelectList(fridges, "Id", "SerialNumber", schedule.FridgeId);
                var technicians = await _context.Users.Where(e => e.Role == UserRole.MAINTENANCETECHNICIAN && e.Status == Status.Active).ToListAsync();
                ViewBag.Technicians = new SelectList(technicians, "Id", "FullName", schedule.AssignedTechnicianId);
                return View(schedule);
            }

            var existing = await _context.MaintenanceSchedules.FindAsync(id);
            if (existing == null) return NotFound();

            existing.FridgeId = schedule.FridgeId;
            existing.ScheduledDate = schedule.ScheduledDate;
            existing.Notes = schedule.Notes;
            existing.AssignedTechnicianId = schedule.AssignedTechnicianId;
            // Status unchanged unless explicitly changed elsewhere

            _context.Update(existing);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Schedule updated.";
            return RedirectToAction(nameof(Index));
        }

        // Cancel schedule (soft delete)
        public async Task<IActionResult> CancelSchedule(int? id)
        {
            if (id == null) return NotFound();
            var schedule = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (schedule == null) return NotFound();
            return View(schedule);
        }

        [HttpPost, ActionName("CancelSchedule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelScheduleConfirmed(int id)
        {
            var schedule = await _context.MaintenanceSchedules.FindAsync(id);
            if (schedule != null)
            {
                schedule.Status = MaintenanceStatus.Cancelled;
                schedule.IsDeleted = true;
                _context.Update(schedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Schedule cancelled.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================== SERVICE FRIDGE (Perform Maintenance) ====================
        public async Task<IActionResult> ServiceFridge(int? scheduleId)
        {
            if (scheduleId == null) return NotFound();
            var schedule = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted);
            if (schedule == null) return NotFound();

            var tech = await GetCurrentTechnicianAsync();
            ViewBag.TechnicianId = tech?.Id ?? 0;

            // Define checklist items (in real app, fetch from admin-configured template)
            ViewBag.ChecklistItems = new List<string> { "Compressor function", "Door seal intact", "Temperature calibration", "Clean condenser coils" };

            return View(new MaintenanceLog { MaintenanceScheduleId = scheduleId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ServiceFridge(MaintenanceLog log, List<string> checklistResults)
        {
            var schedule = await _context.MaintenanceSchedules.FindAsync(log.MaintenanceScheduleId);
            if (schedule == null) return NotFound();

            if (ModelState.IsValid)
            {
                // Combine checklist results into a string
                log.ChecklistResults = string.Join(";", checklistResults);
                log.CompletedDate = DateTime.Now;
                log.IsDeleted = false;

                // Mark schedule as completed
                schedule.Status = MaintenanceStatus.Completed;
                _context.Update(schedule);

                _context.Add(log);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Service completed and logged.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ChecklistItems = new List<string> { "Compressor function", "Door seal intact", "Temperature calibration", "Clean condenser coils" };
            return View(log);
        }

        // ==================== CREATE FAULT FROM MAINTENANCE ====================
        public async Task<IActionResult> CreateFaultFromMaintenance(int fridgeId)
        {
            var fridge = await _context.Fridges.FindAsync(fridgeId);
            if (fridge == null) return NotFound();

            // Get customer for this fridge
            var allocation = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.FridgeId == fridgeId && a.Status == AllocationStatus.Active);

            var fault = new FaultReport
            {
                FridgeId = fridgeId,
                ReportedByCustomerId = allocation?.CustomerId ?? 0,
                ReportedDate = DateTime.Now,
                Status = FaultStatus.Reported
            };

            ViewBag.CustomerName = allocation?.Customer?.Name ?? "Unknown";
            return View(fault);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFaultFromMaintenance(FaultReport fault)
        {
            if (ModelState.IsValid)
            {
                fault.IsDeleted = false;
                _context.Add(fault);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fault reported from maintenance.";
                return RedirectToAction(nameof(CustomerFridges));
            }
            return View(fault);
        }

        // ==================== VIEW MAINTENANCE HISTORY ====================
        public async Task<IActionResult> History(int? fridgeId)
        {
            IQueryable<MaintenanceLog> query = _context.MaintenanceLogs
                .Include(l => l.MaintenanceSchedule).ThenInclude(s => s!.Fridge)
                .Include(l => l.Technician)
                .Where(l => !l.IsDeleted);

            if (fridgeId.HasValue)
                query = query.Where(l => l.MaintenanceSchedule!.FridgeId == fridgeId);

            var logs = await query.OrderByDescending(l => l.CompletedDate).ToListAsync();

            // For filter dropdown
            var fridges = await _context.Fridges.Where(f => f.Status == Status.Active).ToListAsync();
            ViewBag.Fridges = new SelectList(fridges, "Id", "SerialNumber", fridgeId);

            return View(logs);
        }
    }
}