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
using Moq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;


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

    private static PaymentController CreateControllerWithMissingRazorpayKey(
    UrlAuditDbContext context,
    int userId = 1)
    {
        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "",
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
    public async Task VerifyPayment_WhenRazorpayConfigurationIsMissing_ReturnsBadGateway()
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

        var controller =
            CreateControllerWithMissingRazorpayKey(context, 1);

        var validSignature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = validSignature
            });

        var objectResult =
            Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status502BadGateway,
            objectResult.StatusCode);

        var payment = await context.Payments
            .SingleAsync(x => x.OrderId == "order_test123");

        Assert.Equal("Created", payment.Status);
    }

    [Fact]
    public async Task VerifyPayment_WhenRazorpayOrderIdDoesNotMatch_ReturnsBadRequest()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_different");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
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
    public async Task VerifyPayment_WhenCurrencyDoesNotMatch_ReturnsBadRequest()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                currency: "USD");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
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
    public async Task VerifyPayment_WhenAmountDoesNotMatch_ReturnsBadRequest()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                amountInPaise: 29900);

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
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
    public async Task VerifyPayment_WhenPaymentIsNotCaptured_ReturnsBadRequest()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                status: "authorized");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
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

    [Fact]
    public async Task VerifyPayment_WhenRazorpayPaymentIdDoesNotMatch_ReturnsBadRequest()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_different",
                "order_test123");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
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
    public async Task Webhook_WithValidNonPaymentEvent_ReturnsOkAndRecordsEvent()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "order.paid",
            "payload": {}
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_test123";

        var result = await controller.Webhook();

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status200OK,
            okResult.StatusCode);

        var webhookEvent = await context.PaymentWebhookEvents
    .SingleAsync();

        Assert.Equal(
            "evt_test123",
            webhookEvent.EventId);

        Assert.Equal(
            "order.paid",
            webhookEvent.EventType);

        Assert.True(webhookEvent.Processed);
    }

    private static async Task<(UrlAuditDbContext Context, SqliteConnection Connection)> CreateSqliteContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new UrlAuditDbContext(options);

        await context.Database.EnsureCreatedAsync();

        return (context, connection);
    }

    [Fact]
    public async Task Webhook_WhenEventAlreadyProcessed_ReturnsOkWithoutProcessingAgain()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        context.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            EventId = "evt_duplicate123",
            EventType = "order.paid",
            Processed = true,            
        });

        await context.SaveChangesAsync();

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "order.paid",
            "payload": {}
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_duplicate123";

        var result = await controller.Webhook();

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status200OK,
            okResult.StatusCode);

        var events = await context.PaymentWebhookEvents
            .Where(x => x.EventId == "evt_duplicate123")
            .ToListAsync();

        Assert.Single(events);
        Assert.True(events[0].Processed);

        razorpayService.Verify(
            x => x.FetchPayment(It.IsAny<string>()),
            Times.Never);
    }


    [Fact]
    public async Task VerifyPayment_WithValidCapturedPayment_MarksPaymentPaidAndCreatesSubscription()
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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var signature =
            CreatePaymentSignature(
                "order_test123",
                "pay_test123",
                "test-key-secret");

        var result = await controller.VerifyPayment(
            new VerifyPaymentRequest
            {
                Plan = PlanDefinitions.Starter,
                RazorpayOrderId = "order_test123",
                RazorpayPaymentId = "pay_test123",
                RazorpaySignature = signature
            });

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status200OK,
            okResult.StatusCode);

        var payment = await context.Payments
            .SingleAsync(x => x.OrderId == "order_test123");

        Assert.Equal("Paid", payment.Status);
        Assert.Equal("pay_test123", payment.PaymentId);
        Assert.Equal("Razorpay", payment.Provider);
        Assert.NotNull(payment.PaidAt);

        var subscription = await context.Subscriptions
            .SingleAsync(x => x.UserId == 1);

        Assert.Equal(
            PlanDefinitions.Starter,
            subscription.Plan);

        Assert.Equal(
            PlanDefinitions.Plans[PlanDefinitions.Starter].MonthlyAuditLimit,
            subscription.MonthlyAuditLimit);

        Assert.Equal(
            "Active",
            subscription.Status);

        Assert.Equal(
            "pay_test123",
            subscription.ExternalSubscriptionId);
    }
    private static string CreatePaymentSignature(
    string orderId,
    string paymentId,
    string secret)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(
                $"{orderId}|{paymentId}"));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    [Fact]
    public async Task Webhook_PaymentCapturedWithMissingPayload_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured"
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_missing_payload";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        razorpayService.Verify(
            x => x.FetchPayment(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_PaymentCapturedWithMissingPaymentOrOrderId_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {}
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_missing_ids";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        razorpayService.Verify(
            x => x.FetchPayment(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_PaymentCaptured_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_payment_not_found";

        var result = await controller.Webhook();

        var notFound =
            Assert.IsType<NotFoundObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status404NotFound,
            notFound.StatusCode);

        razorpayService.Verify(
            x => x.FetchPayment(It.IsAny<string>()),
            Times.Never);
    }
    [Fact]
    public async Task Webhook_PaymentCaptured_WhenRazorpayConfigurationIsMissing_ReturnsBadGateway()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "",
                KeySecret = "",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new RazorpayService(razorpayOptions);

        var controller = new PaymentController(
            context,
            razorpayService,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_fetch_config_error";

        var result = await controller.Webhook();

        var objectResult =
            Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status502BadGateway,
            objectResult.StatusCode);
    }
    [Fact]
    public async Task Webhook_PaymentCaptured_WhenRazorpayOrderIdDoesNotMatch_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_different");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_order_mismatch";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync();

        Assert.Equal("Created", payment.Status);
    }

    [Fact]
    public async Task Webhook_PaymentCaptured_WhenCurrencyDoesNotMatch_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                19900,
                "USD");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_currency_mismatch";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync();

        Assert.Equal("Created", payment.Status);
    }

    [Fact]
    public async Task Webhook_PaymentCaptured_WhenRazorpayPaymentIsNotCaptured_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                19900,
                "INR",
                "authorized");

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_not_captured";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync();

        Assert.Equal("Created", payment.Status);
    }

    [Fact]
    public async Task Webhook_PaymentCaptured_WhenAmountDoesNotMatch_ReturnsBadRequest()
    {
        var (context, connection) = await CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

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

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayService =
            new Mock<RazorpayService>(razorpayOptions);

        var fakePayment =
            CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                29900);

        razorpayService
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(fakePayment);

        var controller = new PaymentController(
            context,
            razorpayService.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        const string payload =
            """
        {
            "event": "payment.captured",
            "payload": {
                "payment": {
                    "entity": {
                        "id": "pay_test123",
                        "order_id": "order_test123"
                    }
                }
            }
        }
        """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
            {
                Body = new MemoryStream(
                    Encoding.UTF8.GetBytes(payload))
            }
            }
        };

        controller.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_amount_mismatch";

        var result = await controller.Webhook();

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            badRequest.StatusCode);

        var payment = await context.Payments
            .SingleAsync();

        Assert.Equal("Created", payment.Status);
    }
    [Fact]
    public async Task Webhook_PaymentCaptured_WhenPlanIsInvalid_ReturnsBadRequest()
    {
        var setup = await CreateSqliteContext();
        await using var context = setup.Context;
        await using var connection = setup.Connection;

        context.Payments.Add(new Payment
        {
            UserId = 1,
            Plan = "InvalidPlan",
            Amount = 199,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = "order_test123",
            Status = "Created"
        });

        await context.SaveChangesAsync();

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayServiceMock = new Mock<RazorpayService>(razorpayOptions);

        razorpayServiceMock
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                19900,
                "INR",
                "captured"));

        var controller = new PaymentController(
            context,
            razorpayServiceMock.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
            new Claim(
                ClaimTypes.NameIdentifier,
                "1")
            },
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var payload = """
    {
        "event": "payment.captured",
        "payload": {
            "payment": {
                "entity": {
                    "id": "pay_test123",
                    "order_id": "order_test123"
                }
            }
        }
    }
    """;

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext.HttpContext.Request.Body =
            new MemoryStream(Encoding.UTF8.GetBytes(payload));

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_invalid_plan";

        var result = await controller.Webhook();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);

        var payment = await context.Payments.SingleAsync();

        Assert.Equal("Created", payment.Status);
    }
    [Fact]
    public async Task Webhook_PaymentCaptured_WithValidPayment_MarksPaymentPaidAndCreatesSubscription()
    {
        var setup = await CreateSqliteContext();
        await using var context = setup.Context;
        await using var connection = setup.Connection;

        context.Users.Add(new User
        {
            Id = 1,
            FullName = "Test User",
            Email = "test@example.com",
            PasswordHash = "test-password"
        });

        await context.SaveChangesAsync();

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
        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayServiceMock = new Mock<RazorpayService>(razorpayOptions);

        razorpayServiceMock
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                19900,
                "INR",
                "captured"));

        var controller = new PaymentController(
            context,
            razorpayServiceMock.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var payload = """
    {
        "event": "payment.captured",
        "payload": {
            "payment": {
                "entity": {
                    "id": "pay_test123",
                    "order_id": "order_test123"
                }
            }
        }
    }
    """;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext.HttpContext.Request.Body =
            new MemoryStream(Encoding.UTF8.GetBytes(payload));

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_payment_success";

        var result = await controller.Webhook();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var payment = await context.Payments.SingleAsync();

        Assert.Equal("Paid", payment.Status);
        Assert.Equal("pay_test123", payment.PaymentId);
        Assert.NotNull(payment.PaidAt);

        var subscription = await context.Subscriptions.SingleAsync();

        Assert.Equal(1, subscription.UserId);
        Assert.Equal(PlanDefinitions.Starter, subscription.Plan);
        Assert.Equal(
            PlanDefinitions.Plans[PlanDefinitions.Starter].MonthlyAuditLimit,
            subscription.MonthlyAuditLimit);
        Assert.Equal("Active", subscription.Status);
        Assert.Equal("pay_test123", subscription.ExternalSubscriptionId);

        var webhookEvent = await context.PaymentWebhookEvents.SingleAsync();

        Assert.Equal("evt_payment_success", webhookEvent.EventId);
        Assert.Equal("payment.captured", webhookEvent.EventType);
        Assert.True(webhookEvent.Processed);
    }
    [Fact]
    public async Task Webhook_PaymentCaptured_WhenPaymentAlreadyPaid_ReturnsOkWithoutCreatingDuplicateSubscription()
    {
        var setup = await CreateSqliteContext();
        await using var context = setup.Context;
        await using var connection = setup.Connection;

        context.Users.Add(new User
        {
            Id = 1,
            FullName = "Test User",
            Email = "test@example.com",
            PasswordHash = "test-password"
        });

        context.Payments.Add(new Payment
        {
            UserId = 1,
            Plan = PlanDefinitions.Starter,
            Amount = 199,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = "order_test123",
            PaymentId = "pay_test123",
            Status = "Paid",
            PaidAt = DateTime.UtcNow
        });

        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = PlanDefinitions.Starter,
            MonthlyAuditLimit =
                PlanDefinitions.Plans[PlanDefinitions.Starter].MonthlyAuditLimit,
            Status = "Active",
            ExternalSubscriptionId = "pay_test123"
        });

        await context.SaveChangesAsync();

        var razorpayOptions = Options.Create(
            new RazorpayOptions
            {
                KeyId = "test-key-id",
                KeySecret = "test-key-secret",
                WebhookSecret = "test-webhook-secret"
            });

        var razorpayServiceMock = new Mock<RazorpayService>(razorpayOptions);

        razorpayServiceMock
            .Setup(x => x.FetchPayment("pay_test123"))
            .Returns(CreateFakeRazorpayPayment(
                "pay_test123",
                "order_test123",
                19900,
                "INR",
                "captured"));

        var controller = new PaymentController(
            context,
            razorpayServiceMock.Object,
            razorpayOptions,
            NullLogger<PaymentController>.Instance);

        var payload = """
    {
        "event": "payment.captured",
        "payload": {
            "payment": {
                "entity": {
                    "id": "pay_test123",
                    "order_id": "order_test123"
                }
            }
        }
    }
    """;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var signature = CreateWebhookSignature(
            payload,
            "test-webhook-secret");

        controller.ControllerContext.HttpContext.Request.Body =
            new MemoryStream(Encoding.UTF8.GetBytes(payload));

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Signature"] =
            signature;

        controller.ControllerContext.HttpContext.Request.Headers["X-Razorpay-Event-Id"] =
            "evt_already_paid";

        var result = await controller.Webhook();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var payments = await context.Payments.ToListAsync();
        Assert.Single(payments);

        var subscriptions = await context.Subscriptions.ToListAsync();
        Assert.Single(subscriptions);

        var webhookEvent = await context.PaymentWebhookEvents.SingleAsync();

        Assert.Equal("evt_already_paid", webhookEvent.EventId);
        Assert.Equal("payment.captured", webhookEvent.EventType);
        Assert.True(webhookEvent.Processed);
    }


    private static Razorpay.Api.Payment CreateFakeRazorpayPayment(
    string paymentId,
    string orderId,
    int amountInPaise = 19900,
    string currency = "INR",
    string status = "captured")
    {
        var payment = new Razorpay.Api.Payment();

        payment["id"] = paymentId;
        payment["order_id"] = orderId;
        payment["amount"] = amountInPaise;
        payment["currency"] = currency;
        payment["status"] = status;
        payment["captured"] = status == "captured";

        return payment;
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