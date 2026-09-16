namespace DigitalHeroes.UrlAudit.Api.Models;

public class CreatePaymentOrderResponse
{
    public string KeyId { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public string Plan { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";
}