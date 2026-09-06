using System.ComponentModel.DataAnnotations;

namespace DigitalHeroes.UrlAudit.Api.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public bool EmailVerified { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;


        public ICollection<Website> Websites { get; set; }  = new List<Website>();
    }
}