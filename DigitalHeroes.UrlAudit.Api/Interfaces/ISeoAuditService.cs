using DigitalHeroes.UrlAudit.Api.DTOs.Audit;

namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface ISeoAuditService
    {
        Task<SeoAuditDto> AnalyzeAsync(string url);
    }

}
