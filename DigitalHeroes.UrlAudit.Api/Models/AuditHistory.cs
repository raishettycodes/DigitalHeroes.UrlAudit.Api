namespace DigitalHeroes.UrlAudit.Api.Models;

public class AuditHistory
{
    public int Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public int ResponseTimeMs { get; set; }

    public bool IsReachable { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}