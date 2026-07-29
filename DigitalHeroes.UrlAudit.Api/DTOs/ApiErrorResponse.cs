namespace DigitalHeroes.UrlAudit.Api.DTOs
{
    public class ApiErrorResponse
    {
        public bool Success { get; set; } = false;

        public int StatusCode { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? RequestId { get; set; }
    }
}