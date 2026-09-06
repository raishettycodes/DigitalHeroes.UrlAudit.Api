namespace DigitalHeroes.UrlAudit.Api.DTOs.Website
{
    public class WebsiteDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}