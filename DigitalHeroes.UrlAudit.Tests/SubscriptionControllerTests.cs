using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Tests;

public class SubscriptionControllerTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UrlAuditDbContext(options);
    }

    private static SubscriptionController CreateController(
        UrlAuditDbContext context,
        int userId)
    {
        var controller = new SubscriptionController(context);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                userId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication");

        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        return controller;
    }

    [Fact]
    public void GetPlans_ReturnsAllFourPlans()
    {
        using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = controller.GetPlans();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var plans = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            okResult.Value);

        var planList = plans
            .Cast<object>()
            .ToList();

        Assert.Equal(4, planList.Count);

        var planNames = planList
            .Select(x => x.GetType().GetProperty("name")?.GetValue(x)?.ToString())
            .ToList();

        Assert.Contains(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            planNames);

        Assert.Contains(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter,
            planNames);

        Assert.Contains(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Professional,
            planNames);

        Assert.Contains(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Agency,
            planNames);
    }

    [Fact]
    public async Task GetUsage_ForNewUser_CreatesFreeSubscription()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var subscription = await context.Subscriptions
            .SingleAsync(x => x.UserId == 1);

        Assert.Equal(DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free, subscription.Plan);
        Assert.Equal(0, subscription.MonthlyPrice);
        Assert.Equal(100, subscription.MonthlyAuditLimit);
        Assert.True(subscription.IsActive);
        Assert.Equal("Active", subscription.Status);
    }

    [Fact]
    public async Task GetUsage_ForExistingSubscription_ReturnsExistingSubscription()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter,
            MonthlyPrice = 199,
            MonthlyAuditLimit = 500,
            IsActive = true,
            Status = "Active"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var subscriptions = await context.Subscriptions
            .Where(x => x.UserId == 1)
            .ToListAsync();

        Assert.Single(subscriptions);

        var subscription = subscriptions[0];

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter,
            subscription.Plan);

        Assert.Equal(199, subscription.MonthlyPrice);
        Assert.Equal(500, subscription.MonthlyAuditLimit);
        Assert.True(subscription.IsActive);
        Assert.Equal("Active", subscription.Status);
    }

    [Fact]  
    public async Task GetUsage_WithCurrentMonthAudits_ReturnsCorrectUsage()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            MonthlyPrice = 0,
            MonthlyAuditLimit = 100,
            IsActive = true,
            Status = "Active"
        });

        var website = new Website
        {
            Name = "Test Website",
            Url = "https://example.com",
            UserId = 1,
            IsActive = true
        };

        context.Websites.Add(website);
        await context.SaveChangesAsync();

        var auditDate = DateTime.UtcNow;

        context.AuditHistories.AddRange(
            new AuditHistory
            {
                WebsiteId = website.Id,
                Url = website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            },
            new AuditHistory
            {
                WebsiteId = website.Id,
                Url = website.Url,
                StatusCode = 200,
                ResponseTimeMs = 150,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            },
            new AuditHistory
            {
                WebsiteId = website.Id,
                Url = website.Url,
                StatusCode = 200,
                ResponseTimeMs = 200,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var usage = Assert.IsType<SubscriptionDto>(okResult.Value);

        Assert.Equal(3, usage.AuditsUsed);
        Assert.Equal(97, usage.RemainingAudits);
        Assert.Equal(3, usage.UsagePercentage);
        Assert.False(usage.IsUnlimited);

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            usage.Plan);

        Assert.Equal(100, usage.MonthlyAuditLimit);
    }

    [Fact]
    public async Task GetUsage_DoesNotCountAuditsFromAnotherUser()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            MonthlyPrice = 0,
            MonthlyAuditLimit = 100,
            IsActive = true,
            Status = "Active"
        });

        var user1Website = new Website
        {
            Name = "User 1 Website",
            Url = "https://user1.com",
            UserId = 1,
            IsActive = true
        };

        var user2Website = new Website
        {
            Name = "User 2 Website",
            Url = "https://user2.com",
            UserId = 2,
            IsActive = true
        };

        context.Websites.AddRange(user1Website, user2Website);
        await context.SaveChangesAsync();

        var auditDate = DateTime.UtcNow;

        context.AuditHistories.AddRange(
            new AuditHistory
            {
                WebsiteId = user1Website.Id,
                Url = user1Website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            },
            new AuditHistory
            {
                WebsiteId = user2Website.Id,
                Url = user2Website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            },
            new AuditHistory
            {
                WebsiteId = user2Website.Id,
                Url = user2Website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<SubscriptionDto>(okResult.Value);

        Assert.Equal(1, usage.AuditsUsed);
        Assert.Equal(99, usage.RemainingAudits);
        Assert.Equal(1, usage.UsagePercentage);
        Assert.False(usage.IsUnlimited);
    }

    [Fact]
    public async Task GetUsage_WhenAuditsExceedLimit_CapsRemainingAtZeroAndPercentageAt100()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            MonthlyPrice = 0,
            MonthlyAuditLimit = 100,
            IsActive = true,
            Status = "Active"
        });

        var website = new Website
        {
            Name = "Test Website",
            Url = "https://example.com",
            UserId = 1,
            IsActive = true
        };

        context.Websites.Add(website);
        await context.SaveChangesAsync();

        var auditDate = DateTime.UtcNow;

        var audits = Enumerable.Range(1, 105)
            .Select(_ => new AuditHistory
            {
                WebsiteId = website.Id,
                Url = website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            });

        context.AuditHistories.AddRange(audits);
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<SubscriptionDto>(okResult.Value);

        Assert.Equal(105, usage.AuditsUsed);
        Assert.Equal(0, usage.RemainingAudits);
        Assert.Equal(100, usage.UsagePercentage);
        Assert.False(usage.IsUnlimited);
    }

    [Fact]
    public async Task GetUsage_ForUnlimitedPlan_ReturnsUnlimitedUsage()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Agency,
            MonthlyPrice = 1499,
            MonthlyAuditLimit = -1,
            IsActive = true,
            Status = "Active"
        });

        var website = new Website
        {
            Name = "Test Website",
            Url = "https://example.com",
            UserId = 1,
            IsActive = true
        };

        context.Websites.Add(website);
        await context.SaveChangesAsync();

        var auditDate = DateTime.UtcNow;

        var audits = Enumerable.Range(1, 105)
            .Select(_ => new AuditHistory
            {
                WebsiteId = website.Id,
                Url = website.Url,
                StatusCode = 200,
                ResponseTimeMs = 100,
                IsReachable = true,
                Message = "OK",
                CreatedAt = auditDate
            });

        context.AuditHistories.AddRange(audits);
        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<SubscriptionDto>(okResult.Value);

        Assert.Equal(105, usage.AuditsUsed);
        Assert.Equal(-1, usage.RemainingAudits);
        Assert.Equal(0, usage.UsagePercentage);
        Assert.True(usage.IsUnlimited);

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Agency,
            usage.Plan);

        Assert.Equal(1499, usage.MonthlyPrice);
        Assert.Equal(-1, usage.MonthlyAuditLimit);
    }

    [Fact]
    public async Task Upgrade_ToFreePlan_ActivatesFreePlan()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free
            });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var subscription = await context.Subscriptions
            .SingleAsync(x => x.UserId == 1);

        Assert.Equal(DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free, subscription.Plan);
        Assert.Equal(0, subscription.MonthlyPrice);
        Assert.Equal(100, subscription.MonthlyAuditLimit);
        Assert.True(subscription.IsActive);
        Assert.Equal("Active", subscription.Status);
        Assert.Null(subscription.EndDate);
    }

    [Fact]
    public async Task Upgrade_ToPaidPlan_ReturnsForbidden()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter
            });

        var forbiddenResult =
            Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            forbiddenResult.StatusCode);

        Assert.Empty(
            await context.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task Upgrade_ToInvalidPlan_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = "InvalidPlan"
            });

        var badRequestResult =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequestResult.StatusCode);

        Assert.Empty(
            await context.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task Upgrade_ToFreePlan_WithExistingSubscription_UpdatesExistingSubscription()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            MonthlyPrice = 0,
            MonthlyAuditLimit = 100,
            IsActive = true,
            Status = "Active"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free
            });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var subscriptions = await context.Subscriptions
            .Where(x => x.UserId == 1)
            .ToListAsync();

        Assert.Single(subscriptions);

        var subscription = subscriptions[0];

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            subscription.Plan);

        Assert.Equal(0, subscription.MonthlyPrice);
        Assert.Equal(100, subscription.MonthlyAuditLimit);
        Assert.True(subscription.IsActive);
        Assert.Equal("Active", subscription.Status);
        Assert.Null(subscription.EndDate);
    }

    [Fact]
    public async Task GetUsage_WithInvalidUserIdentity_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller = new SubscriptionController(context);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "invalid-user-id")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var result = await controller.GetUsage();

        var unauthorizedResult =
            Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorizedResult.StatusCode);

        Assert.Empty(
            await context.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task GetUsage_WhenPaidSubscriptionIsExpired_MarksSubscriptionExpired()
    {
        await using var context = CreateContext();

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter,
            MonthlyPrice = 199,
            MonthlyAuditLimit = 500,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            Status = "Active"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetUsage();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<SubscriptionDto>(okResult.Value);

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Starter,
            usage.Plan);

        Assert.False(usage.IsActive);
        Assert.Equal("Expired", usage.Status);

        var subscription = await context.Subscriptions
            .SingleAsync(x => x.UserId == 1);

        Assert.False(subscription.IsActive);
        Assert.Equal("Expired", subscription.Status);
    }

    [Fact]
    public async Task Upgrade_WithInvalidUserIdentity_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller = new SubscriptionController(context);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "invalid-user-id")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free
            });

        var unauthorizedResult =
            Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorizedResult.StatusCode);

        Assert.Empty(
            await context.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task Upgrade_WithEmptyPlan_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = "   "
            });

        var badRequestResult =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequestResult.StatusCode);

        Assert.Empty(
            await context.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task Upgrade_WithWhitespaceAroundFreePlan_ActivatesFreePlan()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = await controller.Upgrade(
            new UpgradeSubscriptionRequest
            {
                Plan = "  Free  "
            });

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        var subscription = await context.Subscriptions
            .SingleAsync(x => x.UserId == 1);

        Assert.Equal(
            DigitalHeroes.UrlAudit.Api.Configuration.PlanDefinitions.Free,
            subscription.Plan);

        Assert.Equal(0, subscription.MonthlyPrice);
        Assert.Equal(100, subscription.MonthlyAuditLimit);
        Assert.True(subscription.IsActive);
        Assert.Equal("Active", subscription.Status);
    }
}
