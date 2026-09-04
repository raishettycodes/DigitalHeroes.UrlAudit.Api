using System.Security.Claims;

using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuditHistoryController : ControllerBase
{
    private readonly UrlAuditDbContext _context;

    public AuditHistoryController(
        UrlAuditDbContext context)
    {
        _context = context;
    }

    // GET /api/AuditHistory
    // Supports pagination, search, status filter and sorting.

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? status = null,
        string? sort = "latest")
    {
        try
        {
            var userId = GetUserId();

            if (userId is null)
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid user."
                });
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query =
                from audit in _context.AuditHistories
                join website in _context.Websites
                    on audit.WebsiteId equals website.Id
                where website.UserId == userId.Value
                select audit;

            // Search

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();

                query = query.Where(audit =>
                    audit.Url.Contains(searchText) ||
                    audit.Message.Contains(searchText) ||
                    audit.StatusCode
                        .ToString()
                        .Contains(searchText));
            }

            // Status filter

            var statusFilter =
                status?.Trim().ToLowerInvariant();

            if (statusFilter == "successful")
            {
                query = query.Where(
                    audit => audit.IsReachable);
            }
            else if (statusFilter == "failed")
            {
                query = query.Where(
                    audit => !audit.IsReachable);
            }

            // Sorting

            query = sort?.Trim().ToLowerInvariant() switch
            {
                "oldest" =>
                    query.OrderBy(
                        audit => audit.CreatedAt),

                "fastest" =>
                    query.OrderBy(
                        audit => audit.ResponseTimeMs),

                "slowest" =>
                    query.OrderByDescending(
                        audit => audit.ResponseTimeMs),

                _ =>
                    query.OrderByDescending(
                        audit => audit.CreatedAt)
            };

            var totalRecords =
                await query.CountAsync();

            var totalPages =
                (int)Math.Ceiling(
                    totalRecords / (double)pageSize);

            var audits =
                await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(audit =>
                        MapToDto(audit))
                    .ToListAsync();

            var response =
                new PagedResponseDto<AuditHistoryDto>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages,
                    Items = audits
                };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    Success = false,
                    Message =
                        "An error occurred while retrieving audit history.",
                    Detail = ex.Message
                });
        }
    }

    // GET /api/AuditHistory/website/{websiteId}

    [HttpGet("website/{websiteId}")]
    public async Task<IActionResult> GetWebsiteHistory(
        int websiteId)
    {
        try
        {
            var userId = GetUserId();

            if (userId is null)
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid user."
                });
            }

            var websiteExists =
                await _context.Websites
                    .AnyAsync(website =>
                        website.Id == websiteId &&
                        website.UserId == userId.Value);

            if (!websiteExists)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Website not found."
                });
            }

            var history =
                await _context.AuditHistories
                    .Where(audit =>
                        audit.WebsiteId == websiteId)
                    .OrderByDescending(
                        audit => audit.CreatedAt)
                    .Select(audit =>
                        MapToDto(audit))
                    .ToListAsync();

            return Ok(history);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    Success = false,
                    Message =
                        "An error occurred while retrieving website history.",
                    Detail = ex.Message
                });
        }
    }

    // DELETE /api/AuditHistory/{id}

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudit(
        int id)
    {
        try
        {
            var userId = GetUserId();

            if (userId is null)
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid user."
                });
            }

            var audit =
                await (
                    from history in _context.AuditHistories
                    join website in _context.Websites
                        on history.WebsiteId equals website.Id
                    where history.Id == id &&
                          website.UserId == userId.Value
                    select history
                ).FirstOrDefaultAsync();

            if (audit is null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Audit not found."
                });
            }

            _context.AuditHistories.Remove(audit);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = "Audit deleted successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    Success = false,
                    Message =
                        "An error occurred while deleting the audit.",
                    Detail = ex.Message
                });
        }
    }

    // GET /api/AuditHistory/{id}

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuditById(
        int id)
    {
        try
        {
            var userId = GetUserId();

            if (userId is null)
            {
                return Unauthorized(new
                {
                    Success = false,
                    Message = "Invalid user."
                });
            }

            var audit =
                await (
                    from history in _context.AuditHistories
                    join website in _context.Websites
                        on history.WebsiteId equals website.Id
                    where history.Id == id &&
                          website.UserId == userId.Value
                    select history
                ).FirstOrDefaultAsync();

            if (audit is null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = "Audit not found."
                });
            }

            return Ok(MapToDto(audit));
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    Success = false,
                    Message =
                        "An error occurred while retrieving the audit.",
                    Detail = ex.Message
                });
        }
    }

    private int? GetUserId()
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return int.TryParse(
            userIdClaim,
            out var userId)
                ? userId
                : null;
    }

    private static AuditHistoryDto MapToDto(
        Models.AuditHistory audit)
    {
        return new AuditHistoryDto
        {
            Id = audit.Id,
            WebsiteId = audit.WebsiteId,
            CreatedAt = audit.CreatedAt,

            Url = audit.Url,
            StatusCode = audit.StatusCode,
            ResponseTimeMs = audit.ResponseTimeMs,
            IsReachable = audit.IsReachable,
            Message = audit.Message,

            HttpVersion = audit.HttpVersion,
            Server = audit.Server,
            ContentType = audit.ContentType,
            ContentLength = audit.ContentLength,
            IsRedirect = audit.IsRedirect,
            RedirectLocation =
                audit.RedirectLocation,
            IsSslValid = audit.IsSslValid,

            Title = audit.Title,
            MetaDescription =
                audit.MetaDescription,
            H1Count = audit.H1Count,
            H2Count = audit.H2Count,
            Images = audit.Images,
            ImagesWithoutAlt =
                audit.ImagesWithoutAlt,
            SeoScore = audit.SeoScore
        };
    }
}