namespace DigitalHeroes.UrlAudit.Api.Models
{
    public class AuditResult
    {
        public int Id { get; set; }

        public int WebsiteId { get; set; }

        public Website Website { get; set; } = null!;

        public int StatusCode { get; set; }

        public long ResponseTimeMs { get; set; }

        public bool IsSslValid { get; set; }

        public bool IsReachable { get; set; }

        public DateTime AuditDate { get; set; }
    }
}
