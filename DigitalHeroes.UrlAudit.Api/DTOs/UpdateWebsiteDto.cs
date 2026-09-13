using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.DTOs.Website
{
    public class UpdateWebsiteDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}