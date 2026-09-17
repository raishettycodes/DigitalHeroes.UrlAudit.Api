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

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly UrlAuditDbContext _context;
    private readonly RazorpayService _razorpayService;
    private readonly RazorpayOptions _razorpayOptions;

    public PaymentController(
        UrlAuditDbContext context,
        RazorpayService razorpayService,
        IOptions<RazorpayOptions> razorpayOptions)
    {
        _context = context;
        _razorpayService = razorpayService;
        _razorpayOptions = razorpayOptions.Value;
    }

    [HttpPost("create-order")]
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
        subscription.StartDate = DateTime.UtcNow;
        subscription.EndDate = null;
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

        // 2. Check idempotency.
        // 2. Check idempotency.
        var existingEvent =
            await _context.PaymentWebhookEvents
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

        // 3. Parse Razorpay webhook payload.
        using var document =
            JsonDocument.Parse(payload);

        var root = document.RootElement;

        var eventType =
            root.TryGetProperty("event", out var eventProperty)
                ? eventProperty.GetString() ?? "unknown"
                : "unknown";

        // 4. Record the webhook event immediately.
        var webhookEvent = new PaymentWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            ReceivedAt = DateTime.UtcNow,
            Processed = false
        };

        _context.PaymentWebhookEvents.Add(webhookEvent);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  (sqlException.Number == 2601 ||
                   sqlException.Number == 2627))
        {
            _context.Entry(webhookEvent).State = EntityState.Detached;

            webhookEvent =
                await _context.PaymentWebhookEvents
                    .FirstOrDefaultAsync(e =>
                        e.EventId == eventId);

            if (webhookEvent == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "Unable to resolve duplicate webhook event."
                    });
            }

            if (webhookEvent.Processed)
            {
                return Ok(new
                {
                    success = true,
                    message = "Webhook already processed."
                });
            }
        }

        // 5. Only process captured payments.
        if (eventType != "payment.captured")
        {
            webhookEvent.Processed = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Webhook received."
            });
        }

        // 6. Extract Razorpay payment details.
        if (!root.TryGetProperty("payload", out var payloadElement) ||
            !payloadElement.TryGetProperty("payment", out var paymentElement) ||
            !paymentElement.TryGetProperty("entity", out var entity))
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid payment webhook payload."
            });
        }

        var razorpayPaymentId =
            entity.TryGetProperty("id", out var paymentIdProperty)
                ? paymentIdProperty.GetString()
                : null;

        var razorpayOrderId =
            entity.TryGetProperty("order_id", out var orderIdProperty)
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

        // 7. Find our original payment record.
        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(p =>
                    p.OrderId == razorpayOrderId);

        if (payment == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Payment order not found."
            });
        }
        if (payment == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Payment order not found."
            });
        }

        webhookEvent.UserId = payment.UserId;
        webhookEvent.RazorpayPaymentId = razorpayPaymentId;
        webhookEvent.RazorpayOrderId = razorpayOrderId;

        await _context.SaveChangesAsync();

        // 8. Idempotency at payment level too.
        if (payment.Status == "Paid")
        {
            webhookEvent.Processed = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Payment already processed."
            });
        }

        // 9. Verify the payment with Razorpay.
        Razorpay.Api.Payment razorpayPayment;

        try
        {
            razorpayPayment =
                _razorpayService.FetchPayment(
                    razorpayPaymentId);
        }
        catch
        {
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

        // 10. Find the plan.
        var plan = PlanDefinitions.Plans.Values
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
    Convert.ToInt64(razorpayPayment["amount"]);

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

        // 11. Atomically record the payment,
        // activate the subscription, and mark the webhook processed.
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            payment.PaymentId =
                razorpayPaymentId;

            payment.Status = "Paid";

            payment.PaidAt =
                DateTime.UtcNow;

            // 12. Activate the subscription.
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

            subscription.Plan = plan.Name;
            subscription.MonthlyPrice = plan.MonthlyPrice;
            subscription.MonthlyAuditLimit =
                plan.MonthlyAuditLimit;
            subscription.StartDate =
                DateTime.UtcNow;
            subscription.EndDate = null;
            subscription.IsActive = true;
            subscription.Status = "Active";
            subscription.PaymentProvider = "Razorpay";
            subscription.ExternalSubscriptionId =
                razorpayPaymentId;
            subscription.UpdatedAt =
                DateTime.UtcNow;

            // 13. Mark webhook as processed.
            webhookEvent.Processed = true;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return Ok(new
        {
            success = true,
            message = "Payment webhook processed successfully."
        });
    }
}