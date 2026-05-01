using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "SUPPLIER")]
    public class SupplierController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SupplierController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper: Get current Supplier record based on logged-in user's email
        private async Task<Supplier?> GetCurrentSupplierAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Email == userEmail && s.Status == Status.Active);
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> DashBoard()
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Count of pending RFQs (RFQs sent to this supplier with no quotation yet, or quotation not submitted)
            var rfqIdsForSupplier = await _context.RFQSuppliers
                .Where(rs => rs.SupplierId == supplier.Id && !rs.IsDeleted)
                .Select(rs => rs.RFQId)
                .ToListAsync();

            var pendingRFQs = await _context.RequestForQuotations
                .Where(r => rfqIdsForSupplier.Contains(r.Id) && !r.IsDeleted && r.Status == RFQStatus.Sent)
                .CountAsync();

            // Quotations submitted and accepted/rejected
            var quotationsSubmitted = await _context.Quotations
                .Where(q => q.SupplierId == supplier.Id && !q.IsDeleted)
                .CountAsync();

            // Purchase orders for this supplier (via quotation)
            var purchaseOrders = await _context.PurchaseOrders
                .Include(po => po.Quotation)
                .Where(po => po.Quotation.SupplierId == supplier.Id && !po.IsDeleted)
                .CountAsync();

            // Orders awaiting delivery (Ordered or Shipped)
            var awaitingDelivery = await _context.PurchaseOrders
                .Include(po => po.Quotation)
                .Where(po => po.Quotation.SupplierId == supplier.Id && !po.IsDeleted &&
                            (po.Status == PurchaseOrderStatus.Ordered || po.Status == PurchaseOrderStatus.Shipped))
                .CountAsync();

            ViewBag.PendingRFQs = pendingRFQs;
            ViewBag.QuotationsSubmitted = quotationsSubmitted;
            ViewBag.TotalOrders = purchaseOrders;
            ViewBag.AwaitingDelivery = awaitingDelivery;

            return View();
        }

        // ==================== RFQs (View & Submit Quotation) ====================
        public async Task<IActionResult> RFQs()
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            var rfqIds = await _context.RFQSuppliers
                .Where(rs => rs.SupplierId == supplier.Id && !rs.IsDeleted)
                .Select(rs => rs.RFQId)
                .ToListAsync();

            var rfqs = await _context.RequestForQuotations
                .Include(r => r.PurchaseRequest)
                .Where(r => rfqIds.Contains(r.Id) && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            // Mark which ones already have a quotation from this supplier
            var quotedRfqIds = await _context.Quotations
                .Where(q => q.SupplierId == supplier.Id && !q.IsDeleted)
                .Select(q => q.RFQId)
                .ToListAsync();
            ViewBag.QuotedRfqIds = quotedRfqIds;

            return View(rfqs);
        }

        // View RFQ details and submit quotation
        public async Task<IActionResult> RFQDetails(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Verify this RFQ was sent to this supplier
            var rfqSupplier = await _context.RFQSuppliers
                .FirstOrDefaultAsync(rs => rs.RFQId == id && rs.SupplierId == supplier.Id && !rs.IsDeleted);
            if (rfqSupplier == null) return Forbid();

            var rfq = await _context.RequestForQuotations
                .Include(r => r.PurchaseRequest)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (rfq == null) return NotFound();

            // Check if already submitted a quotation
            var existingQuotation = await _context.Quotations
                .FirstOrDefaultAsync(q => q.RFQId == id && q.SupplierId == supplier.Id && !q.IsDeleted);
            ViewBag.ExistingQuotation = existingQuotation;

            return View(rfq);
        }

        // Submit Quotation (GET)
        public async Task<IActionResult> SubmitQuotation(int? rfqId)
        {
            if (rfqId == null) return NotFound();
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Verify RFQ sent to this supplier
            var rfqSupplier = await _context.RFQSuppliers
                .FirstOrDefaultAsync(rs => rs.RFQId == rfqId && rs.SupplierId == supplier.Id && !rs.IsDeleted);
            if (rfqSupplier == null) return Forbid();

            // Check if quotation already exists
            var existing = await _context.Quotations
                .FirstOrDefaultAsync(q => q.RFQId == rfqId && q.SupplierId == supplier.Id && !q.IsDeleted);
            if (existing != null)
            {
                TempData["ErrorMessage"] = "You have already submitted a quotation for this RFQ.";
                return RedirectToAction(nameof(RFQDetails), new { id = rfqId });
            }

            var quotation = new Quotation
            {
                RFQId = rfqId.Value,
                SupplierId = supplier.Id
            };
            return View(quotation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuotation(Quotation quotation)
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Ensure RFQ belongs to supplier
            var rfqSupplier = await _context.RFQSuppliers
                .FirstOrDefaultAsync(rs => rs.RFQId == quotation.RFQId && rs.SupplierId == supplier.Id && !rs.IsDeleted);
            if (rfqSupplier == null) return Forbid();

            if (ModelState.IsValid)
            {
                quotation.SupplierId = supplier.Id;
                quotation.ReceivedDate = DateTime.Now;
                quotation.Status = QuotationStatus.Received;
                quotation.IsDeleted = false;
                _context.Add(quotation);

                // Optionally update RFQ status to indicate a quotation was received
                var rfq = await _context.RequestForQuotations.FindAsync(quotation.RFQId);
                if (rfq != null && rfq.Status == RFQStatus.Sent)
                {
                    rfq.Status = RFQStatus.Sent; // could set to a "QuotationsReceived" status if added
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Quotation submitted successfully.";
                return RedirectToAction(nameof(RFQDetails), new { id = quotation.RFQId });
            }
            return View(quotation);
        }

        // ==================== PURCHASE ORDERS ====================
        public async Task<IActionResult> PurchaseOrders()
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            var orders = await _context.PurchaseOrders
                .Include(po => po.Quotation).ThenInclude(q => q.RFQ).ThenInclude(r => r.PurchaseRequest)
                .Where(po => po.Quotation.SupplierId == supplier.Id && !po.IsDeleted)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> PurchaseOrderDetails(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            var po = await _context.PurchaseOrders
                .Include(po => po.Quotation).ThenInclude(q => q.RFQ).ThenInclude(r => r.PurchaseRequest)
                .FirstOrDefaultAsync(po => po.Id == id && po.Quotation.SupplierId == supplier.Id);
            if (po == null) return NotFound();

            var deliveryNotes = await _context.DeliveryNotes
                .Where(d => d.PurchaseOrderId == id && !d.IsDeleted)
                .ToListAsync();
            ViewBag.DeliveryNotes = deliveryNotes;

            return View(po);
        }

        // ==================== DELIVERY NOTE ====================
        public async Task<IActionResult> CreateDeliveryNote(int? purchaseOrderId)
        {
            if (purchaseOrderId == null) return NotFound();
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Verify this PO belongs to this supplier
            var po = await _context.PurchaseOrders
                .Include(po => po.Quotation)
                .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.Quotation.SupplierId == supplier.Id);
            if (po == null) return Forbid();
            if (po.Status == PurchaseOrderStatus.Delivered || po.Status == PurchaseOrderStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Cannot create delivery note for this order.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = purchaseOrderId });
            }

            var deliveryNote = new DeliveryNote { PurchaseOrderId = po.Id };
            return View(deliveryNote);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDeliveryNote(DeliveryNote deliveryNote)
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            // Verify PO belongs to supplier
            var po = await _context.PurchaseOrders
                .Include(po => po.Quotation)
                .FirstOrDefaultAsync(po => po.Id == deliveryNote.PurchaseOrderId && po.Quotation.SupplierId == supplier.Id);
            if (po == null) return Forbid();

            if (ModelState.IsValid)
            {
                deliveryNote.IsDeleted = false;
                _context.Add(deliveryNote);

                // Update PO status to Shipped (if not already)
                if (po.Status == PurchaseOrderStatus.Ordered)
                {
                    po.Status = PurchaseOrderStatus.Shipped;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Delivery note created. Order status updated.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = deliveryNote.PurchaseOrderId });
            }
            return View(deliveryNote);
        }

        // ==================== MANAGE PROFILE ====================
        public async Task<IActionResult> Profile()
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(Supplier model)
        {
            var supplier = await GetCurrentSupplierAsync();
            if (supplier == null) return RedirectToAction("AccessDenied", "Account");

            if (ModelState.IsValid)
            {
                supplier.ContactPerson = model.ContactPerson;
                supplier.PhoneNumber = model.PhoneNumber;
                supplier.Address = model.Address;
                supplier.Email = model.Email;
                supplier.Name = model.Name;
                supplier.Status = Status.Active;
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile updated.";
                return RedirectToAction(nameof(Profile));
            }
            return View(supplier);
        }
    }
}