using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("AccessDenied", "Account");

            // Get allocated fridges
            var allocations = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == customer.Id && a.Status == AllocationStatus.Active)
                .ToListAsync();

            // Recent faults
            var recentFaults = await _context.FaultReports
                .Where(f => f.ReportedByCustomerId == customer.Id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .Take(5)
                .ToListAsync();

            // Pending fridge requests
            var pendingRequests = await _context.FridgeRequests
                .Where(r => r.CustomerId == customer.Id && !r.IsDeleted &&
                            (r.Status == FridgeRequestStatus.Pending || r.Status == FridgeRequestStatus.Approved))
                .ToListAsync();

            ViewBag.Customer = customer;
            ViewBag.AllocatedFridges = allocations;
            ViewBag.RecentFaults = recentFaults;
            ViewBag.PendingRequests = pendingRequests;

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
        public async Task<IActionResult> ReportFault(FaultReport faultReport)
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
                .Include(f => f.AssignedTechnician)
                .FirstOrDefaultAsync(f => f.Id == id && f.ReportedByCustomerId == customer.Id && !f.IsDeleted);
            if (fault == null) return NotFound();

            return View(fault);
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
    }
}