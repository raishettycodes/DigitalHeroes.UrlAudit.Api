using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly UrlAuditDbContext _context;

    public SubscriptionController(UrlAuditDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET PLANS
    // =========================================================

    [HttpGet("plans")]
    [AllowAnonymous]
    public IActionResult GetPlans()
    {
        var plans = PlanDefinitions.Plans.Values
            .Select(plan => new
            {
                name = plan.Name,
                monthlyPrice = plan.MonthlyPrice,
                monthlyAuditLimit = plan.MonthlyAuditLimit
            })
            .ToList();

        return Ok(plans);
    }


    // =========================================================
    // GET CURRENT SUBSCRIPTION USAGE
    // =========================================================

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid user identity."
            });
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription == null)
        {
            var freePlan =
                PlanDefinitions.Plans[PlanDefinitions.Free];

            subscription = new Subscription
            {
                UserId = userId,
                Plan = freePlan.Name,
                MonthlyAuditLimit = freePlan.MonthlyAuditLimit,
                MonthlyPrice = freePlan.MonthlyPrice,
                StartDate = DateTime.UtcNow,
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);

            await _context.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var nextMonth = monthStart.AddMonths(1);

        // Count only audits belonging to this user
        var auditsUsed = await (
            from audit in _context.AuditHistories
            join website in _context.Websites
                on audit.WebsiteId equals website.Id
            where website.UserId == userId &&
                  audit.CreatedAt >= monthStart &&
                  audit.CreatedAt < nextMonth
            select audit
        ).CountAsync();

        var isUnlimited =
            subscription.MonthlyAuditLimit == -1;

        int remainingAudits;
        double usagePercentage;

        if (isUnlimited)
        {
            remainingAudits = -1;
            usagePercentage = 0;
        }
        else
        {
            remainingAudits = Math.Max(
                0,
                subscription.MonthlyAuditLimit - auditsUsed);

            usagePercentage =
                subscription.MonthlyAuditLimit > 0
                    ? Math.Min(
                        100,
                        (double)auditsUsed /
                        subscription.MonthlyAuditLimit * 100)
                    : 100;
        }

        var result = new SubscriptionDto
        {
            Plan = subscription.Plan,
            MonthlyPrice = subscription.MonthlyPrice,
            MonthlyAuditLimit = subscription.MonthlyAuditLimit,
            AuditsUsed = auditsUsed,
            RemainingAudits = remainingAudits,
            UsagePercentage = Math.Round(usagePercentage, 2),
            IsUnlimited = isUnlimited,
            Status = subscription.Status,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            IsActive = subscription.IsActive
        };

        return Ok(result);
    }


    // =========================================================
    // UPGRADE / CHANGE PLAN
    // =========================================================

    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade(
        [FromBody] UpgradeSubscriptionRequest request)
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid user identity."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Plan))
        {
            return BadRequest(new
            {
                success = false,
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
                success = false,
                message = "Invalid subscription plan."
            });
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
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
        subscription.UpdatedAt = DateTime.UtcNow;

        if (subscription.CreatedAt == default)
        {
            subscription.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Get fresh data from database
        _context.Entry(subscription).State =
            EntityState.Detached;

        var updatedSubscription =
            await _context.Subscriptions
                .FirstAsync(s => s.UserId == userId);

        var now = DateTime.UtcNow;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var nextMonth = monthStart.AddMonths(1);

        var auditsUsed = await (
            from audit in _context.AuditHistories
            join website in _context.Websites
                on audit.WebsiteId equals website.Id
            where website.UserId == userId &&
                  audit.CreatedAt >= monthStart &&
                  audit.CreatedAt < nextMonth
            select audit
        ).CountAsync();

        var isUnlimited =
            updatedSubscription.MonthlyAuditLimit == -1;

        var remainingAudits = isUnlimited
            ? -1
            : Math.Max(
                0,
                updatedSubscription.MonthlyAuditLimit - auditsUsed);

        var usagePercentage = isUnlimited
            ? 0
            : updatedSubscription.MonthlyAuditLimit > 0
                ? Math.Min(
                    100,
                    (double)auditsUsed /
                    updatedSubscription.MonthlyAuditLimit * 100)
                : 100;

        return Ok(new
        {
            success = true,
            message =
                $"Subscription changed to {updatedSubscription.Plan}.",
            plan = updatedSubscription.Plan,
            monthlyPrice = updatedSubscription.MonthlyPrice,
            monthlyAuditLimit =
                updatedSubscription.MonthlyAuditLimit,
            auditsUsed,
            remainingAudits,
            usagePercentage =
                Math.Round(usagePercentage, 2),
            isUnlimited,
            status = updatedSubscription.Status,
            startDate = updatedSubscription.StartDate,
            endDate = updatedSubscription.EndDate,
            isActive = updatedSubscription.IsActive
        });
    }
}