using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.Extensions.Configuration;
using Resend;
using Microsoft.Extensions.Logging;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    public class ResendEmailService : IEmailService
    {
        private readonly IResend _resend;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResendEmailService> _logger;


        public ResendEmailService(
     IResend resend,
     IConfiguration configuration,
     ILogger<ResendEmailService> logger)
        {
            _resend = resend;
            _configuration = configuration;
            _logger = logger;
        }
        public async Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string resetLink)
        {
            var fromEmail =
                _configuration["Resend:FromEmail"];

            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException(
                    "Resend:FromEmail is not configured.");
            }

            var message = new EmailMessage();

            message.From = fromEmail;
            message.To.Add(recipientEmail);
            message.Subject = "Reset your DigitalHeroes password";

            message.HtmlBody = $"""
                <h2>Reset your DigitalHeroes password</h2>

                <p>We received a request to reset your password.</p>

                <p>
                    <a href="{resetLink}">
                        Reset your password
                    </a>
                </p>

                <p>
                    This link will expire in 30 minutes.
                </p>

                <p>
                    If you did not request a password reset,
                    you can safely ignore this email.
                </p>

                <p>— DigitalHeroes</p>
                """;

            var response = await _resend.EmailSendAsync(message);

            _logger.LogInformation(
                "Password reset email sent. Resend Email ID: {EmailId}",
                response.Content);
        }
    }
}