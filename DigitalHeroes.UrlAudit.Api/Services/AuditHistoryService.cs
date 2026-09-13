using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    public class AuditHistoryService : IAuditHistoryService
    {
        private readonly UrlAuditDbContext _context;

        public AuditHistoryService(
            UrlAuditDbContext context)
        {
            _context = context;
        }

        public async Task<List<AuditHistoryDto>> GetWebsiteHistoryAsync(int websiteId)
        {
            return await _context.AuditHistories
                .Where(x => x.WebsiteId == websiteId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AuditHistoryDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    Url = x.Url,
                    StatusCode = x.StatusCode,
                    ResponseTimeMs = x.ResponseTimeMs,
                    IsReachable = x.IsReachable,
                    Message = x.Message,

                    HttpVersion = x.HttpVersion,
                    Server = x.Server,
                    ContentType = x.ContentType,
                    ContentLength = x.ContentLength,
                    IsRedirect = x.IsRedirect,
                    RedirectLocation = x.RedirectLocation,
                    IsSslValid = x.IsSslValid,

                    Title = x.Title,
                    MetaDescription = x.MetaDescription,
                    H1Count = x.H1Count,
                    H2Count = x.H2Count,
                    Images = x.Images,
                    ImagesWithoutAlt = x.ImagesWithoutAlt,
                    SeoScore = x.SeoScore
                })
                .ToListAsync();
        }
    }
}