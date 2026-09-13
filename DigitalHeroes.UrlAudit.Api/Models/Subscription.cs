namespace DigitalHeroes.UrlAudit.Api.Models;

public class Subscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Plan { get; set; } = "Free";

    public int MonthlyAuditLimit { get; set; } = 100;

    public decimal MonthlyPrice { get; set; } = 0;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? PaymentProvider { get; set; }

    public string? ExternalSubscriptionId { get; set; }
}