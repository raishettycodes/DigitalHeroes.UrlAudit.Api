using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Models;

namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface IAuditService
    {
        Task<AuditResultDto> AuditWebsiteAsync(int websiteId);
    }
}