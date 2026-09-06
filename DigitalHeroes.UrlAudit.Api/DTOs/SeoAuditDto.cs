namespace DigitalHeroes.UrlAudit.Api.DTOs.Audit
{
    public class SeoAuditDto
    {
        public string? Title { get; set; }

        public string? MetaDescription { get; set; }

        public int H1Count { get; set; }

        public int H2Count { get; set; }

        public int Images { get; set; }

        public int ImagesWithoutAlt { get; set; }

        public int InternalLinks { get; set; }

        public int ExternalLinks { get; set; }

        public int SeoScore { get; set; }
    }
}