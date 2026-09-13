using System.Diagnostics;
using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Helpers;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    public class AuditService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuditService> _logger;
        private readonly AuditSettings _settings;
        private readonly UrlAuditDbContext _context;
        private readonly SeoAuditService _seoAuditService;

        public AuditService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<AuditService> logger,
            IOptions<AuditSettings> options,
            UrlAuditDbContext context,
            SeoAuditService seoAuditService)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _settings = options.Value;
            _context = context;
            _seoAuditService = seoAuditService;

            _httpClient.Timeout =
                TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        public async Task<AuditResponseDto> AuditUrlAsync(string url)
        {
            var stopwatch = Stopwatch.StartNew();

            string cacheKey = $"audit_{url}";

            if (_cache.TryGetValue(cacheKey, out AuditResponseDto? cachedResponse))
            {
                _logger.LogInformation("Returning cached result for {Url}", url);
                return cachedResponse!;
            }

            try
            {
                _logger.LogInformation("Auditing URL {Url}", url);

                var response = await _httpClient.GetAsync(url);

                stopwatch.Stop();

                // HTTP Information
                string server =
                    response.Headers.Server?.ToString() ?? "Unknown";

                string? contentType =
                    response.Content.Headers.ContentType?.MediaType;

                long contentLength =
                    response.Content.Headers.ContentLength ?? 0;

                string httpVersion =
                    response.Version.ToString();

                bool isRedirect =
                    (int)response.StatusCode >= 300 &&
                    (int)response.StatusCode < 400;

                string? redirectLocation =
                    response.Headers.Location?.ToString();

                bool ssl =
                    response.RequestMessage?.RequestUri?.Scheme ==
                    Uri.UriSchemeHttps;

                // =========================================================
                // SEO ANALYSIS
                // =========================================================

                string html = await response.Content.ReadAsStringAsync();

                var seo = _seoAuditService.Analyze(url, html);

                // =========================================================
                // SEO SCORE
                // =========================================================

                int seoScore = SeoScoreCalculator.Calculate(
                    seo.Title,
                    seo.MetaDescription,
                    seo.H1Count,
                    seo.Images,
                    seo.ImagesWithoutAlt,
                    ssl);

                // Build Response
                var result = new AuditResponseDto
                {
                    Success = true,
                    Url = url,

                    StatusCode = (int)response.StatusCode,

                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,

                    IsReachable = response.IsSuccessStatusCode,

                    Message = response.IsSuccessStatusCode
                        ? "URL audited successfully"
                        : $"Website returned HTTP {(int)response.StatusCode}",

                    HttpVersion = httpVersion,

                    Server = server,

                    ContentType = contentType,

                    ContentLength = contentLength,

                    IsRedirect = isRedirect,

                    RedirectLocation = redirectLocation,

                    IsSslValid = ssl,

                    // SEO
                    Title = seo.Title,

                    MetaDescription = seo.MetaDescription,

                    H1Count = seo.H1Count,

                    H2Count = seo.H2Count,

                    Images = seo.Images,

                    ImagesWithoutAlt = seo.ImagesWithoutAlt,

                    SeoScore = seoScore
                };

                // Save Audit History
                var auditHistory = new AuditHistory
                {
                    Url = result.Url!,
                    StatusCode = result.StatusCode,
                    ResponseTimeMs = (int)result.ResponseTimeMs,
                    IsReachable = result.IsReachable,
                    Message = result.Message,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditHistories.Add(auditHistory);

                await _context.SaveChangesAsync();

                // Cache Result
                _cache.Set(
                    cacheKey,
                    result,
                    TimeSpan.FromMinutes(_settings.CacheDurationMinutes));

                _logger.LogInformation(
                    "Stored audit result in cache for {Url}", url);

                return result;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning(
                    "Request timed out for {Url}", url);

                return new AuditResponseDto
                {
                    Success = false,
                    Url = url,
                    Message = $"Request timed out after {_settings.TimeoutSeconds} seconds"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while auditing {Url}",
                    url);

                throw;
            }
        }
    }
}