namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string resetLink);
    }
}