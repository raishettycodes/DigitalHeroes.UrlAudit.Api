using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Tests;

public class NotificationControllerTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UrlAuditDbContext(options);
    }

    private static NotificationController CreateController(
        UrlAuditDbContext context,
        string? userId = null)
    {
        var controller = new NotificationController(context);

        var claims = userId is null
            ? new List<Claim>()
            : new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId)
            };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        claims,
                        "TestAuth"))
            }
        };

        return controller;
    }

    private static Notification CreateNotification(
        string userId,
        string title,
        string message,
        bool isRead,
        DateTime createdAt,
        string type = "Info")
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = isRead,
            CreatedAt = createdAt
        };
    }

    [Fact]
    public async Task GetNotifications_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetNotifications();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCurrentUsersNotifications()
    {
        await using var context = CreateContext();

        context.Notifications.AddRange(
            CreateNotification(
                "user-1",
                "Welcome",
                "Welcome message",
                false,
                new DateTime(2026, 1, 1)),
            CreateNotification(
                "user-2",
                "Other",
                "Other user message",
                false,
                new DateTime(2026, 1, 2)));

        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.GetNotifications();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var notifications =
            Assert.IsAssignableFrom<IEnumerable<Notification>>(
                okResult.Value);

        var list = notifications.ToList();

        Assert.Single(list);
        Assert.Equal("user-1", list[0].UserId);
        Assert.Equal("Welcome", list[0].Title);
    }

    [Fact]
    public async Task GetNotifications_ReturnsNewestFirst()
    {
        await using var context = CreateContext();

        context.Notifications.AddRange(
            CreateNotification(
                "user-1",
                "Older",
                "Older notification",
                false,
                new DateTime(2026, 1, 1)),
            CreateNotification(
                "user-1",
                "Newer",
                "Newer notification",
                false,
                new DateTime(2026, 1, 3)),
            CreateNotification(
                "user-1",
                "Middle",
                "Middle notification",
                false,
                new DateTime(2026, 1, 2)));

        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.GetNotifications();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var notifications =
            Assert.IsAssignableFrom<IEnumerable<Notification>>(
                okResult.Value);

        var list = notifications.ToList();

        Assert.Equal(3, list.Count);
        Assert.Equal("Newer", list[0].Title);
        Assert.Equal("Middle", list[1].Title);
        Assert.Equal("Older", list[2].Title);
    }

    [Fact]
    public async Task GetUnreadCount_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetUnreadCount();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyCurrentUsersUnreadNotifications()
    {
        await using var context = CreateContext();

        context.Notifications.AddRange(
            CreateNotification(
                "user-1",
                "Unread 1",
                "Message",
                false,
                new DateTime(2026, 1, 1)),
            CreateNotification(
                "user-1",
                "Read",
                "Message",
                true,
                new DateTime(2026, 1, 2)),
            CreateNotification(
                "user-1",
                "Unread 2",
                "Message",
                false,
                new DateTime(2026, 1, 3)),
            CreateNotification(
                "user-2",
                "Other unread",
                "Message",
                false,
                new DateTime(2026, 1, 4)));

        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.GetUnreadCount();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var value = okResult.Value!;
        var countProperty = value.GetType().GetProperty("count");

        Assert.NotNull(countProperty);

        var count = (int)countProperty!.GetValue(value)!;

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAsRead_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.MarkAsRead(1);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, "user-1");

        var result = await controller.MarkAsRead(999);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        var value = notFoundResult.Value!;
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(messageProperty);
        Assert.Equal(
            "Notification not found.",
            messageProperty!.GetValue(value));
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationBelongsToAnotherUser_ReturnsNotFound()
    {
        await using var context = CreateContext();

        var notification = CreateNotification(
            "user-2",
            "Other user's notification",
            "Message",
            false,
            new DateTime(2026, 1, 1));

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.MarkAsRead(notification.Id);

        Assert.IsType<NotFoundObjectResult>(result);

        var stored = await context.Notifications
            .SingleAsync();

        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationExists_MarksItAsRead()
    {
        await using var context = CreateContext();

        var notification = CreateNotification(
            "user-1",
            "Test notification",
            "Message",
            false,
            new DateTime(2026, 1, 1));

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.MarkAsRead(notification.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var value = okResult.Value!;
        var successProperty = value.GetType().GetProperty("success");
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);

        Assert.True((bool)successProperty!.GetValue(value)!);
        Assert.Equal(
            "Notification marked as read.",
            messageProperty!.GetValue(value));

        var stored = await context.Notifications
            .SingleAsync();

        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.MarkAllAsRead();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenNoUnreadNotifications_ReturnsSuccessMessage()
    {
        await using var context = CreateContext();

        context.Notifications.AddRange(
            CreateNotification(
                "user-1",
                "Read notification",
                "Message",
                true,
                new DateTime(2026, 1, 1)),
            CreateNotification(
                "user-2",
                "Other notification",
                "Message",
                false,
                new DateTime(2026, 1, 2)));

        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.MarkAllAsRead();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var value = okResult.Value!;
        var successProperty = value.GetType().GetProperty("success");
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);

        Assert.True((bool)successProperty!.GetValue(value)!);
        Assert.Equal(
            "No unread notifications.",
            messageProperty!.GetValue(value));

        var otherUserNotification = await context.Notifications
            .SingleAsync(x => x.UserId == "user-2");

        Assert.False(otherUserNotification.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersUnreadNotifications()
    {
        await using var context = CreateContext();

        context.Notifications.AddRange(
            CreateNotification(
                "user-1",
                "Unread 1",
                "Message",
                false,
                new DateTime(2026, 1, 1)),
            CreateNotification(
                "user-1",
                "Unread 2",
                "Message",
                false,
                new DateTime(2026, 1, 2)),
            CreateNotification(
                "user-1",
                "Already read",
                "Message",
                true,
                new DateTime(2026, 1, 3)),
            CreateNotification(
                "user-2",
                "Other unread",
                "Message",
                false,
                new DateTime(2026, 1, 4)));

        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.MarkAllAsRead();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var value = okResult.Value!;
        var successProperty = value.GetType().GetProperty("success");
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);

        Assert.True((bool)successProperty!.GetValue(value)!);
        Assert.Equal(
            "All notifications marked as read.",
            messageProperty!.GetValue(value));

        var userNotifications = await context.Notifications
            .Where(x => x.UserId == "user-1")
            .ToListAsync();

        Assert.All(
            userNotifications,
            notification => Assert.True(notification.IsRead));

        var otherUserNotification = await context.Notifications
            .SingleAsync(x => x.UserId == "user-2");

        Assert.False(otherUserNotification.IsRead);
    }

    [Fact]
    public async Task Delete_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Delete(1);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, "user-1");

        var result = await controller.Delete(999);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        var value = notFoundResult.Value!;
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(messageProperty);
        Assert.Equal(
            "Notification not found.",
            messageProperty!.GetValue(value));
    }

    [Fact]
    public async Task Delete_WhenNotificationBelongsToAnotherUser_ReturnsNotFound()
    {
        await using var context = CreateContext();

        var notification = CreateNotification(
            "user-2",
            "Other user's notification",
            "Message",
            false,
            new DateTime(2026, 1, 1));

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "user-1");

        var result = await controller.Delete(notification.Id);

        Assert.IsType<NotFoundObjectResult>(result);

        var stillExists = await context.Notifications
            .AnyAsync(x => x.Id == notification.Id);

        Assert.True(stillExists);
    }

    [Fact]
    public async Task Delete_WhenNotificationExists_DeletesIt()
    {
        await using var context = CreateContext();

        var notification = CreateNotification(
            "user-1",
            "Delete me",
            "Message",
            false,
            new DateTime(2026, 1, 1));

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var notificationId = notification.Id;

        var controller = CreateController(context, "user-1");

        var result = await controller.Delete(notificationId);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var value = okResult.Value!;
        var successProperty = value.GetType().GetProperty("success");
        var messageProperty = value.GetType().GetProperty("message");

        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);

        Assert.True((bool)successProperty!.GetValue(value)!);
        Assert.Equal(
            "Notification deleted.",
            messageProperty!.GetValue(value));

        var exists = await context.Notifications
            .AnyAsync(x => x.Id == notificationId);

        Assert.False(exists);
    }
}
