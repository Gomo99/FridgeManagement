using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
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

        public CustomerLiaisonManagementController(ApplicationDbContext context)
        {
            _context = context;
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
                // Check fridge is still available
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