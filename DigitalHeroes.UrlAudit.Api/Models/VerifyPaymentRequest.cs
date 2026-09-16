namespace DigitalHeroes.UrlAudit.Api.Models;

public class VerifyPaymentRequest
{
    public string Plan { get; set; } = string.Empty;

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string RazorpayPaymentId { get; set; } = string.Empty;

    public string RazorpaySignature { get; set; } = string.Empty;
}