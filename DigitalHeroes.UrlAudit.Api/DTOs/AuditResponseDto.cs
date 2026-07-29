namespace DigitalHeroes.UrlAudit.Api.DTOs
{
    /// <summary>
    /// Represents the audit result.
    /// </summary>
    public class AuditResponseDto
    {
        /// <summary>
        /// Indicates whether the audit succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Audited URL.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// HTTP status code returned.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Indicates whether the URL is reachable.
        /// </summary>
        public bool IsReachable { get; set; }

        /// <summary>
        /// Additional information.
        /// </summary>
        public string? Message { get; set; }
    }
}