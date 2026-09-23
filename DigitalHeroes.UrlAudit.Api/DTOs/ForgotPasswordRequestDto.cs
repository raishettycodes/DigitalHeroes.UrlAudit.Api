using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.DTOs.Auth
{
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}