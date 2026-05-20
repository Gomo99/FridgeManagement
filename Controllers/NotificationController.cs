using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FridgeManagement.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public NotificationController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // Get current user ID
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 0;
        }

        // Display all notifications
        public async Task<IActionResult> Index(int page = 1)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, 20);
            ViewBag.UnreadCount = await _notificationService.GetUnreadCountAsync(userId);
            ViewBag.CurrentPage = page;

            return View(notifications);
        }

        // Get unread notifications (for dropdown/bell icon)
        public async Task<IActionResult> Unread()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false });

            var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
            return Json(new
            {
                success = true,
                count = notifications.Count,
                notifications = notifications.Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type.ToString(),
                    createdAt = n.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                    actionUrl = n.ActionUrl
                })
            });
        }

        // Get unread count (AJAX endpoint)
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { count = 0 });

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        // Mark as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = "User not found" });

            var result = await _notificationService.MarkAsReadAsync(id, userId);

            if (result)
            {
                var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
                return Json(new { success = true, unreadCount });
            }

            return Json(new { success = false, message = "Notification not found" });
        }

        // Mark all as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = "User not found" });

            var count = await _notificationService.MarkAllAsReadAsync(userId);
            return Json(new { success = true, count });
        }

        // Delete notification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = "User not found" });

            var result = await _notificationService.DeleteNotificationAsync(id, userId);
            return Json(new { success = result });
        }
    }
}