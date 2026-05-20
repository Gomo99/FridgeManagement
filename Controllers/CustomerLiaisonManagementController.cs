using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
        public IActionResult DashBoard()
        {
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
                .Include(c => c.FridgeAllocations!)
                    .ThenInclude(a => a.Fridge)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == Status.Active);

            if (customer == null) return NotFound();

            return View(customer);
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
    }
}