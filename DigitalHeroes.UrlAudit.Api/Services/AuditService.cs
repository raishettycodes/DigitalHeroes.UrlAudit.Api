using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using DigitalHeroes.UrlAudit.Api.DTOs;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using DigitalHeroes.UrlAudit.Api.Configuration;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    public class AuditService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuditService> _logger;
        private readonly AuditSettings _settings;

        public AuditService(HttpClient httpClient,IMemoryCache cache,ILogger<AuditService> logger, IOptions<AuditSettings> options)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _settings = options.Value;
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        public async Task<AuditResponseDto> AuditUrlAsync(string url)
        {
            var stopwatch = Stopwatch.StartNew();
            string cacheKey = $"audit_{url}";
            if (_cache.TryGetValue(cacheKey, out AuditResponseDto cachedResponse))
            {
                _logger.LogInformation("Returning cached result for {Url}", url);

                return cachedResponse;
            }

            try
            {
                _logger.LogInformation("Auditing URL {Url}", url);

                using var response = await _httpClient.GetAsync(url);

                stopwatch.Stop();

                var result = new AuditResponseDto
                {
                    Success = true,
                    Url = url,
                    StatusCode = (int)response.StatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    IsReachable = response.IsSuccessStatusCode,
                    Message = "URL audited successfully"
                };

                // Store in cache for 5 minutes
                _cache.Set(
                    cacheKey,
                    result,
                    TimeSpan.FromMinutes(_settings.CacheDurationMinutes));

                _logger.LogInformation("Stored audit result in cache for {Url}", url);

                return result;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Request timed out for {Url}", url);
                return new AuditResponseDto
                {
                    Success = false,
                    Url = url,
                    Message = "Request timed out after 10 seconds"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while auditing {Url}", url);

                throw;
            }
        
        }
    }
}