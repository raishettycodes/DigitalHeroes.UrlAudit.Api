namespace DigitalHeroes.UrlAudit.Api.DTOs;

public class SubscriptionDto
{
    public string Plan { get; set; } = "Free";

    public decimal MonthlyPrice { get; set; }

    public int MonthlyAuditLimit { get; set; }

    public int AuditsUsed { get; set; }

    public int RemainingAudits { get; set; }

    public double UsagePercentage { get; set; }

    public bool IsUnlimited { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; }
}