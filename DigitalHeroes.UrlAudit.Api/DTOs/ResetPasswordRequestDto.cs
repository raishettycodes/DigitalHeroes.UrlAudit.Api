using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.DTOs.Auth
{
    public class ResetPasswordRequestDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}