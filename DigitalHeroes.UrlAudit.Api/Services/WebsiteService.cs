using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs.Website;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    public class WebsiteService : IWebsiteService
    {
        private readonly UrlAuditDbContext _context;

        public WebsiteService(UrlAuditDbContext context)
        {
            _context = context;
        }

        public async Task<WebsiteDto> AddAsync(
            int userId,
            CreateWebsiteDto dto)
        {
            var normalizedUrl = dto.Url.Trim().TrimEnd('/');

            var userWebsites = await _context.Websites
                .Where(w => w.UserId == userId)
                .ToListAsync();

            var existingWebsite = userWebsites
                .FirstOrDefault(w =>
                    w.Url.Trim().TrimEnd('/') == normalizedUrl);

            if (existingWebsite != null)
            {
                return MapToDto(existingWebsite);
            }

            var website = new Website
            {
                Name = dto.Name,
                Url = normalizedUrl,
                UserId = userId,
                IsActive = true,
                CreatedOn = DateTime.UtcNow
            };

            _context.Websites.Add(website);

            await _context.SaveChangesAsync();

            return MapToDto(website);
        }

        public async Task<List<WebsiteDto>> GetAllAsync(int userId)
        {
            return await _context.Websites
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedOn)
                .Select(x => new WebsiteDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Url = x.Url,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }

        public async Task<WebsiteDto?> GetByIdAsync(
            int userId,
            int id)
        {
            var website = await _context.Websites
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

            if (website == null)
                return null;

            return MapToDto(website);
        }

        public async Task<WebsiteDto?> UpdateAsync(
            int userId,
            int id,
            UpdateWebsiteDto dto)
        {
            var website = await _context.Websites
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

            if (website == null)
                return null;

            website.Name = dto.Name;
            website.Url = dto.Url.Trim().TrimEnd('/');
            website.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return MapToDto(website);
        }

        public async Task DeleteAsync(
            int userId,
            int id)
        {
            var website = await _context.Websites
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

            if (website == null)
                return;

            _context.Websites.Remove(website);

            await _context.SaveChangesAsync();
        }

        private static WebsiteDto MapToDto(Website website)
        {
            return new WebsiteDto
            {
                Id = website.Id,
                Name = website.Name,
                Url = website.Url,
                IsActive = website.IsActive
            };
        }
    }
}