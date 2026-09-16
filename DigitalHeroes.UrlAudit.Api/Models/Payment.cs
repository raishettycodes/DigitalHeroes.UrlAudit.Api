namespace DigitalHeroes.UrlAudit.Api.Models;

public class Payment
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Plan { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string Provider { get; set; } = "Razorpay";

    public string OrderId { get; set; } = string.Empty;

    public string? PaymentId { get; set; }

    public string? Signature { get; set; }

    public string Status { get; set; } = "Created";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
