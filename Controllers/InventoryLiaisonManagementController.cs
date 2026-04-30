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
    [Authorize(Roles = "INVENTORYLIAISON")]
    public class InventoryLiaisonManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryLiaisonManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            // Stock statistics
            int totalFridges = await _context.Fridges.CountAsync(f => f.Status == Status.Active);
            int allocatedFridges = await _context.FridgeAllocations
                .CountAsync(a => a.Status == AllocationStatus.Active);
            int availableFridges = totalFridges - allocatedFridges;
            int scrappedFridges = await _context.Fridges.CountAsync(f => f.Status == Status.Inactive);

            // Low stock threshold (configurable)
            const int lowStockThreshold = 5;
            bool isLowStock = availableFridges < lowStockThreshold;

            // Pending purchase requests
            int pendingRequests = await _context.PurchaseRequests
                .CountAsync(p => p.Status == PurchaseRequestStatus.Pending);

            ViewBag.TotalFridges = totalFridges;
            ViewBag.AllocatedFridges = allocatedFridges;
            ViewBag.AvailableFridges = availableFridges;
            ViewBag.ScrappedFridges = scrappedFridges;
            ViewBag.LowStockThreshold = lowStockThreshold;
            ViewBag.IsLowStock = isLowStock;
            ViewBag.PendingRequests = pendingRequests;

            return View();
        }

        // ==================== INVENTORY MANAGEMENT ====================
        // List all fridges (active and scrapped)
        public async Task<IActionResult> Inventory(string? statusFilter)
        {
            IQueryable<Fridge> query = _context.Fridges
                .Include(f => f.FridgeAllocations!.Where(a => a.Status == AllocationStatus.Active))
                    .ThenInclude(a => a.Customer);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "active")
                    query = query.Where(f => f.Status == Status.Active);
                else if (statusFilter == "scrapped")
                    query = query.Where(f => f.Status == Status.Inactive);
            }

            var fridges = await query.OrderBy(f => f.SerialNumber).ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            return View(fridges);
        }

        // Receive new fridge (GET)
        public IActionResult ReceiveFridge()
        {
            return View();
        }

        // Receive new fridge (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveFridge(Fridge fridge)
        {
            if (ModelState.IsValid)
            {
                fridge.Status = Status.Active;
                _context.Add(fridge);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Fridge '{fridge.SerialNumber}' added to inventory.";
                return RedirectToAction(nameof(Inventory));
            }
            return View(fridge);
        }

        // Scrap fridge confirmation (GET)
        public async Task<IActionResult> ScrapFridge(int? id)
        {
            if (id == null) return NotFound();

            var fridge = await _context.Fridges
                .Include(f => f.FridgeAllocations!.Where(a => a.Status == AllocationStatus.Active))
                .FirstOrDefaultAsync(f => f.Id == id && f.Status == Status.Active);

            if (fridge == null) return NotFound();

            // Check if fridge is currently allocated
            if (fridge.FridgeAllocations != null && fridge.FridgeAllocations.Any())
            {
                TempData["ErrorMessage"] = "Cannot scrap a fridge that is currently allocated. Return it first.";
                return RedirectToAction(nameof(Inventory));
            }

            return View(fridge);
        }

        // Scrap fridge (POST)
        [HttpPost, ActionName("ScrapFridge")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScrapFridgeConfirmed(int id)
        {
            var fridge = await _context.Fridges.FindAsync(id);
            if (fridge == null) return NotFound();

            // Double-check allocation status
            var isAllocated = await _context.FridgeAllocations
                .AnyAsync(a => a.FridgeId == id && a.Status == AllocationStatus.Active);
            if (isAllocated)
            {
                TempData["ErrorMessage"] = "Cannot scrap a fridge that is currently allocated.";
                return RedirectToAction(nameof(Inventory));
            }

            fridge.Status = Status.Inactive;
            _context.Update(fridge);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fridge '{fridge.SerialNumber}' has been scrapped.";
            return RedirectToAction(nameof(Inventory));
        }

        // ==================== PROCESS ALLOCATIONS ====================
        // View all allocations (active, returned, cancelled)
        public async Task<IActionResult> ProcessAllocations(string? statusFilter)
        {
            IQueryable<FridgeAllocation> query = _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Include(a => a.Customer);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (Enum.TryParse<AllocationStatus>(statusFilter, true, out var status))
                    query = query.Where(a => a.Status == status);
            }

            var allocations = await query
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            return View(allocations);
        }

        // Process a specific allocation (mark as returned, etc.)
        public async Task<IActionResult> ProcessAllocation(int? id)
        {
            if (id == null) return NotFound();

            var allocation = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allocation == null) return NotFound();

            return View(allocation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessAllocation(int id, string action, string? notes)
        {
            var allocation = await _context.FridgeAllocations.FindAsync(id);
            if (allocation == null) return NotFound();

            switch (action.ToLower())
            {
                case "return":
                    allocation.Status = AllocationStatus.Returned;
                    allocation.ReturnDate = DateTime.Now;
                    allocation.Notes = notes;
                    TempData["SuccessMessage"] = "Fridge return processed.";
                    break;
                case "cancel":
                    allocation.Status = AllocationStatus.Cancelled;
                    allocation.Notes = notes;
                    TempData["SuccessMessage"] = "Allocation cancelled.";
                    break;
                default:
                    TempData["ErrorMessage"] = "Invalid action.";
                    return RedirectToAction(nameof(ProcessAllocations));
            }

            _context.Update(allocation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ProcessAllocations));
        }

        // ==================== PURCHASE REQUESTS ====================
        // List all purchase requests
        public async Task<IActionResult> PurchaseRequests()
        {
            var requests = await _context.PurchaseRequests
                .Include(p => p.RequestedBy)
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();
            return View(requests);
        }

        // Create purchase request (GET)
        public IActionResult CreatePurchaseRequest()
        {
            // Get current logged-in user's email to find employee record
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var employee = _context.Users.FirstOrDefault(e => e.Email == userEmail);

            var model = new PurchaseRequest
            {
                RequestedById = employee?.Id ?? 0,
                RequestDate = DateTime.Now
            };

            ViewBag.EmployeeName = employee?.FullName ?? "Unknown";
            return View(model);
        }

        // Create purchase request (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseRequest(PurchaseRequest request)
        {
            if (ModelState.IsValid)
            {
                request.RequestDate = DateTime.Now;
                request.Status = PurchaseRequestStatus.Pending;

                _context.Add(request);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Purchase request created and sent to Purchasing Manager.";
                return RedirectToAction(nameof(PurchaseRequests));
            }

            // Repopulate employee info if validation fails
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var employee = _context.Users.FirstOrDefault(e => e.Email == userEmail);
            ViewBag.EmployeeName = employee?.FullName ?? "Unknown";

            return View(request);
        }

        // View details of a purchase request
        public async Task<IActionResult> PurchaseRequestDetails(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.PurchaseRequests
                .Include(p => p.RequestedBy)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (request == null) return NotFound();

            return View(request);
        }

        // Cancel a pending purchase request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPurchaseRequest(int id)
        {
            var request = await _context.PurchaseRequests.FindAsync(id);
            if (request != null && request.Status == PurchaseRequestStatus.Pending)
            {
                request.Status = PurchaseRequestStatus.Rejected; // Use Rejected as cancelled
                _context.Update(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Purchase request cancelled.";
            }
            return RedirectToAction(nameof(PurchaseRequests));
        }
    }
}