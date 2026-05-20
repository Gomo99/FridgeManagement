using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace FridgeManagement.Service
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Create a notification for a specific user
        public async Task<Notification> CreateNotificationAsync(
            int userId,
            string title,
            string message,
            NotificationType type,
            string? actionUrl = null,
            string? relatedEntityType = null,
            int? relatedEntityId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification;
        }

        // Create notifications for multiple users
        public async Task CreateNotificationsAsync(
            List<int> userIds,
            string title,
            string message,
            NotificationType type,
            string? actionUrl = null,
            string? relatedEntityType = null,
            int? relatedEntityId = null)
        {
            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.Now,
                IsRead = false
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        // Notify all users with a specific role
        public async Task NotifyRoleAsync(
            UserRole role,
            string title,
            string message,
            NotificationType type,
            string? actionUrl = null,
            string? relatedEntityType = null,
            int? relatedEntityId = null)
        {
            var userIds = await _context.Users
                .Where(u => u.Role == role && u.Status == AppStatus.Status.Active)
                .Select(u => u.Id)
                .ToListAsync();

            if (userIds.Any())
            {
                await CreateNotificationsAsync(userIds, title, message, type, actionUrl, relatedEntityType, relatedEntityId);
            }
        }

        // Get unread notifications for a user
        public async Task<List<Notification>> GetUnreadNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        // Get all notifications for a user (with pagination)
        public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int pageNumber = 1, int pageSize = 20)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // Mark notification as read
        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null || notification.IsRead)
                return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return true;
        }

        // Mark all notifications as read for a user
        public async Task<int> MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return unreadNotifications.Count;
        }

        // Get unread count
        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        // Delete old read notifications (cleanup)
        public async Task<int> DeleteOldNotificationsAsync(int daysOld = 30)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            var oldNotifications = await _context.Notifications
                .Where(n => n.IsRead && n.ReadAt < cutoffDate)
                .ToListAsync();

            _context.Notifications.RemoveRange(oldNotifications);
            await _context.SaveChangesAsync();

            return oldNotifications.Count;
        }

        // Delete a specific notification
        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}