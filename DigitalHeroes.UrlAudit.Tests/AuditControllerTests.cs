using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuditControllerTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<UrlAuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        return new UrlAuditDbContext(options);
    }

    private static AuditService CreateAuditService(
        UrlAuditDbContext context)
    {
        var httpClient = new HttpClient(
            new HttpClientHandler());

        var cache =
            new MemoryCache(
                new MemoryCacheOptions());

        var options =
            Options.Create(
                new AuditSettings
                {
                    TimeoutSeconds = 5,
                    CacheDurationMinutes = 5
                });

        var seoAuditService =
            new SeoAuditService();

        return new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            seoAuditService);
    }

    private static AuditController CreateController(
        UrlAuditDbContext context,
        int? userId = null)
    {
        var auditService =
            CreateAuditService(context);

        var controller =
            new AuditController(
                auditService,
                context);

        ClaimsIdentity identity;

        if (userId.HasValue)
        {
            identity =
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            userId.Value.ToString())
                    },
                    "TestAuthentication");
        }
        else
        {
            identity =
                new ClaimsIdentity(
                    authenticationType: "TestAuthentication");
        }

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext
                    {
                        User =
                            new ClaimsPrincipal(identity)
                    }
            };

        return controller;
    }

    [Fact]
    public async Task Audit_WhenModelStateIsInvalid_ReturnsBadRequest()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        controller.ModelState.AddModelError(
            "Url",
            "The Url field is required.");

        var request =
            new AuditRequestDto();

        var result =
            await controller.Audit(request);

        Assert.IsType<BadRequestObjectResult>(
            result);
    }

    [Fact]
    public async Task Audit_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context);

        var request =
            new AuditRequestDto
            {
                Url = "https://example.com"
            };

        var result =
            await controller.Audit(request);

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);

        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task Audit_WhenUserIdIsInvalid_ReturnsUnauthorized()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context);

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        "invalid-user-id")
                },
                "TestAuthentication");

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext
                    {
                        User =
                            new ClaimsPrincipal(identity)
                    }
            };

        var request =
            new AuditRequestDto
            {
                Url = "https://example.com"
            };

        var result =
            await controller.Audit(request);

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);

        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task Audit_WhenUrlIsInvalid_ReturnsOkWithFailureResult()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var request =
            new AuditRequestDto
            {
                Url = "not-a-valid-url"
            };

        var result =
            await controller.Audit(request);

        var okResult =
            Assert.IsType<OkObjectResult>(
                result);

        var response =
            Assert.IsType<AuditResponseDto>(
                okResult.Value);

        Assert.False(response.Success);

        Assert.Equal(
            "Please enter a valid HTTP or HTTPS URL.",
            response.Message);
    }

    [Fact]
    public async Task GetStatistics_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context);

        var result =
            await controller.GetStatistics();

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);

        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task GetStatistics_WhenUserIdIsInvalid_ReturnsUnauthorized()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context);

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        "invalid-user-id")
                },
                "TestAuthentication");

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext
                    {
                        User =
                            new ClaimsPrincipal(identity)
                    }
            };

        var result =
            await controller.GetStatistics();

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);

        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task GetStatistics_WithValidUserId_ReturnsStatistics()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetStatistics();

        var okResult =
            Assert.IsType<OkObjectResult>(
                result);

        var statistics =
            Assert.IsType<AuditStatisticsDto>(
                okResult.Value);

        Assert.Equal(0, statistics.TotalAudits);
        Assert.Equal(0, statistics.SuccessfulAudits);
        Assert.Equal(0, statistics.FailedAudits);
        Assert.Equal(0, statistics.AverageResponseTimeMs);
    }

    [Fact]
    public async Task Upgrade_WhenUserIdIsInvalid_ReturnsUnauthorized()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context);

        var request =
            new UpgradeSubscriptionRequest
            {
                Plan =
                    PlanDefinitions.Free
            };

        var result =
            await controller.Upgrade(request);

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);

        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task Upgrade_WhenPlanIsEmpty_ReturnsBadRequest()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var request =
            new UpgradeSubscriptionRequest
            {
                Plan = "   "
            };

        var result =
            await controller.Upgrade(request);

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(
                result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Upgrade_WhenPlanIsInvalid_ReturnsBadRequest()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var request =
            new UpgradeSubscriptionRequest
            {
                Plan = "InvalidPlan"
            };

        var result =
            await controller.Upgrade(request);

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(
                result);

        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Upgrade_WhenPaidPlanIsRequested_ReturnsForbidden()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var request =
            new UpgradeSubscriptionRequest
            {
                Plan =
                    PlanDefinitions.Professional
            };

        var result =
            await controller.Upgrade(request);

        var forbidden =
            Assert.IsType<ObjectResult>(
                result);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            forbidden.StatusCode);

        Assert.NotNull(forbidden.Value);
    }

    [Fact]
    public async Task Upgrade_FreePlan_WithValidUser_ReturnsOk()
    {
        using var context = CreateContext();

        var controller =
            CreateController(context, 1);

        var request =
            new UpgradeSubscriptionRequest
            {
                Plan =
                    PlanDefinitions.Free
            };

        var result =
            await controller.Upgrade(request);

        var okResult =
            Assert.IsType<OkObjectResult>(
                result);

        Assert.NotNull(okResult.Value);

        var subscription =
            await context.Subscriptions
                .FirstOrDefaultAsync(
                    x => x.UserId == 1);

        Assert.NotNull(subscription);

        Assert.Equal(
            PlanDefinitions.Free,
            subscription.Plan);

        Assert.True(
            subscription.IsActive);
    }
}