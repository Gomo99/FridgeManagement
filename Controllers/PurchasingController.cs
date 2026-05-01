using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FridgeManagement.Controllers
{
    [Authorize(Roles = "PURCHASINGMANAGER")]
    public class PurchasingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchasingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> DashBoard()
        {
            ViewBag.PendingRequests = await _context.PurchaseRequests.CountAsync(r => r.Status == PurchaseRequestStatus.Pending);
            ViewBag.PendingRFQs = await _context.RequestForQuotations.CountAsync(r => r.Status == RFQStatus.Draft);
            ViewBag.PendingQuotations = await _context.Quotations.CountAsync(q => q.Status == QuotationStatus.Received);
            ViewBag.OpenOrders = await _context.PurchaseOrders.CountAsync(po => po.Status == PurchaseOrderStatus.Ordered || po.Status == PurchaseOrderStatus.Shipped);
            return View();
        }

        // ==================== SUPPLIER MANAGEMENT ====================
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
                TempData["SuccessMessage"] = "Supplier added.";
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
            TempData["SuccessMessage"] = "Supplier updated.";
            return RedirectToAction(nameof(Suppliers));
        }

        public async Task<IActionResult> DeleteSupplier(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.Status == Status.Active);
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
                supplier.Status = Status.Inactive; // Soft delete
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Supplier deleted (soft).";
            }
            return RedirectToAction(nameof(Suppliers));
        }



        public async Task<IActionResult> RestoreSupplier(int? id)
        {
            if (id == null) return NotFound();
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.Status == Status.Inactive);
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
                supplier.Status = Status.Active; // Soft delete
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Supplier restored.";
            }
            return RedirectToAction(nameof(Suppliers));
        }



        // ==================== PURCHASE REQUESTS ====================
        public async Task<IActionResult> PurchaseRequests()
        {
            var requests = await _context.PurchaseRequests
                .Include(r => r.RequestedBy)
                .Where(r => r.Status == PurchaseRequestStatus.Pending || r.Status == PurchaseRequestStatus.Approved)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();
            return View(requests);
        }

        public async Task<IActionResult> ProcessPurchaseRequest(int? id)
        {
            if (id == null) return NotFound();
            var request = await _context.PurchaseRequests
                .Include(r => r.RequestedBy)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int id)
        {
            var request = await _context.PurchaseRequests.FindAsync(id);
            if (request != null && request.Status == PurchaseRequestStatus.Pending)
            {
                request.Status = PurchaseRequestStatus.Approved;
                _context.Update(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Request approved.";
            }
            return RedirectToAction(nameof(PurchaseRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int id)
        {
            var request = await _context.PurchaseRequests.FindAsync(id);
            if (request != null)
            {
                request.Status = PurchaseRequestStatus.Rejected;
                _context.Update(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Request rejected.";
            }
            return RedirectToAction(nameof(PurchaseRequests));
        }

        // ==================== REQUEST FOR QUOTATION (RFQ) ====================
        public async Task<IActionResult> RFQs()
        {
            var rfqs = await _context.RequestForQuotations
                .Include(r => r.PurchaseRequest)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return View(rfqs);
        }

        public async Task<IActionResult> CreateRFQ(int? purchaseRequestId)
        {
            if (purchaseRequestId == null) return RedirectToAction(nameof(PurchaseRequests));

            var request = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
            if (request == null) return NotFound();

            var rfq = new RequestForQuotation
            {
                PurchaseRequestId = request.Id,
                Status = RFQStatus.Draft
            };

            // Load suppliers for selection
            ViewBag.Suppliers = new MultiSelectList(await _context.Suppliers.Where(s => s.Status == Status.Active).ToListAsync(), "Id", "Name");
            return View(rfq);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRFQ(RequestForQuotation rfq, int[] selectedSuppliers)
        {
            if (ModelState.IsValid && selectedSuppliers.Length > 0)
            {
                rfq.CreatedDate = DateTime.Now;
                rfq.IsDeleted = false;
                _context.Add(rfq);
                await _context.SaveChangesAsync();

                foreach (var supplierId in selectedSuppliers)
                {
                    _context.RFQSuppliers.Add(new RFQSupplier { RFQId = rfq.Id, SupplierId = supplierId });
                }
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "RFQ created and sent to selected suppliers.";
                return RedirectToAction(nameof(RFQs));
            }

            ViewBag.Suppliers = new MultiSelectList(await _context.Suppliers.Where(s => s.Status == Status.Active).ToListAsync(), "Id", "Name", selectedSuppliers);
            return View(rfq);
        }

        public async Task<IActionResult> RFQDetails(int? id)
        {
            if (id == null) return NotFound();
            var rfq = await _context.RequestForQuotations
                .Include(r => r.PurchaseRequest)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (rfq == null) return NotFound();

            var suppliers = await _context.RFQSuppliers
                .Include(rs => rs.Supplier)
                .Where(rs => rs.RFQId == id)
                .Select(rs => rs.Supplier)
                .ToListAsync();
            ViewBag.Suppliers = suppliers;

            var quotations = await _context.Quotations
                .Include(q => q.Supplier)
                .Where(q => q.RFQId == id && !q.IsDeleted)
                .ToListAsync();
            ViewBag.Quotations = quotations;

            return View(rfq);
        }

        [HttpGet]
        public async Task<JsonResult> GetPendingPurchaseRequests()
        {
            var requests = await _context.PurchaseRequests
                .Where(r => r.Status == PurchaseRequestStatus.Approved) // only approved can become RFQ
                .Select(r => new { id = r.Id, reason = r.Reason, quantity = r.QuantityRequested })
                .ToListAsync();
            return Json(requests);
        }

        [HttpGet]
        public async Task<JsonResult> GetActiveSuppliers()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.Status == Status.Active)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();
            return Json(suppliers);
        }






        // ==================== QUOTATIONS ====================
        public async Task<IActionResult> ReceiveQuotation(int? rfqId)
        {
            if (rfqId == null) return NotFound();
            var rfq = await _context.RequestForQuotations.FindAsync(rfqId);
            if (rfq == null) return NotFound();

            var suppliers = await _context.RFQSuppliers
                .Include(rs => rs.Supplier)
                .Where(rs => rs.RFQId == rfqId)
                .Select(rs => rs.Supplier)
                .ToListAsync();
            ViewBag.Suppliers = new SelectList(suppliers, "Id", "Name");

            var quotation = new Quotation { RFQId = rfq.Id };
            return View(quotation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveQuotation(Quotation quotation)
        {
            if (ModelState.IsValid)
            {
                quotation.ReceivedDate = DateTime.Now;
                quotation.Status = QuotationStatus.Received;
                quotation.IsDeleted = false;
                _context.Add(quotation);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Quotation received.";
                return RedirectToAction(nameof(RFQDetails), new { id = quotation.RFQId });
            }

            var rfq = await _context.RequestForQuotations.FindAsync(quotation.RFQId);
            var suppliers = await _context.RFQSuppliers
                .Include(rs => rs.Supplier)
                .Where(rs => rs.RFQId == quotation.RFQId)
                .Select(rs => rs.Supplier)
                .ToListAsync();
            ViewBag.Suppliers = new SelectList(suppliers, "Id", "Name", quotation.SupplierId);
            return View(quotation);
        }

        public async Task<IActionResult> AcceptQuotation(int? id)
        {
            if (id == null) return NotFound();
            var quotation = await _context.Quotations
                .Include(q => q.RFQ).ThenInclude(r => r!.PurchaseRequest)
                .Include(q => q.Supplier)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (quotation == null) return NotFound();
            return View(quotation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuotationConfirmed(int id)
        {
            var quotation = await _context.Quotations.FindAsync(id);
            if (quotation != null)
            {
                // Reject all other quotations for this RFQ
                var otherQuotations = await _context.Quotations
                    .Where(q => q.RFQId == quotation.RFQId && q.Id != id)
                    .ToListAsync();
                foreach (var q in otherQuotations)
                {
                    q.Status = QuotationStatus.Rejected;
                }
                quotation.Status = QuotationStatus.Accepted;
                await _context.SaveChangesAsync();

                // Automatically create Purchase Order
                var po = new PurchaseOrder
                {
                    QuotationId = quotation.Id,
                    OrderDate = DateTime.Now,
                    ExpectedDeliveryDate = DateTime.Now.AddDays(quotation.EstimatedDeliveryDays ?? 7),
                    Status = PurchaseOrderStatus.Ordered
                };
                _context.Add(po);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quotation accepted and Purchase Order created.";
                return RedirectToAction(nameof(PurchaseOrders));
            }
            return RedirectToAction(nameof(RFQs));
        }

        // ==================== PURCHASE ORDERS ====================
        public async Task<IActionResult> PurchaseOrders()
        {
            var orders = await _context.PurchaseOrders
                .Include(po => po.Quotation).ThenInclude(q => q!.Supplier)
                .Include(po => po.Quotation).ThenInclude(q => q!.RFQ).ThenInclude(r => r!.PurchaseRequest)
                .Where(po => !po.IsDeleted)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> PurchaseOrderDetails(int? id)
        {
            if (id == null) return NotFound();
            var po = await _context.PurchaseOrders
                .Include(po => po.Quotation).ThenInclude(q => q!.Supplier)
                .Include(po => po.Quotation).ThenInclude(q => q!.RFQ).ThenInclude(r => r!.PurchaseRequest)
                .FirstOrDefaultAsync(po => po.Id == id);
            if (po == null) return NotFound();

            var deliveryNotes = await _context.DeliveryNotes
                .Where(d => d.PurchaseOrderId == id && !d.IsDeleted)
                .ToListAsync();
            ViewBag.DeliveryNotes = deliveryNotes;

            return View(po);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPurchaseOrder(int id)
        {
            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po != null && po.Status != PurchaseOrderStatus.Delivered)
            {
                po.Status = PurchaseOrderStatus.Cancelled;
                po.IsDeleted = true;
                _context.Update(po);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Purchase order cancelled.";
            }
            return RedirectToAction(nameof(PurchaseOrders));
        }

        // ==================== DELIVERY NOTES ====================
        public async Task<IActionResult> ProcessDelivery(int? purchaseOrderId)
        {
            if (purchaseOrderId == null) return NotFound();
            var po = await _context.PurchaseOrders
                .Include(po => po.Quotation).ThenInclude(q => q!.Supplier)
                .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);
            if (po == null) return NotFound();

            var deliveryNote = new DeliveryNote { PurchaseOrderId = po.Id };
            return View(deliveryNote);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessDelivery(DeliveryNote deliveryNote)
        {
            if (ModelState.IsValid)
            {
                deliveryNote.IsDeleted = false;
                _context.Add(deliveryNote);

                // Update purchase order status
                var po = await _context.PurchaseOrders.FindAsync(deliveryNote.PurchaseOrderId);
                if (po != null)
                {
                    po.Status = PurchaseOrderStatus.Delivered;
                }

                // Here we should also update inventory by adding fridges
                // For simplicity, we assume inventory update is handled by Inventory Liaison separately

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Delivery processed.";
                return RedirectToAction(nameof(PurchaseOrders));
            }
            return View(deliveryNote);
        }
    }
}