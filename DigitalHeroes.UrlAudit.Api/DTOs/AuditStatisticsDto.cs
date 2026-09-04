namespace DigitalHeroes.UrlAudit.Api.DTOs;

public class AuditStatisticsDto
{
    public int TotalAudits { get; set; }

    public int SuccessfulAudits { get; set; }

    public int FailedAudits { get; set; }

    public long AverageResponseTimeMs { get; set; }
}