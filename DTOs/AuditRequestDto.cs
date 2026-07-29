using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.DTOs
{
    /// <summary>
    /// Represents a URL audit request.
    /// </summary>
    public class AuditRequestDto
    {
        /// <summary>
        /// URL to audit.
        /// </summary>
        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;
    }
}