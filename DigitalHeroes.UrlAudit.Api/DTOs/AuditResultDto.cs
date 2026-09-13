namespace DigitalHeroes.UrlAudit.Api.Models
{
    public class AuditResultDto
    {
        public int Id { get; set; }

        public int WebsiteId { get; set; }

        public Website Website { get; set; } = null!;

        public int StatusCode { get; set; }

        public long ResponseTimeMs { get; set; }

        public bool IsReachable { get; set; }

        public bool IsSslValid { get; set; }

        // HTTP Details

        public string HttpVersion { get; set; } = string.Empty;

        public string? Server { get; set; }

        public string? ContentType { get; set; }

        public long ContentLength { get; set; }

        public bool IsRedirect { get; set; }

        public string? RedirectLocation { get; set; }

        // Future SEO

        public string? PageTitle { get; set; }

        public string? MetaDescription { get; set; }

        public int SeoScore { get; set; }

        public DateTime AuditDate { get; set; }
    }
}