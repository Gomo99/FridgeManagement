using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "ADMINISTRATOR")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DASHBOARD ====================
        public IActionResult DashBoard()
        {
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
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
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




        private bool SupplierExists(int id) => _context.Suppliers.Any(e => e.Id == id);
    }
}