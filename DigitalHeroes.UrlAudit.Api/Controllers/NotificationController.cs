using DigitalHeroes.UrlAudit.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly UrlAuditDbContext _context;

    public NotificationController(
        UrlAuditDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.UserId == userId)
            .OrderByDescending(notification =>
                notification.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var count = await _context.Notifications
            .CountAsync(notification =>
                notification.UserId == userId &&
                !notification.IsRead);

        return Ok(new
        {
            count
        });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.UserId == userId);

        if (notification is null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        notification.IsRead = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Notification marked as read."
        });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var notifications =
            await _context.Notifications
                .Where(notification =>
                    notification.UserId == userId &&
                    !notification.IsRead)
                .ToListAsync();

        if (notifications.Count == 0)
        {
            return Ok(new
            {
                success = true,
                message = "No unread notifications."
            });
        }

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "All notifications marked as read."
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.UserId == userId);

        if (notification is null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        _context.Notifications.Remove(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Notification deleted."
        });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier);
    }
}