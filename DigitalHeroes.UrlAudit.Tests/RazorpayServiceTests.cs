using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace DigitalHeroes.UrlAudit.Tests;

public class RazorpayServiceTests
{
    private const string TestSecret =
        "test-secret-for-unit-tests-only";

    private static RazorpayService CreateService()
    {
        var options = Options.Create(new RazorpayOptions
        {
            KeyId = "test-key-id",
            KeySecret = TestSecret,
            WebhookSecret = "test-webhook-secret"
        });

        return new RazorpayService(options);
    }

    [Fact]
    public void VerifyPaymentSignature_WithValidSignature_ReturnsTrue()
    {
        var service = CreateService();

        const string orderId = "order_test123";
        const string paymentId = "pay_test123";

        var signature = CreateHmacSignature(
            $"{orderId}|{paymentId}",
            TestSecret);

        var result = service.VerifyPaymentSignature(
            orderId,
            paymentId,
            signature);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPaymentSignature_WithInvalidSignature_ReturnsFalse()
    {
        var service = CreateService();

        var result = service.VerifyPaymentSignature(
            "order_test123",
            "pay_test123",
            "invalid-signature");

        Assert.False(result);
    }

    [Fact]
    public void VerifyPaymentSignature_WithMissingSecret_ThrowsException()
    {
        var options = Options.Create(new RazorpayOptions
        {
            KeyId = "test-key-id",
            KeySecret = "",
            WebhookSecret = "test-webhook-secret"
        });

        var service = new RazorpayService(options);

        Assert.Throws<InvalidOperationException>(() =>
            service.VerifyPaymentSignature(
                "order_test123",
                "pay_test123",
                "signature"));
    }

    [Fact]
    public void VerifyWebhookSignature_WithValidSignature_ReturnsTrue()
    {
        var service = CreateService();

        const string payload =
            """{"event":"payment.captured","id":"evt_test123"}""";

        const string webhookSecret = "test-webhook-secret";

        var signature = CreateHmacSignature(
            payload,
            webhookSecret);

        var result = service.VerifyWebhookSignature(
            payload,
            signature,
            webhookSecret);

        Assert.True(result);
    }

    [Fact]
    public void VerifyWebhookSignature_WithInvalidSignature_ReturnsFalse()
    {
        var service = CreateService();

        var result = service.VerifyWebhookSignature(
            """{"event":"payment.captured"}""",
            "invalid-signature",
            "test-webhook-secret");

        Assert.False(result);
    }

    [Fact]
    public void VerifyWebhookSignature_WithMissingInputs_ReturnsFalse()
    {
        var service = CreateService();

        Assert.False(
            service.VerifyWebhookSignature(
                "",
                "signature",
                "test-webhook-secret"));

        Assert.False(
            service.VerifyWebhookSignature(
                "payload",
                "",
                "test-webhook-secret"));

        Assert.False(
            service.VerifyWebhookSignature(
                "payload",
                "signature",
                ""));
    }

    private static string CreateHmacSignature(
        string payload,
        string secret)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
