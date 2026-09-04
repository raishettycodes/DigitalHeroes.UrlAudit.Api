using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.Models
{
    public class Website
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public ICollection<AuditHistory> AuditHistories { get; set; }
    = new List<AuditHistory>();
    }
}