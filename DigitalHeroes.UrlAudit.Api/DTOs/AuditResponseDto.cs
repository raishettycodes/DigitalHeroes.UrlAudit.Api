namespace DigitalHeroes.UrlAudit.Api.DTOs;

public class AuditResponseDto
{
    public bool Success { get; set; }

    public string? Url { get; set; }

    public int StatusCode { get; set; }

    public long ResponseTimeMs { get; set; }

    public bool IsReachable { get; set; }

    public string? Message { get; set; }

    // Technical details

    public string? HttpVersion { get; set; }

    public string? Server { get; set; }

    public string? ContentType { get; set; }

    public long? ContentLength { get; set; }

    public bool? IsRedirect { get; set; }

    public string? RedirectLocation { get; set; }

    public bool? IsSslValid { get; set; }

    // SEO details

    public string? Title { get; set; }

    public string? MetaDescription { get; set; }

    public int? H1Count { get; set; }

    public int? H2Count { get; set; }

    public int? Images { get; set; }

    public int? ImagesWithoutAlt { get; set; }

    public int? SeoScore { get; set; }

    public int? InternalLinks { get; set; }

    public int? ExternalLinks { get; set; }
}