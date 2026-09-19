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
    public void GetPlans_ReturnsAllPlans()
    {
        using var context = CreateContext();
        var controller = CreateController(context, 1);

        var result = controller.GetPlans();

        var okResult = Assert.IsType<OkObjectResult>(result);

        var plans = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            okResult.Value);

        Assert.NotNull(plans);
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
}
