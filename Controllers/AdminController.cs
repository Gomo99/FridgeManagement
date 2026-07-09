using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "ADMINISTRATOR")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;   // new

        public AdminController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }


        // ==================== DASHBOARD ====================
        // ==================== DASHBOARD ====================
        public IActionResult Dashboard()
        {
            // ---------- Customers ----------
            ViewBag.TotalCustomers = _context.Customers.Count();
            ViewBag.ActiveCustomers = _context.Customers.Count(c => c.Status == Status.Active);

            // ---------- Employees (all roles except Customer & Supplier) ----------
            ViewBag.TotalEmployees = _context.Users
                .Count(u => u.Status == Status.Active
                            && u.Role != UserRole.CUSTOMER
                            && u.Role != UserRole.SUPPLIER);

            // ---------- Fridges ----------
            ViewBag.TotalFridges = _context.Fridges.Count();
            ViewBag.AllocatedFridges = _context.Fridges.Count(f => f.Status == Status.Active);
            ViewBag.AvailableFridges = _context.Fridges.Count(f => f.Status == Status.Active);

            // ---------- Faults ----------
            ViewBag.FaultsReported = _context.FaultReports.Count();
            ViewBag.FaultsWaiting = _context.FaultReports.Count(f => f.Status == FaultStatus.Reported || f.Status == FaultStatus.InProgress);
            ViewBag.FaultsCompleted = _context.FaultReports.Count(f => f.Status == FaultStatus.Repaired);

            // ---------- Maintenance ----------
            ViewBag.UpcomingMaintenance = _context.MaintenanceSchedules
                .Count(m => m.ScheduledDate >= DateTime.Today && m.Status != MaintenanceStatus.Completed);

            // ---------- Suppliers ----------
            ViewBag.TotalSuppliers = _context.Suppliers.Count();

            // ---------- Purchase Requests ----------
            ViewBag.PendingOrders = _context.PurchaseRequests
                .Count(p => p.Status == PurchaseRequestStatus.Pending);

            // ==================== CHART DATA ====================
            // ---- Fridges by Condition ----
            var fridgeStatusCounts = _context.Fridges
                .GroupBy(f => f.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToList();
            var fridgeLabels = fridgeStatusCounts.Select(x => x.Status).ToList();
            var fridgeValues = fridgeStatusCounts.Select(x => x.Count).ToList();
            ViewBag.FridgeConditionLabels = System.Text.Json.JsonSerializer.Serialize(fridgeLabels);
            ViewBag.FridgeConditionData = System.Text.Json.JsonSerializer.Serialize(fridgeValues);

            // ---- Faults per Month (last 6 months) ----
            var sixMonthsAgo = DateTime.Today.AddMonths(-5);
            var monthlyFaults = _context.FaultReports
                .Where(f => f.ReportedDate >= sixMonthsAgo)
                .GroupBy(f => new { f.ReportedDate.Year, f.ReportedDate.Month })
                .Select(g => new {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToList();
            ViewBag.FaultsMonthLabels = System.Text.Json.JsonSerializer.Serialize(monthlyFaults.Select(x => x.Month));
            ViewBag.FaultsMonthData = System.Text.Json.JsonSerializer.Serialize(monthlyFaults.Select(x => x.Count));

            // ---- Purchases per Month (last 6 months, from PurchaseRequests) ----
            var monthlyPurchases = _context.PurchaseRequests
                .Where(p => p.RequestDate >= sixMonthsAgo)
                .GroupBy(p => new { p.RequestDate.Year, p.RequestDate.Month })
                .Select(g => new {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToList();
            ViewBag.PurchasesMonthLabels = System.Text.Json.JsonSerializer.Serialize(monthlyPurchases.Select(x => x.Month));
            ViewBag.PurchasesMonthData = System.Text.Json.JsonSerializer.Serialize(monthlyPurchases.Select(x => x.Count));

            // ---- Customers per City (assumes Customer has City property) ----
            // If City doesn't exist, use Address or add a City field.
            var cityCounts = _context.Customers
                .Where(c => c.Status == Status.Active)   // or all
                .GroupBy(c => c.Address ?? "Unknown")       // c.City – add property if needed
                .Select(g => new { City = g.Key, Count = g.Count() })
                .ToList();
            ViewBag.CityLabels = System.Text.Json.JsonSerializer.Serialize(cityCounts.Select(x => x.City));
            ViewBag.CityData = System.Text.Json.JsonSerializer.Serialize(cityCounts.Select(x => x.Count));

            // ---- Maintenance Visits per Month (last 6 months, completed logs) ----
            var completedMaintenance = _context.MaintenanceLogs
                .Where(l => l.CompletedDate.HasValue && l.CompletedDate.Value >= sixMonthsAgo)
                .GroupBy(l => new { l.CompletedDate!.Value.Year, l.CompletedDate.Value.Month })
                .Select(g => new {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToList();
            ViewBag.MaintMonthLabels = System.Text.Json.JsonSerializer.Serialize(completedMaintenance.Select(x => x.Month));
            ViewBag.MaintMonthData = System.Text.Json.JsonSerializer.Serialize(completedMaintenance.Select(x => x.Count));

            return View();
        }

        // ==================== CUSTOMERS ====================
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

        public IActionResult CreateCustomer()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(Customer customer)
        {
            if (ModelState.IsValid)
            {
                customer.Status = Status.Active;
                _context.Add(customer);
                await _context.SaveChangesAsync();   // 1) Save the Customer entity

                // 2) Create a matching User account if one doesn't already exist
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == customer.Email &&
                                              u.Role == UserRole.CUSTOMER &&
                                              u.Status == Status.Active);

                if (existingUser == null)
                {
                    // Generate a temporary password (8 random characters)
                    var tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
                    var newUser = new User
                    {
                        Name = customer.Name,                  // use business name as first name
                        Surname = "Customer",
                        Username = customer.Email,
                        Email = customer.Email,
                        PasswordHash = tempPassword,           // plain text for demo (hash in production)
                        Role = UserRole.CUSTOMER,
                        Gender = GenderType.Male,
                        Status = Status.Active,
                        Title = "Mr"
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    // 3) Send welcome notification
                    var adminName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Administrator";
                    await _notificationService.CreateNotificationAsync(
                        userId: newUser.Id,
                        title: "Welcome to Fridge Management!",
                        message: $"Your account was created by {adminName}. " +
                                 $"Your temporary password is: {tempPassword}. " +
                                 "Please change it after logging in.",
                        type: NotificationType.Success,
                        actionUrl: "/Customer/DashBoard"
                    );
                }

                return RedirectToAction(nameof(Customers));
            }
            return View(customer);
        }


        public async Task<IActionResult> EditCustomer(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null || customer.Status == Status.Inactive) return NotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(int id, Customer customer)
        {
            if (id != customer.Id) return NotFound();
            if (!ModelState.IsValid) return View(customer);

            try
            {
                var existing = await _context.Customers.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Name = customer.Name;
                existing.ContactPerson = customer.ContactPerson;
                existing.PhoneNumber = customer.PhoneNumber;
                existing.Email = customer.Email;
                existing.Address = customer.Address;
                existing.Status = Status.Active;

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Customers));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.Id)) return NotFound();
                throw;
            }
        }

        public async Task<IActionResult> DeleteCustomer(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Active);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("DeleteCustomer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomerConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                customer.Status = Status.Inactive; // Soft delete
                _context.Update(customer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Customers));
        }



        public async Task<IActionResult> RestoreDeleteCustomer(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Inactive);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("RestoreCustomer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreCustomerConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                customer.Status = Status.Active; // Soft delete
                _context.Update(customer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Customers));
        }



        private bool CustomerExists(int id) => _context.Customers.Any(e => e.Id == id);

        // ==================== EMPLOYEES ====================
        public async Task<IActionResult> Employees()
        {
            var employees = await _context.Users
                .Where(e => e.Status == Status.Active)
                .ToListAsync();
            return View(employees);
        }


        public async Task<IActionResult> InactiveEmployees()
        {
            var employees = await _context.Users
                .Where(e => e.Status == Status.Inactive)
                .ToListAsync();
            return View(employees);
        }

        public IActionResult CreateEmployee()
        {
            ViewBag.Roles = new SelectList(Enum.GetValues(typeof(UserRole)));
            ViewBag.Genders = new SelectList(Enum.GetValues(typeof(GenderType)));
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(User employee)
        {
            if (ModelState.IsValid)
            {
                employee.Status = Status.Active;
                _context.Add(employee);
                await _context.SaveChangesAsync();

                // ────── NEW: Send welcome notification if the new user is a CUSTOMER ──────
                if (employee.Role == UserRole.CUSTOMER)
                {
                    // Get the name of the admin who is currently logged in
                    var adminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                                    ?? "Administrator";

                    await _notificationService.CreateNotificationAsync(
                        userId: employee.Id,
                        title: "Welcome to Fridge Management!",
                        message: $"Your account was created by {adminName}. You can now log in and manage your fridges.",
                        type: NotificationType.Info,
                        actionUrl: "/Customer/DashBoard"
                    );
                }
                // ─────────────────────────────────────────────────────────────────────────

                return RedirectToAction(nameof(Employees));
            }

            ViewBag.Roles = new SelectList(Enum.GetValues(typeof(UserRole)), employee.Role);
            ViewBag.Genders = new SelectList(Enum.GetValues(typeof(GenderType)), employee.Gender);
            return View(employee);
        }


        public async Task<IActionResult> EditEmployee(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Users.FindAsync(id);
            if (employee == null || employee.Status == Status.Inactive) return NotFound();

            ViewBag.Roles = new SelectList(Enum.GetValues(typeof(UserRole)), employee.Role);
            ViewBag.Genders = new SelectList(Enum.GetValues(typeof(GenderType)), employee.Gender);
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(int id, User employee)
        {
            if (id != employee.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(Enum.GetValues(typeof(UserRole)), employee.Role);
                ViewBag.Genders = new SelectList(Enum.GetValues(typeof(GenderType)), employee.Gender);
                return View(employee);
            }

            try
            {
                var existing = await _context.Users.FindAsync(id);
                if (existing == null) return NotFound();

                existing.FullName = employee.FullName;
                existing.Email = employee.Email;
                existing.PhoneNumber = employee.PhoneNumber;
                existing.Role = employee.Role;
                existing.Gender = employee.Gender;
                existing.Status = Status.Active;

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Employees));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.Id)) return NotFound();
                throw;
            }
        }

        public async Task<IActionResult> DeleteEmployee(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Active);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ActionName("DeleteEmployee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployeeConfirmed(int id)
        {
            var employee = await _context.Users.FindAsync(id);
            if (employee != null)
            {
                employee.Status = Status.Inactive;
                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Employees));
        }


        public async Task<IActionResult> RestoreEmployee(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Inactive);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ActionName("RestoreEmployee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmedEmployeeConfirmed(int id)
        {
            var employee = await _context.Users.FindAsync(id);
            if (employee != null)
            {
                employee.Status = Status.Active;
                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Employees));
        }



        private bool EmployeeExists(int id) => _context.Users.Any(e => e.Id == id);

        // ==================== LOCATIONS ====================
        public async Task<IActionResult> Locations()
        {
            var locations = await _context.Locations
                .Where(l => l.Status == Status.Active)
                .ToListAsync();
            return View(locations);
        }

        public async Task<IActionResult> InactiveLocations()
        {
            var locations = await _context.Locations
                .Where(l => l.Status == Status.Inactive)
                .ToListAsync();
            return View(locations);
        }


        public IActionResult CreateLocation()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(Location location)
        {
            if (ModelState.IsValid)
            {
                location.Status = Status.Active;
                _context.Add(location);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Locations));
            }
            return View(location);
        }

        public async Task<IActionResult> EditLocation(int? id)
        {
            if (id == null) return NotFound();
            var location = await _context.Locations.FindAsync(id);
            if (location == null || location.Status == Status.Inactive) return NotFound();
            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(int id, Location location)
        {
            if (id != location.Id) return NotFound();
            if (!ModelState.IsValid) return View(location);

            try
            {
                var existing = await _context.Locations.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Name = location.Name;
                existing.Address = location.Address;
                existing.City = location.City;
                existing.Status = Status.Active;
                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Locations));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationExists(location.Id)) return NotFound();
                throw;
            }
        }

        public async Task<IActionResult> DeleteLocation(int? id)
        {
            if (id == null) return NotFound();
            var location = await _context.Locations
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Active);
            if (location == null) return NotFound();
            return View(location);
        }

        [HttpPost, ActionName("DeleteLocation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLocationConfirmed(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location != null)
            {
                location.Status = Status.Inactive;
                _context.Update(location);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Locations));
        }


        public async Task<IActionResult> RestoreLocation(int? id)
        {
            if (id == null) return NotFound();
            var location = await _context.Locations
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Inactive);
            if (location == null) return NotFound();
            return View(location);
        }

        [HttpPost, ActionName("RestoreLocation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreLocationConfirmed(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location != null)
            {
                location.Status = Status.Active;
                _context.Update(location);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Locations));
        }





        private bool LocationExists(int id) => _context.Locations.Any(e => e.Id == id);

        // ==================== FRIDGES ====================
        public async Task<IActionResult> Fridges()
        {
            var fridges = await _context.Fridges
                .Where(f => f.Status == Status.Active)
                .ToListAsync();
            return View(fridges);
        }


        public async Task<IActionResult> InactiveFridges()
        {
            var fridges = await _context.Fridges
                .Where(f => f.Status == Status.Inactive)
                .ToListAsync();
            return View(fridges);
        }


        public IActionResult CreateFridge()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFridge(Fridge fridge)
        {
            if (ModelState.IsValid)
            {
                fridge.Status = Status.Active;
                _context.Add(fridge);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Fridges));
            }
            return View(fridge);
        }

        public async Task<IActionResult> EditFridge(int? id)
        {
            if (id == null) return NotFound();
            var fridge = await _context.Fridges.FindAsync(id);
            if (fridge == null || fridge.Status == Status.Inactive) return NotFound();
            return View(fridge);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFridge(int id, Fridge fridge)
        {
            if (id != fridge.Id) return NotFound();
            if (!ModelState.IsValid) return View(fridge);

            try
            {
                var existing = await _context.Fridges.FindAsync(id);
                if (existing == null) return NotFound();

                existing.SerialNumber = fridge.SerialNumber;
                existing.Model = fridge.Model;
                existing.Brand = fridge.Brand;
                existing.PurchaseDate = fridge.PurchaseDate;
                existing.Status = Status.Active;
                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Fridges));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FridgeExists(fridge.Id)) return NotFound();
                throw;
            }
        }

        public async Task<IActionResult> DeleteFridge(int? id)
        {
            if (id == null) return NotFound();
            var fridge = await _context.Fridges
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Active);
            if (fridge == null) return NotFound();
            return View(fridge);
        }

        [HttpPost, ActionName("DeleteFridge")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFridgeConfirmed(int id)
        {
            var fridge = await _context.Fridges.FindAsync(id);
            if (fridge != null)
            {
                fridge.Status = Status.Inactive;
                _context.Update(fridge);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Fridges));
        }


        public async Task<IActionResult> RestoreFridge(int? id)
        {
            if (id == null) return NotFound();
            var fridge = await _context.Fridges
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Inactive);
            if (fridge == null) return NotFound();
            return View(fridge);
        }

        [HttpPost, ActionName("RestoreFridge")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreFridgeConfirmed(int id)
        {
            var fridge = await _context.Fridges.FindAsync(id);
            if (fridge != null)
            {
                fridge.Status = Status.Active;
                _context.Update(fridge);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Fridges));
        }






        private bool FridgeExists(int id) => _context.Fridges.Any(e => e.Id == id);

        // ==================== SUPPLIERS ====================
        public async Task<IActionResult> Suppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.Status == Status.Active)
                .ToListAsync();
            return View(suppliers);
        }


        public async Task<IActionResult> InactiveSuppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.Status == Status.Inactive)
                .ToListAsync();
            return View(suppliers);
        }


        public IActionResult CreateSupplier()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                supplier.Status = Status.Active;
                _context.Add(supplier);
                await _context.SaveChangesAsync();   // 1) Save the Supplier entity

                // 2) Create a matching User account if one doesn't already exist
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == supplier.Email &&
                                              u.Role == UserRole.SUPPLIER &&
                                              u.Status == Status.Active);

                if (existingUser == null)
                {
                    var tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
                    var newUser = new User
                    {
                        Name = supplier.Name,
                        Surname = "Supplier",
                        Username = supplier.Email,
                        Email = supplier.Email,
                        PasswordHash = tempPassword,
                        Role = UserRole.SUPPLIER,
                        Gender = GenderType.Male,
                        Status = Status.Active,
                        Title = "Supplier"
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    // 3) Send welcome notification
                    var adminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Administrator";
                    await _notificationService.CreateNotificationAsync(
                        userId: newUser.Id,
                        title: "Welcome to Fridge Management – Supplier Portal",
                        message: $"Your supplier account was created by {adminName}. " +
                                 $"Your temporary password is: {tempPassword}. " +
                                 "Please change it after logging in.",
                        type: NotificationType.Success,
                        actionUrl: "/Supplier/DashBoard"
                    );
                }

                TempData["SuccessMessage"] = "Supplier added successfully.";
                return RedirectToAction(nameof(Suppliers));
            }
            return View(supplier);
        }
        public async Task<IActionResult> EditSupplier(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null || supplier.Status == Status.Inactive) return NotFound();
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id) return NotFound();
            if (!ModelState.IsValid) return View(supplier);

            try
            {
                var existing = await _context.Suppliers.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Name = supplier.Name;
                existing.ContactPerson = supplier.ContactPerson;
                existing.PhoneNumber = supplier.PhoneNumber;
                existing.Email = supplier.Email;
                existing.Address = supplier.Address;
                existing.Status = Status.Active;
                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Suppliers));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(supplier.Id)) return NotFound();
                throw;
            }
        }

        public async Task<IActionResult> DeleteSupplier(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Active);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ActionName("DeleteSupplier")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupplierConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.Status = Status.Inactive;
                _context.Update(supplier);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Suppliers));
        }


        public async Task<IActionResult> RestoreSupplier(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.Id == id && m.Status == Status.Inactive);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ActionName("RestoreSupplier")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreSupplierConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.Status = Status.Active;
                _context.Update(supplier);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Suppliers));
        }

        // Inside AdminController class

        // ==================== BUSINESS INFO ====================
        [HttpGet]
        public async Task<IActionResult> ManageBusinessInfo()
        {
            var businessInfo = await _context.BusinessInfos.FirstOrDefaultAsync(b => b.Id == 1);
            if (businessInfo == null)
            {
                // If for some reason the record is missing, create a blank one
                businessInfo = new BusinessInfo { Id = 1 };
                _context.BusinessInfos.Add(businessInfo);
                await _context.SaveChangesAsync();
            }
            return View(businessInfo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageBusinessInfo(BusinessInfo model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existing = await _context.BusinessInfos.FirstOrDefaultAsync(b => b.Id == 1);
            if (existing == null)
            {
                model.Id = 1;
                _context.BusinessInfos.Add(model);
            }
            else
            {
                existing.CompanyName = model.CompanyName;
                existing.Address = model.Address;
                existing.PhoneNumber = model.PhoneNumber;
                existing.Email = model.Email;
                existing.Website = model.Website;
                existing.TaxId = model.TaxId;
                _context.BusinessInfos.Update(existing);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Business information updated successfully.";
            return RedirectToAction(nameof(ManageBusinessInfo));
        }


        // ==================== QR CODE ====================
        [AllowAnonymous] // so anyone with the link can scan it
        public IActionResult GenerateFridgeQRCode(int id)
        {
            var fridge = _context.Fridges.FirstOrDefault(f => f.Id == id);
            if (fridge == null) return NotFound();

            // Build the URL that the QR code will point to
            var url = Url.Action("FridgeProfile", "Admin", new { id = fridge.Id }, Request.Scheme);

            // Generate QR code as PNG
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(10);

            return File(qrCodeBytes, "image/png");
        }

        [AllowAnonymous]
        public async Task<IActionResult> FridgeProfile(int id)
        {
            var fridge = await _context.Fridges
                .Include(f => f.Supplier)          // if supplier linked
                .FirstOrDefaultAsync(f => f.Id == id && f.Status == Status.Active);
            if (fridge == null) return NotFound();

            // Allocation history (all, not just active)
            var allocationHistory = await _context.FridgeAllocations
                .Include(a => a.Customer)
                .Where(a => a.FridgeId == id)
                .OrderByDescending(a => a.AllocationDate)   // assume you have this field
                .ToListAsync();

            // Fault history
            var faultHistory = await _context.FaultReports
                .Include(f => f.AssignedTechnician)
                .Where(f => f.FridgeId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // Maintenance history
            var serviceHistory = await _context.MaintenanceLogs
                .Include(l => l.MaintenanceSchedule)
                .ThenInclude(s => s!.AssignedTechnician)
                .Where(l => l.MaintenanceSchedule!.FridgeId == id && !l.IsDeleted)
                .OrderByDescending(l => l.CompletedDate)
                .ToListAsync();

            // Purchase history (requests linked to this fridge)
            var purchaseHistory = await _context.PurchaseRequests
                .Where(p => p.FridgeId == id)
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();

            // Current customer (active allocation)
            var currentAllocation = allocationHistory
                .FirstOrDefault(a => a.Status == AllocationStatus.Active);

            ViewBag.Fridge = fridge;
            ViewBag.AllocationHistory = allocationHistory;
            ViewBag.FaultHistory = faultHistory;
            ViewBag.ServiceHistory = serviceHistory;
            ViewBag.PurchaseHistory = purchaseHistory;
            ViewBag.CurrentCustomer = currentAllocation?.Customer;

            return View();
        }


        // ==================== CUSTOMER PROFILE ====================
        [AllowAnonymous]
        public async Task<IActionResult> CustomerProfile(int id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == Status.Active);
            if (customer == null) return NotFound();

            // Allocated fridges (active)
            var allocatedFridges = await _context.FridgeAllocations
                .Include(a => a.Fridge)
                .Where(a => a.CustomerId == id && a.Status == AllocationStatus.Active)
                .ToListAsync();

            // Fault history
            var faultHistory = await _context.FaultReports
                .Include(f => f.Fridge)
                .Include(f => f.AssignedTechnician)
                .Where(f => f.ReportedByCustomerId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // Maintenance visits (scheduled & completed) for this customer's fridges
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

            ViewBag.Customer = customer;
            ViewBag.AllocatedFridges = allocatedFridges;
            ViewBag.FaultHistory = faultHistory;
            ViewBag.MaintenanceSchedules = maintenanceSchedules;
            ViewBag.FridgeRequests = fridgeRequests;

            return View();
        }

        // ==================== SUPPLIER PROFILE ====================
        [AllowAnonymous] // or keep [Authorize(Roles = "ADMINISTRATOR")]
        public async Task<IActionResult> SupplierProfile(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == Status.Active);
            if (supplier == null) return NotFound();

            // Quotation history (quotations submitted by this supplier)
            var quotations = await _context.Quotations
                .Include(q => q.RFQ)
                .Where(q => q.SupplierId == id && !q.IsDeleted)
                .OrderByDescending(q => q.ReceivedDate)
                .ToListAsync();

            // Purchase orders placed to this supplier (via Quotation → PurchaseOrder)
            var quotationIds = quotations.Select(q => q.Id).ToList();
            var purchaseOrders = await _context.PurchaseOrders
                .Include(po => po.Quotation)
                .Where(po => quotationIds.Contains(po.QuotationId) && !po.IsDeleted)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            // Delivery notes for these purchase orders
            var purchaseOrderIds = purchaseOrders.Select(po => po.Id).ToList();
            var deliveryNotes = await _context.DeliveryNotes
                .Include(dn => dn.PurchaseOrder)
                .Where(dn => purchaseOrderIds.Contains(dn.PurchaseOrderId) && !dn.IsDeleted)
                .OrderByDescending(dn => dn.DeliveryDate)
                .ToListAsync();

            // Fridges purchased from this supplier (direct link via Fridge.SupplierId)
            var fridgesPurchased = await _context.Fridges
                .Where(f => f.SupplierId == id && f.Status == Status.Active) // or all statuses
                .OrderByDescending(f => f.PurchaseDate)
                .ToListAsync();

            // Average delivery time (in days) based on delivery notes vs purchase order dates
            double averageDeliveryDays = 0;
            if (deliveryNotes.Any())
            {
                var deliveryDays = new List<int>();
                foreach (var dn in deliveryNotes)
                {
                    var po = purchaseOrders.FirstOrDefault(po => po.Id == dn.PurchaseOrderId);
                    if (po != null)
                    {
                        var days = (dn.DeliveryDate - po.OrderDate).Days;
                        deliveryDays.Add(days);
                    }
                }
                averageDeliveryDays = deliveryDays.Any() ? deliveryDays.Average() : 0;
            }

            // Supplier rating – placeholder (you can add a Rating property to Supplier model)
            double supplierRating = 0; // or fetch from some Reviews table, e.g., _context.SupplierReviews.Average(r => r.Rating)

            ViewBag.Supplier = supplier;
            ViewBag.Quotations = quotations;
            ViewBag.PurchaseOrders = purchaseOrders;
            ViewBag.DeliveryNotes = deliveryNotes;
            ViewBag.FridgesPurchased = fridgesPurchased;
            ViewBag.AverageDeliveryDays = averageDeliveryDays;
            ViewBag.SupplierRating = supplierRating;

            return View();
        }


        // ==================== EMPLOYEE PROFILE ====================
        [AllowAnonymous]
        public async Task<IActionResult> EmployeeProfile(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Status == Status.Active);
            if (user == null) return NotFound();

            // Assigned faults (for fault technicians)
            var assignedFaults = await _context.FaultReports
                .Include(f => f.Fridge)
                .Where(f => f.AssignedTechnicianId == id && !f.IsDeleted)
                .OrderByDescending(f => f.ReportedDate)
                .ToListAsync();

            // Completed faults
            var completedFaults = assignedFaults
                .Where(f => f.Status == FaultStatus.Repaired)
                .ToList();

            // Assigned maintenance schedules (for maintenance technicians)
            var assignedMaintenance = await _context.MaintenanceSchedules
                .Include(s => s.Fridge)
                .Where(s => s.AssignedTechnicianId == id && !s.IsDeleted)
                .OrderByDescending(s => s.ScheduledDate)
                .ToListAsync();

            // Completed maintenance logs
            var completedMaintenanceLogs = await _context.MaintenanceLogs
                .Include(l => l.MaintenanceSchedule)
                    .ThenInclude(s => s!.Fridge)
                .Where(l => l.TechnicianId == id && !l.IsDeleted)
                .OrderByDescending(l => l.CompletedDate)
                .ToListAsync();

            // Performance metrics
            int totalFaultsAssigned = assignedFaults.Count;
            int totalFaultsCompleted = completedFaults.Count;
            double faultCompletionRate = totalFaultsAssigned > 0
                ? (double)totalFaultsCompleted / totalFaultsAssigned * 100 : 0;

            int totalMaintenanceAssigned = assignedMaintenance.Count;
            int totalMaintenanceCompleted = completedMaintenanceLogs.Count;
            double maintenanceCompletionRate = totalMaintenanceAssigned > 0
                ? (double)totalMaintenanceCompleted / totalMaintenanceAssigned * 100 : 0;

            // Average fault resolution time (not fully modelled – placeholder)
            double avgFaultResolutionDays = 0;   // can be extended later

            // Notifications
            var notifications = await _context.Notifications
                .Where(n => n.UserId == id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToListAsync();

            // Recent activity: combine latest faults & maintenance events
            var recentActivity = new List<dynamic>();
            foreach (var f in assignedFaults.Take(10))
            {
                recentActivity.Add(new
                {
                    Date = f.ReportedDate,
                    Description = $"Fault reported for {f.Fridge?.SerialNumber}: {f.Description}",
                    Type = f.Status == FaultStatus.Repaired ? "Completed" : "Assigned"
                });
            }
            foreach (var s in assignedMaintenance.Take(10))
            {
                recentActivity.Add(new
                {
                    Date = s.ScheduledDate,
                    Description = $"Maintenance scheduled for {s.Fridge?.SerialNumber}",
                    Type = "Scheduled"
                });
            }
            foreach (var l in completedMaintenanceLogs.Take(10))
            {
                recentActivity.Add(new
                {
                    Date = l.CompletedDate ?? DateTime.MinValue,
                    Description = $"Maintenance completed for {l.MaintenanceSchedule?.Fridge?.SerialNumber}",
                    Type = "Completed"
                });
            }
            recentActivity = recentActivity.OrderByDescending(a => a.Date).Take(15).ToList();

            ViewBag.User = user;
            ViewBag.AssignedFaults = assignedFaults;
            ViewBag.CompletedFaults = completedFaults;
            ViewBag.AssignedMaintenance = assignedMaintenance;
            ViewBag.CompletedMaintenanceLogs = completedMaintenanceLogs;
            ViewBag.FaultCompletionRate = faultCompletionRate;
            ViewBag.MaintenanceCompletionRate = maintenanceCompletionRate;
            ViewBag.AvgFaultResolutionDays = avgFaultResolutionDays;
            ViewBag.Notifications = notifications;
            ViewBag.RecentActivity = recentActivity;

            return View();
        }




        private bool SupplierExists(int id) => _context.Suppliers.Any(e => e.Id == id);
    }
}