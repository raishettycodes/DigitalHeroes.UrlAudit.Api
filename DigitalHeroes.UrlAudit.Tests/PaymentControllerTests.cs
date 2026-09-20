using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DigitalHeroes.UrlAudit.Tests;

public class PaymentControllerTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UrlAuditDbContext(options);
    }

    private static PaymentController CreateController(
        UrlAuditDbContext context,
        int userId = 1)
    {
        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new RazorpayService(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.ToString())
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    private static PaymentController CreateControllerWithInvalidUser(
        UrlAuditDbContext context)
    {
        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new RazorpayService(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

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

        return controller;
    }

    [Fact]
    public async Task CreateOrder_WithEmptyPlan_ReturnsBadRequest()
    {
        await using var context = CreateContext();

        var controller = CreateController(context);

        var result = await controller.CreateOrder(
            new CreatePaymentOrderRequest
            {
                Plan = "   "
            });

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        Assert.Empty(
            await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task CreateOrder_WithInvalidPlan_ReturnsBadRequest()
    {
        await using var context = CreateContext();

        var controller = CreateController(context);

        var result = await controller.CreateOrder(
            new CreatePaymentOrderRequest
            {
                Plan = "InvalidPlan"
            });

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        Assert.Empty(
            await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task CreateOrder_WithFreePlan_ReturnsBadRequest()
    {
        await using var context = CreateContext();

        var controller = CreateController(context);

        var result = await controller.CreateOrder(
            new CreatePaymentOrderRequest
            {
                Plan = PlanDefinitions.Free
            });

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        Assert.Empty(
            await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task CreateOrder_WithInvalidUserIdentity_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateControllerWithInvalidUser(context);

        var result = await controller.CreateOrder(
            new CreatePaymentOrderRequest
            {
                Plan = PlanDefinitions.Starter
            });

        var unauthorized =
            Assert.IsType<UnauthorizedResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorized.StatusCode);

        Assert.Empty(
            await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task VerifyPayment_WithMissingDetails_ReturnsBadRequest()
    {
        await using var context = CreateContext();

        var controller = CreateController(context);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest());

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        Assert.Empty(
            await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task VerifyPayment_WithInvalidUserIdentity_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateControllerWithInvalidUser(context);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = "signature"
            });

        var unauthorized =
            Assert.IsType<UnauthorizedResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorized.StatusCode);
    }

    [Fact]
    public async Task VerifyPayment_WhenPaymentOrderDoesNotExist_ReturnsNotFound()
    {
        await using var context = CreateContext();

        var controller = CreateController(context, 1);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_missing",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = "signature"
            });

        var notFound =
            Assert.IsType<NotFoundObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status404NotFound,
            notFound.StatusCode);
    }

    [Fact]
    public async Task VerifyPayment_WhenPaymentAlreadyPaid_ReturnsConflict()
    {
        await using var context = CreateContext();

        context.Payments.Add(new Payment
        {
            UserId = 1,
            Plan = PlanDefinitions.Starter,
            Amount = 199,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = "order_test123",
            PaymentId = "pay_existing",
            Status = "Paid",
            PaidAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = "signature"
            });

        var conflict =
            Assert.IsType<ConflictObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            conflict.StatusCode);
    }

    [Fact]
    public async Task VerifyPayment_WhenPlanDoesNotMatchPayment_ReturnsBadRequest()
    {
        await using var context = CreateContext();

        context.Payments.Add(new Payment
        {
            UserId = 1,
            Plan = PlanDefinitions.Starter,
            Amount = 199,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = "order_test123",
            Status = "Created"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Professional,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = "signature"
            });

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync(x => x.OrderId == "order_test123");

        Assert.Equal("Created", payment.Status);
    }

    [Fact]
    public async Task VerifyPayment_WithInvalidSignature_MarksPaymentFailed()
    {
        await using var context = CreateContext();

        context.Payments.Add(new Payment
        {
            UserId = 1,
            Plan = PlanDefinitions.Starter,
            Amount = 199,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = "order_test123",
            Status = "Created"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = "invalid-signature"
            });

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync(x => x.OrderId == "order_test123");

        Assert.Equal("Failed", payment.Status);
    }
    [Fact]
    public async Task GetWebhookEvents_ReturnsOnlyCurrentUsersEvents()
    {
        await using var context = CreateContext();

        context.PaymentWebhookEvents.AddRange(
            new PaymentWebhookEvent
            {
                EventId = "evt_user1",
                EventType = "payment.captured",
                UserId = 1,
                RazorpayPaymentId = "pay_1",
                RazorpayOrderId = "order_1",
                Processed = true,
                ReceivedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new PaymentWebhookEvent
            {
                EventId = "evt_user2",
                EventType = "payment.captured",
                UserId = 2,
                RazorpayPaymentId = "pay_2",
                RazorpayOrderId = "order_2",
                Processed = true,
                ReceivedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetWebhookEvents();

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        var eventsProperty =
            okResult.Value.GetType().GetProperty("events");

        Assert.NotNull(eventsProperty);

        var events =
    Assert.IsAssignableFrom<System.Collections.IEnumerable>(
        eventsProperty!.GetValue(okResult.Value));

        Assert.Single(events.Cast<object>());
    }

    [Fact]
    public async Task GetWebhookEvents_ReturnsEventsOrderedByReceivedDateDescending()
    {
        await using var context = CreateContext();

        context.PaymentWebhookEvents.AddRange(
            new PaymentWebhookEvent
            {
                EventId = "evt_old",
                EventType = "payment.captured",
                UserId = 1,
                ReceivedAt = DateTime.UtcNow.AddHours(-2),
                Processed = true
            },
            new PaymentWebhookEvent
            {
                EventId = "evt_new",
                EventType = "payment.captured",
                UserId = 1,
                ReceivedAt = DateTime.UtcNow,
                Processed = true
            });

        await context.SaveChangesAsync();

        var controller = CreateController(context, 1);

        var result = await controller.GetWebhookEvents();

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        var eventsProperty =
            okResult.Value.GetType().GetProperty("events");

        Assert.NotNull(eventsProperty);

        var events =
            Assert.IsAssignableFrom<IEnumerable<object>>(
                eventsProperty!.GetValue(okResult.Value));

        var eventIds = events
             .Cast<object>()
             .Select(x =>
             x.GetType()
             .GetProperty("EventId")?
             .GetValue(x)?
             .ToString())
             .ToList();
        Assert.Equal(
         new[] { "evt_new", "evt_old" },
         eventIds);
    }

    [Fact]
    public async Task GetWebhookEvents_WithInvalidUserIdentity_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateControllerWithInvalidUser(context);

        var result = await controller.GetWebhookEvents();

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorized.StatusCode);
    }
    [Fact]
    public async Task Webhook_WithMissingHeaders_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        using var body = new MemoryStream(
            Encoding.UTF8.GetBytes("{}"));

        controller.ControllerContext.HttpContext.Request.Body = body;

        var result = await controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var payload = "{}";

        using var body = new MemoryStream(
            Encoding.UTF8.GetBytes(payload));

        controller.ControllerContext.HttpContext.Request.Body = body;

        controller.ControllerContext.HttpContext.Request.Headers[
            "X-Razorpay-Signature"] = "invalid-signature";

        controller.ControllerContext.HttpContext.Request.Headers[
            "x-razorpay-event-id"] = "evt_test123";

        var result = await controller.Webhook();

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            unauthorized.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithMissingEventId_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, 1);

        var payload = "{}";

        using var body = new MemoryStream(
            Encoding.UTF8.GetBytes(payload));

        controller.ControllerContext.HttpContext.Request.Body = body;

        controller.ControllerContext.HttpContext.Request.Headers[
            "X-Razorpay-Signature"] =
            CreateWebhookSignature(payload, "test-webhook-secret");

        var result = await controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);
    }
    private static string CreateWebhookSignature(
    string payload,
    string secret)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}