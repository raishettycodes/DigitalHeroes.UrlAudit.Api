using System.Security.Claims;
using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        var isValid = _razorpayService.VerifyPaymentSignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            request.RazorpaySignature);

        if (!isValid)
        {
            payment.Status = "Failed";
            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = "Invalid payment signature."
            });
        }

        payment.PaymentId = request.RazorpayPaymentId;
        payment.Signature = request.RazorpaySignature;
        payment.Status = "Paid";
        payment.PaidAt = DateTime.UtcNow;

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                UserId = userId
            };

            _context.Subscriptions.Add(subscription);
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
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Payment verified and subscription activated.",
            plan = subscription.Plan,
            paymentId = payment.PaymentId
        });
    }
}