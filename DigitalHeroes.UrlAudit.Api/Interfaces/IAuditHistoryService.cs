using DigitalHeroes.UrlAudit.Api.DTOs;


namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface IAuditHistoryService
    {
        Task<List<AuditHistoryDto>> GetWebsiteHistoryAsync(int websiteId);
    }
}