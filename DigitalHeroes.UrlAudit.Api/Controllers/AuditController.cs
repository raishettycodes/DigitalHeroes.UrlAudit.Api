using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DigitalHeroes.UrlAudit.Api.Interfaces;

namespace DigitalHeroes.UrlAudit.Api.Controllers
{
    /// <summary>
    /// API used to audit website URLs.
    /// </summary>
    [Authorize]
    [EnableRateLimiting("FixedPolicy")]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly AuditService _auditService;
      //  private readonly UrlAuditDbContext _context;

        public AuditController(
            AuditService auditService,
            UrlAuditDbContext context)
        {
            _auditService = auditService;
           // _context = context;
        }

        /// <summary>
        /// Audits a URL and returns availability,
        /// response time, HTTP status code,
        /// and reachability.
        /// </summary>
        [HttpPost]
[ProducesResponseType(
    typeof(AuditResponseDto),
    StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> Audit(
    [FromBody] AuditRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // ==========================================
            // READ USER ID FROM JWT
            // ==========================================

            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "User ID not found in token."
                });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid User ID."
                });
            }


            // ==========================================
            // RUN AUDIT
            //
            // AuditService is responsible for:
            // - Subscription lookup
            // - Monthly usage calculation
            // - Plan limit enforcement
            // - Cache handling
            // ==========================================

            var result =
                await _auditService.AuditUrlAsync(
                    request.Url,
                    userId);

            //// ==========================================
            //// CREATE NOTIFICATION
            //// ==========================================

            //if (result.Success)
            //{
            //    var notification = new Notification
            //    {
            //        UserId = userId.ToString(),
            //        Title = "Audit completed successfully",
            //        Message = $"Your website audit for {request.Url} was completed successfully.",
            //        Type = "Success",
            //        IsRead = false,
            //        CreatedAt = DateTime.UtcNow
            //    };

            //    _context.Notifications.Add(notification);

            //    await _context.SaveChangesAsync();
            //}


            // ==========================================
            // MONTHLY LIMIT REACHED
            // ==========================================

            if (!result.Success &&
                result.Message.Contains(
                    "Monthly audit limit",
                    StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    result);
            }


            // ==========================================
            // NORMAL RESPONSE
            // ==========================================

            return Ok(result);
        }



        /// <summary>
        /// Returns audit statistics for the
        /// currently authenticated user.
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(
            typeof(AuditStatisticsDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStatistics()
        {
            // Read UserId from JWT
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "User ID not found in token."
                });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid User ID."
                });
            }

            var result =
                await _auditService.GetStatisticsAsync(userId);

            return Ok(result);
        }


        [HttpPost("upgrade")]
        public async Task<IActionResult> Upgrade(
    [FromBody] UpgradeSubscriptionRequest request)
        {
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
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
            var result =
                await _auditService.UpgradeSubscriptionAsync(
                    userId,
                    request.Plan);

            if (result == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid subscription plan."
                });
            }

            return Ok(new
            {
                success = true,
                message = $"Subscription changed to {result.Plan}.",
                plan = result.Plan,
                monthlyPrice = result.MonthlyPrice,
                monthlyAuditLimit = result.MonthlyAuditLimit,
                auditsUsed = result.AuditsUsed,
                remainingAudits = result.RemainingAudits,
                usagePercentage = result.UsagePercentage,
                isUnlimited = result.IsUnlimited,
                status = result.Status,
                isActive = result.IsActive
            });
        }
    }

    
}