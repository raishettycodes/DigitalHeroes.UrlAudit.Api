using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly UrlAuditDbContext _context;
    private readonly RazorpayService _razorpayService;
    private readonly RazorpayOptions _razorpayOptions;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        UrlAuditDbContext context,
        RazorpayService razorpayService,
        IOptions<RazorpayOptions> razorpayOptions,
         ILogger<PaymentController> logger)
    {
        _context = context;
        _razorpayService = razorpayService;
        _razorpayOptions = razorpayOptions.Value;
         _logger = logger;
    }

    [HttpPost("create-order")]
    [EnableRateLimiting("FixedPolicy")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreatePaymentOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Plan))
        {
            return BadRequest(new
            {
                message = "Plan is required."
            });
        }

        var plan = PlanDefinitions.Plans.Values
            .FirstOrDefault(p =>
                string.Equals(
                    p.Name,
                    request.Plan.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (plan == null)
        {
            return BadRequest(new
            {
                message = "Invalid subscription plan."
            });
        }

        if (plan.MonthlyPrice <= 0)
        {
            return BadRequest(new
            {
                message = "A payment is not required for this plan."
            });
        }

        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var receipt =
            $"dh_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        var order = _razorpayService.CreateOrder(
            plan.MonthlyPrice,
            receipt);

        var payment = new Payment
        {
            UserId = userId,
            Plan = plan.Name,
            Amount = plan.MonthlyPrice,
            Currency = "INR",
            Provider = "Razorpay",
            OrderId = order.OrderId,
            Status = "Created"
        };

        _context.Payments.Add(payment);

        await _context.SaveChangesAsync();

        return Ok(new CreatePaymentOrderResponse
        {
            KeyId = _razorpayOptions.KeyId,
            OrderId = order.OrderId,
            Plan = plan.Name,
            Amount = plan.MonthlyPrice,
            Currency = "INR"
        });
    }

    [HttpPost("verify")]
    [EnableRateLimiting("FixedPolicy")]
    public async Task<IActionResult> VerifyPayment(
        [FromBody] VerifyPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Plan) ||
            string.IsNullOrWhiteSpace(request.RazorpayOrderId) ||
            string.IsNullOrWhiteSpace(request.RazorpayPaymentId) ||
            string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            return BadRequest(new
            {
                message = "Payment verification details are required."
            });
        }

        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.OrderId == request.RazorpayOrderId);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment order not found."
            });
        }

        if (payment.Status == "Paid")
        {
            return Conflict(new
            {
                message = "Payment has already been verified."
            });
        }

        if (!string.Equals(
                payment.Plan,
                request.Plan.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Payment plan does not match the order."
            });
        }

        // 1. Verify the Razorpay signature.
        var isValidSignature =
            _razorpayService.VerifyPaymentSignature(
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature);

        if (!isValidSignature)
        {
            payment.Status = "Failed";

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = "Invalid payment signature."
            });
        }

        // 2. Fetch the payment directly from Razorpay.
        Razorpay.Api.Payment razorpayPayment;

        try
        {
            razorpayPayment =
                _razorpayService.FetchPayment(
                    request.RazorpayPaymentId);
        }
        catch
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message =
                        "Unable to verify payment status with Razorpay."
                });
        }

        // 3. Verify the payment ID.
        var razorpayPaymentId =
            razorpayPayment["id"]?.ToString();

        if (!string.Equals(
                razorpayPaymentId,
                request.RazorpayPaymentId,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Payment ID does not match Razorpay."
            });
        }

        // 4. Verify the order ID.
        var razorpayOrderId =
            razorpayPayment["order_id"]?.ToString();

        if (!string.Equals(
                razorpayOrderId,
                request.RazorpayOrderId,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Payment order does not match Razorpay."
            });
        }

        // 5. Verify the currency.
        var razorpayCurrency =
            razorpayPayment["currency"]?.ToString();

        if (!string.Equals(
                razorpayCurrency,
                payment.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Payment currency does not match the order."
            });
        }

        // 6. Verify the amount.
        var razorpayAmount =
            Convert.ToInt64(razorpayPayment["amount"]);

        var expectedAmountInPaise =
            (long)Math.Round(
                payment.Amount * 100,
                MidpointRounding.AwayFromZero);

        if (razorpayAmount != expectedAmountInPaise)
        {
            return BadRequest(new
            {
                message = "Payment amount does not match the order."
            });
        }

        // 7. Verify that Razorpay considers the payment captured.
        var razorpayStatus =
            razorpayPayment["status"]?.ToString();

        var captured =
            Convert.ToBoolean(razorpayPayment["captured"]);

        if (!captured ||
            !string.Equals(
                razorpayStatus,
                "captured",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Payment has not been captured."
            });
        }

        // 8. Find the requested subscription plan.
        var plan = PlanDefinitions.Plans.Values
            .FirstOrDefault(p =>
                string.Equals(
                    p.Name,
                    request.Plan.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (plan == null)
        {
            return BadRequest(new
            {
                message = "Invalid subscription plan."
            });
        }

        // 9. Record the verified payment.
        payment.PaymentId =
            request.RazorpayPaymentId;

        payment.Signature =
            request.RazorpaySignature;

        payment.Status = "Paid";

        payment.PaidAt =
            DateTime.UtcNow;

        // 10. Activate the subscription.
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s =>
                s.UserId == userId);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                UserId = userId
            };

            _context.Subscriptions.Add(subscription);
        }

        subscription.Plan = plan.Name;
        subscription.MonthlyPrice = plan.MonthlyPrice;
        subscription.MonthlyAuditLimit = plan.MonthlyAuditLimit;
        var subscriptionStart = DateTime.UtcNow;

        subscription.StartDate = subscriptionStart;
        subscription.EndDate = subscriptionStart.AddDays(30);
        subscription.IsActive = true;
        subscription.Status = "Active";
        subscription.PaymentProvider = "Razorpay";
        subscription.ExternalSubscriptionId =
            request.RazorpayPaymentId;
        subscription.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message =
                "Payment verified and subscription activated.",
            plan = subscription.Plan,
            paymentId = payment.PaymentId
        });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature =
            Request.Headers["X-Razorpay-Signature"].FirstOrDefault();

        var eventId =
            Request.Headers["x-razorpay-event-id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(eventId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Missing Razorpay webhook headers."
            });
        }

        // 1. Verify Razorpay webhook signature.
        if (!_razorpayService.VerifyWebhookSignature(
                payload,
                signature,
                _razorpayOptions.WebhookSecret))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid webhook signature."
            });
        }

        // 2. Parse Razorpay webhook payload.
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;

        var eventType =
            root.TryGetProperty("event", out var eventProperty)
                ? eventProperty.GetString() ?? "unknown"
                : "unknown";

        _logger.LogInformation(
            "Razorpay webhook received. EventId={EventId}, EventType={EventType}",
            eventId,
            eventType);

        // 3. Fast idempotency check.
        //
        // The authoritative check is repeated inside the
        // Serializable transaction below.
        var existingEvent =
            await _context.PaymentWebhookEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.EventId == eventId);

        if (existingEvent?.Processed == true)
        {
            return Ok(new
            {
                success = true,
                message = "Webhook already processed."
            });
        }

        // 4. Only payment.captured requires payment processing.
        if (!string.Equals(
                eventType,
                "payment.captured",
                StringComparison.OrdinalIgnoreCase))
        {
            await using var nonPaymentTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var nonPaymentEvent =
                    await _context.PaymentWebhookEvents
                        .FirstOrDefaultAsync(e =>
                            e.EventId == eventId);

                if (nonPaymentEvent == null)
                {
                    nonPaymentEvent = new PaymentWebhookEvent
                    {
                        EventId = eventId,
                        EventType = eventType,
                        ReceivedAt = DateTime.UtcNow,
                        Processed = true
                    };

                    _context.PaymentWebhookEvents.Add(
                        nonPaymentEvent);

                    await _context.SaveChangesAsync();
                }
                else if (!nonPaymentEvent.Processed)
                {
                    nonPaymentEvent.Processed = true;

                    await _context.SaveChangesAsync();
                }

                await nonPaymentTransaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Webhook received."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Razorpay non-payment webhook failed. EventId={EventId}",
                    eventId);

                await nonPaymentTransaction.RollbackAsync();
                throw;
            }
        }

        // 5. Extract Razorpay payment details.
        if (!root.TryGetProperty(
                "payload",
                out var payloadElement) ||
            !payloadElement.TryGetProperty(
                "payment",
                out var paymentElement) ||
            !paymentElement.TryGetProperty(
                "entity",
                out var entity))
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid payment webhook payload."
            });
        }

        var razorpayPaymentId =
            entity.TryGetProperty(
                "id",
                out var paymentIdProperty)
                    ? paymentIdProperty.GetString()
                    : null;

        var razorpayOrderId =
            entity.TryGetProperty(
                "order_id",
                out var orderIdProperty)
                    ? orderIdProperty.GetString()
                    : null;

        if (string.IsNullOrWhiteSpace(razorpayPaymentId) ||
            string.IsNullOrWhiteSpace(razorpayOrderId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Payment ID or Order ID is missing."
            });
        }

        // 6. Find our original payment record.
        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(p =>
                    p.OrderId == razorpayOrderId);

        _logger.LogInformation(
            "Webhook payment lookup. EventId={EventId}, OrderId={OrderId}, PaymentFound={PaymentFound}, PaymentStatus={PaymentStatus}, Plan={Plan}",
            eventId,
            razorpayOrderId,
            payment != null,
            payment?.Status,
            payment?.Plan);

        if (payment == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Payment order not found."
            });
        }

        // 7. Verify the payment directly with Razorpay.
        Razorpay.Api.Payment razorpayPayment;

        try
        {
            razorpayPayment =
                _razorpayService.FetchPayment(
                    razorpayPaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to fetch Razorpay payment. EventId={EventId}, PaymentId={PaymentId}",
                eventId,
                razorpayPaymentId);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    success = false,
                    message =
                        "Unable to verify payment with Razorpay."
                });
        }

        var fetchedOrderId =
            razorpayPayment["order_id"]?.ToString();

        var fetchedStatus =
            razorpayPayment["status"]?.ToString();

        var fetchedCurrency =
            razorpayPayment["currency"]?.ToString();

        if (!string.Equals(
                fetchedOrderId,
                payment.OrderId,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                success = false,
                message = "Payment order mismatch."
            });
        }

        if (!string.Equals(
                fetchedCurrency,
                payment.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                success = false,
                message = "Payment currency mismatch."
            });
        }

        if (!string.Equals(
                fetchedStatus,
                "captured",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                success = false,
                message = "Payment is not captured."
            });
        }

        // 8. Find the plan.
        var plan =
            PlanDefinitions.Plans.Values
                .FirstOrDefault(p =>
                    string.Equals(
                        p.Name,
                        payment.Plan,
                        StringComparison.OrdinalIgnoreCase));

        if (plan == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid subscription plan."
            });
        }

        var fetchedAmount =
            Convert.ToInt64(
                razorpayPayment["amount"]);

        var expectedAmountInPaise =
            (long)Math.Round(
                payment.Amount * 100,
                MidpointRounding.AwayFromZero);

        if (fetchedAmount != expectedAmountInPaise)
        {
            return BadRequest(new
            {
                success = false,
                message = "Payment amount mismatch."
            });
        }

        // 9. Atomically:
        //    - resolve the webhook event
        //    - reload the payment
        //    - record payment
        //    - activate subscription
        //    - mark webhook processed
        //
        // Serializable prevents two concurrent requests with
        // the same EventId from both inserting the event.
        _logger.LogInformation(
            "Webhook entering payment transaction. EventId={EventId}, PaymentId={PaymentId}, PaymentStatus={PaymentStatus}, Plan={Plan}",
            eventId,
            razorpayPaymentId,
            payment.Status,
            payment.Plan);

        var executionStrategy =
     _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                // 10. Re-check the webhook event inside the transaction.
                var webhookEvent =
                    await _context.PaymentWebhookEvents
                        .FirstOrDefaultAsync(e =>
                            e.EventId == eventId);

                if (webhookEvent?.Processed == true)
                {
                    await transaction.CommitAsync();
                    return;
                }

                // Create the event only if it doesn't already exist.
                if (webhookEvent == null)
                {
                    webhookEvent = new PaymentWebhookEvent
                    {
                        EventId = eventId,
                        EventType = eventType,
                        ReceivedAt = DateTime.UtcNow,
                        Processed = false
                    };

                    _context.PaymentWebhookEvents.Add(webhookEvent);

                    _logger.LogInformation(
                        "Webhook event entity added. EventId={EventId}, EntityState={EntityState}",
                        eventId,
                        _context.Entry(webhookEvent).State);
                }

                webhookEvent.EventType = eventType;
                webhookEvent.RazorpayPaymentId =
                    razorpayPaymentId;
                webhookEvent.RazorpayOrderId =
                    razorpayOrderId;
                webhookEvent.UserId =
                    payment.UserId;

                // 11. Reload the payment while holding the transaction.
                await _context.Entry(payment).ReloadAsync();

                // Another webhook request may have completed
                // the payment while this request was waiting.
                if (payment.Status == "Paid")
                {
                    webhookEvent.Processed = true;

                    _logger.LogInformation(
                        "Webhook SaveChanges starting. EventId={EventId}, WebhookEventState={WebhookEventState}, PaymentState={PaymentState}",
                        eventId,
                        _context.Entry(webhookEvent).State,
                        _context.Entry(payment).State);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return;
                }

                payment.PaymentId =
                    razorpayPaymentId;

                payment.Status = "Paid";

                payment.PaidAt =
                    DateTime.UtcNow;

                // 13. Activate or update subscription.
                var subscription =
                    await _context.Subscriptions
                        .FirstOrDefaultAsync(s =>
                            s.UserId == payment.UserId);

                if (subscription == null)
                {
                    subscription = new Subscription
                    {
                        UserId = payment.UserId
                    };

                    _context.Subscriptions.Add(subscription);
                }

                subscription.Plan =
                    plan.Name;

                subscription.MonthlyPrice =
                    plan.MonthlyPrice;

                subscription.MonthlyAuditLimit =
                    plan.MonthlyAuditLimit;

                var subscriptionStart = DateTime.UtcNow;

                subscription.StartDate = subscriptionStart;
                subscription.EndDate = subscriptionStart.AddDays(30);
                subscription.IsActive = true;
                subscription.Status = "Active";
                subscription.PaymentProvider =
                    "Razorpay";

                subscription.ExternalSubscriptionId =
                    razorpayPaymentId;

                subscription.UpdatedAt =
                    DateTime.UtcNow;

                // 14. Mark webhook processed.
                webhookEvent.Processed = true;

                _logger.LogInformation(
                    "Webhook SaveChanges starting. EventId={EventId}, WebhookEventState={WebhookEventState}, PaymentState={PaymentState}",
                    eventId,
                    _context.Entry(webhookEvent).State,
                    _context.Entry(payment).State);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "WEBHOOK SAVE FAILED. EventId={EventId}, PaymentId={PaymentId}, OrderId={OrderId}, PaymentState={PaymentState}",
                    eventId,
                    razorpayPaymentId,
                    razorpayOrderId,
                    _context.Entry(payment).State);

                await transaction.RollbackAsync();
                throw;
            }
        });
        return Ok(new
        {
            success = true,
            message = "Payment webhook processed successfully."
        });
    }

    [HttpGet("webhook-events")]
    public async Task<IActionResult> GetWebhookEvents()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid user identity."
            });
        }

        var events = await _context.PaymentWebhookEvents
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.ReceivedAt)
            .Take(20)
            .Select(e => new
            {
                e.EventId,
                e.EventType,
                e.RazorpayPaymentId,
                e.RazorpayOrderId,
                e.ReceivedAt,
                e.Processed
            })
            .ToListAsync();


        return Ok(new
        {
            success = true,
            events
        });
    }
}