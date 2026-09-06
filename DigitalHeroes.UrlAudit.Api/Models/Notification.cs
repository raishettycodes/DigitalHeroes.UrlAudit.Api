namespace DigitalHeroes.UrlAudit.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Type { get; set; } = "Info";

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}