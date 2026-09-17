namespace DigitalHeroes.UrlAudit.Api.Models;

public class PaymentWebhookEvent
{
    public int Id { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string? RazorpayOrderId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public bool Processed { get; set; }
}