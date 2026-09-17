using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;

namespace DigitalHeroes.UrlAudit.Api.Services;

public class RazorpayService
{
    private readonly RazorpayOptions _options;

    public RazorpayService(IOptions<RazorpayOptions> options)
    {
        _options = options.Value;
    }

    public (string OrderId, int AmountInPaise) CreateOrder(
        decimal amount,
        string receipt)
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) ||
            string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            throw new InvalidOperationException(
                "Razorpay configuration is missing.");
        }

        var client = new RazorpayClient(
            _options.KeyId,
            _options.KeySecret);

        var amountInPaise = (int)Math.Round(
            amount * 100,
            MidpointRounding.AwayFromZero);

        var orderData = new Dictionary<string, object>
        {
            { "amount", amountInPaise },
            { "currency", "INR" },
            { "receipt", receipt },
            { "payment_capture", 1 }
        };

        var order = client.Order.Create(orderData);

        return (
            order["id"].ToString()!,
            amountInPaise);
    }

    public bool VerifyPaymentSignature(
        string orderId,
        string paymentId,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            throw new InvalidOperationException(
                "Razorpay configuration is missing.");
        }

        var payload = $"{orderId}|{paymentId}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(_options.KeySecret));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(payload));

        var generatedSignature =
            Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(generatedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    public Razorpay.Api.Payment FetchPayment(
        string paymentId)
    {
        if (string.IsNullOrWhiteSpace(_options.KeyId) ||
            string.IsNullOrWhiteSpace(_options.KeySecret))
        {
            throw new InvalidOperationException(
                "Razorpay configuration is missing.");
        }

        var client = new RazorpayClient(
            _options.KeyId,
            _options.KeySecret);

        return client.Payment.Fetch(paymentId);
    }
}