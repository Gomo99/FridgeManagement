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
        public async Task<IActionResult> Dashboard()
        {
            var tech = await GetCurrentTechnicianAsync();
            if (tech == null) return RedirectToAction("AccessDenied", "Account");

            // Statistics
            int totalOpen = await _context.FaultReports.CountAsync(f => !f.IsDeleted && f.Status != FaultStatus.Closed && f.Status != FaultStatus.Cancelled);
            int assignedToMe = await _context.FaultReports.CountAsync(f => f.AssignedTechnicianId == tech.Id && !f.IsDeleted && f.Status != FaultStatus.Closed);
            int highPriority = await _context.FaultReports.CountAsync(f => !f.IsDeleted && f.Priority >= FaultPriority.High && f.Status != FaultStatus.Closed);

            ViewBag.TotalOpen = totalOpen;
            ViewBag.AssignedToMe = assignedToMe;
            ViewBag.HighPriority = highPriority;

            // Recent faults
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

        // ==================== PROCESS FAULT ====================
        public async Task<IActionResult> ProcessFault(int? id)
        {
            if (id == null) return NotFound();

            var fault = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.ReportedByCustomer)
                .Include(f => f.AssignedTechnician)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
            if (fault == null) return NotFound();

            // For assignment dropdown
            var technicians = await _context.Users
                .Where(e => e.Role == UserRole.FAULTTECHNICIAN && e.Status == Status.Active)
                .ToListAsync();
            ViewBag.Technicians = new SelectList(technicians, "Id", "FullName", fault.AssignedTechnicianId);

            return View(fault);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessFault(int id, FaultReport model, string action)
        {
            var fault = await _context.FaultReports.FindAsync(id);
            if (fault == null) return NotFound();

            var tech = await GetCurrentTechnicianAsync();

            switch (action)
            {
                case "assign":
                    fault.AssignedTechnicianId = model.AssignedTechnicianId;
                    fault.Status = FaultStatus.Assigned;
                    TempData["SuccessMessage"] = "Technician assigned.";
                    break;
                case "schedule":
                    fault.ScheduledRepairDate = model.ScheduledRepairDate;
                    fault.Status = FaultStatus.Scheduled;
                    TempData["SuccessMessage"] = "Repair scheduled.";
                    break;
                case "diagnose":
                    fault.DiagnosisNotes = model.DiagnosisNotes;
                    fault.Status = FaultStatus.InProgress;
                    TempData["SuccessMessage"] = "Diagnosis recorded.";
                    break;
                case "repair":
                    fault.ResolutionNotes = model.ResolutionNotes;
                    fault.Status = FaultStatus.Repaired;
                    fault.ResolvedDate = DateTime.Now;
                    TempData["SuccessMessage"] = "Fault marked as repaired.";
                    break;
                case "close":
                    fault.Status = FaultStatus.Closed;
                    fault.IsDeleted = true; // Soft delete when closed
                    TempData["SuccessMessage"] = "Fault closed.";
                    break;
                default:
                    TempData["ErrorMessage"] = "Invalid action.";
                    break;
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
    }
}