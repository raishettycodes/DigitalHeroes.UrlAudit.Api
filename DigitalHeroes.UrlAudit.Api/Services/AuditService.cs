using System.Diagnostics;

using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Helpers;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalHeroes.UrlAudit.Api.Services;

public class AuditService
{
  //  private const int FreePlanMonthlyLimit = 100;

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
            TimeSpan.FromSeconds(
                _settings.TimeoutSeconds);
    }

    /*
     * =========================================================
     * MONTHLY AUDIT COUNT
     * =========================================================
     */

    public async Task<int> GetMonthlyAuditCountAsync(
        int userId)
    {
        var (
            monthStart,
            nextMonthStart) =
            GetCurrentMonthRange();

        return await (
            from audit in _context.AuditHistories

            join website in _context.Websites
                on audit.WebsiteId equals website.Id

            where website.UserId == userId &&
                  audit.CreatedAt >= monthStart &&
                  audit.CreatedAt < nextMonthStart

            select audit
        ).CountAsync();
    }

    /*
     * =========================================================
     * SUBSCRIPTION USAGE
     * =========================================================
     */

    public async Task<SubscriptionDto>
        GetSubscriptionUsageAsync(
            int userId)
    {
        var subscription =
            await GetOrCreateSubscriptionAsync(
                userId);

        var auditsUsed =
            await GetMonthlyAuditCountAsync(
                userId);

        int remainingAudits;
        int usagePercentage;

        /*
         * Unlimited plan
         */

        if (subscription.MonthlyAuditLimit == -1)
        {
            remainingAudits = -1;
            usagePercentage = 0;
        }
        else
        {
            remainingAudits =
                Math.Max(
                    0,
                    subscription.MonthlyAuditLimit -
                    auditsUsed);

            usagePercentage =
                subscription.MonthlyAuditLimit > 0
                    ? (int)Math.Round(
                        (double)auditsUsed /
                        subscription.MonthlyAuditLimit *
                        100)
                    : 0;

            usagePercentage =
                Math.Min(
                    usagePercentage,
                    100);
        }

        return new SubscriptionDto
        {
            Plan = subscription.Plan,

            MonthlyAuditLimit =
                subscription.MonthlyAuditLimit,

            AuditsUsed =
                auditsUsed,

            RemainingAudits =
                remainingAudits,

            UsagePercentage =
                usagePercentage,

            IsActive =
                subscription.IsActive
        };
    }

    /*
     * =========================================================
     * MAIN AUDIT METHOD
     * =========================================================
     */

    public async Task<AuditResponseDto>
        AuditUrlAsync(
            string url,
            int userId)
    {

        /*
         * -----------------------------------------------------
         * Validate URL
         * -----------------------------------------------------
         */

        if (string.IsNullOrWhiteSpace(url))
        {
            return CreateFailureResponse(
                url,
                "Website URL is required.");
        }

        var normalizedUrl =
            NormalizeUrl(url);

        if (!Uri.TryCreate(
                normalizedUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            return CreateFailureResponse(
                url,
                "Please enter a valid HTTP or HTTPS URL.");
        }

        /*
         * -----------------------------------------------------
         * Subscription
         * -----------------------------------------------------
         */

        var subscription =
            await GetOrCreateSubscriptionAsync(
                userId);

        if (!subscription.IsActive)
        {
            return CreateFailureResponse(
                normalizedUrl,
                "Your subscription is not active.");
        }

        /*
         * -----------------------------------------------------
         * Monthly audit limit
         * -----------------------------------------------------
         */

        var monthlyAuditCount =
            await GetMonthlyAuditCountAsync(
                userId);

        if (subscription.MonthlyAuditLimit != -1 &&
            monthlyAuditCount >=
            subscription.MonthlyAuditLimit)
        {
            _logger.LogWarning(
                "Monthly audit limit reached. " +
                "UserId: {UserId}, Plan: {Plan}, " +
                "Used: {Used}, Limit: {Limit}",
                userId,
                subscription.Plan,
                monthlyAuditCount,
                subscription.MonthlyAuditLimit);

            return CreateFailureResponse(
                normalizedUrl,
                $"Monthly audit limit of " +
                $"{subscription.MonthlyAuditLimit} " +
                $"has been reached.");
        }

        /*
         * -----------------------------------------------------
         * Cache
         * -----------------------------------------------------
         */

        var cacheKey =
            $"audit_{userId}_{normalizedUrl}";

        if (_cache.TryGetValue(
                cacheKey,
                out AuditResponseDto? cachedResponse) &&
            cachedResponse != null)
        {
            _logger.LogInformation(
                "Returning cached audit result for " +
                "{Url}, UserId: {UserId}",
                normalizedUrl,
                userId);

            var cachedResult =
                CloneAuditResponse(
                    cachedResponse);

            cachedResult.Message =
                "URL audit completed successfully " +
                "(cached result).";

            /*
             * Cached audits continue to create
             * history and notifications, matching
             * your existing application behavior.
             */

            await SaveAuditHistoryAsync(
                cachedResult,
                userId);

            await CreateAuditNotificationAsync(
                userId,
                cachedResult);

            return cachedResult;
        }

        /*
         * -----------------------------------------------------
         * Start stopwatch
         * -----------------------------------------------------
         */

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting audit for {Url}, " +
                "UserId: {UserId}",
                normalizedUrl,
                userId);

            /*
             * -------------------------------------------------
             * ONE HTTP REQUEST
             * -------------------------------------------------
             */

            using var response =
                await _httpClient.GetAsync(
                    normalizedUrl,
                    HttpCompletionOption.ResponseContentRead);

            /*
             * Read HTML/body from the SAME response.
             */

            var responseBody =
                await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            /*
             * -------------------------------------------------
             * Technical details
             * -------------------------------------------------
             */

            var server =
                response.Headers.Server?.ToString();

            var contentType =
                response.Content
                    .Headers
                    .ContentType?
                    .MediaType;

            var contentLength =
                response.Content
                    .Headers
                    .ContentLength;

            var httpVersion =
                response.Version.ToString();

            var statusCode =
                (int)response.StatusCode;

            /*
             * HttpClient follows redirects by default.
             *
             * Therefore this checks the final response
             * status code. We will improve true redirect
             * chain detection separately if required.
             */

            var isRedirect =
                statusCode >= 300 &&
                statusCode < 400;

            var redirectLocation =
                response.Headers.Location?
                    .ToString();

            /*
             * At this stage HTTPS indicates that the
             * request was made using HTTPS.
             *
             * It does not perform independent certificate
             * validation.
             */

            var isSslValid =
                response.RequestMessage?
                    .RequestUri?
                    .Scheme ==
                Uri.UriSchemeHttps;

            /*
             * -------------------------------------------------
             * SEO analysis
             * -------------------------------------------------
             *
             * IMPORTANT:
             *
             * We pass responseBody directly.
             *
             * SeoAuditService does NOT make another
             * HTTP request.
             */

            var seo =
                new DTOs.Audit.SeoAuditDto();

            if (IsHtmlContent(contentType))
            {
                seo =
                    _seoAuditService.Analyze(
                        normalizedUrl,
                        responseBody);
            }

            /*
             * -------------------------------------------------
             * SEO score
             * -------------------------------------------------
             */

            seo.SeoScore =
                SeoScoreCalculator.Calculate(
                    seo.Title,
                    seo.MetaDescription,
                    seo.H1Count,
                    seo.Images,
                    seo.ImagesWithoutAlt,
                    isSslValid);

            /*
             * -------------------------------------------------
             * Build final result
             * -------------------------------------------------
             */

            var result =
                new AuditResponseDto
                {
                    Success = true,

                    Url =
                        normalizedUrl,

                    StatusCode =
                        statusCode,

                    ResponseTimeMs =
                        stopwatch.ElapsedMilliseconds,

                    IsReachable =
                        response.IsSuccessStatusCode,

                    HttpVersion =
                        httpVersion,

                    Server =
                        server,

                    ContentType =
                        contentType,

                    ContentLength =
                        contentLength,

                    IsRedirect =
                        isRedirect,

                    RedirectLocation =
                        redirectLocation,

                    IsSslValid =
                        isSslValid,

                    Title =
                        seo.Title,

                    MetaDescription =
                        seo.MetaDescription,

                    H1Count =
                        seo.H1Count,

                    H2Count =
                        seo.H2Count,

                    Images =
                        seo.Images,

                    ImagesWithoutAlt =
                        seo.ImagesWithoutAlt,

                    SeoScore =
                        seo.SeoScore,

                    Message =
                        response.IsSuccessStatusCode
                            ? "URL audited successfully"
                            : $"Website returned HTTP {statusCode}",

                    InternalLinks = seo.InternalLinks,

                    ExternalLinks = seo.ExternalLinks
                };

            /*
             * -------------------------------------------------
             * Save history
             * -------------------------------------------------
             */

            await SaveAuditHistoryAsync(
                result,
                userId);

            /*
             * -------------------------------------------------
             * Notification
             * -------------------------------------------------
             */

            await CreateAuditNotificationAsync(
                userId,
                result);

            /*
             * -------------------------------------------------
             * Cache
             * -------------------------------------------------
             */

            _cache.Set(
                cacheKey,
                result,
                TimeSpan.FromMinutes(
                    _settings.CacheDurationMinutes));

            _logger.LogInformation(
                "Audit completed successfully for {Url}. " +
                "UserId: {UserId}, " +
                "StatusCode: {StatusCode}, " +
                "ResponseTime: {ResponseTime}ms",
                normalizedUrl,
                userId,
                statusCode,
                result.ResponseTimeMs);

            return result;
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                "Request timed out for {Url}. " +
                "UserId: {UserId}",
                normalizedUrl,
                userId);

            var timeoutResult =
                new AuditResponseDto
                {
                    Success = false,

                    Url =
                        normalizedUrl,

                    StatusCode = 0,

                    ResponseTimeMs =
                        stopwatch.ElapsedMilliseconds,

                    IsReachable = false,

                    Message =
                        $"Request timed out after " +
                        $"{_settings.TimeoutSeconds} seconds"
                };

            await SaveAuditHistoryAsync(
                timeoutResult,
                userId);

            await CreateAuditNotificationAsync(
                userId,
                timeoutResult);

            return timeoutResult;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HTTP error while auditing {Url}. " +
                "UserId: {UserId}",
                normalizedUrl,
                userId);

            var errorResult =
                new AuditResponseDto
                {
                    Success = false,

                    Url =
                        normalizedUrl,

                    StatusCode = 0,

                    ResponseTimeMs =
                        stopwatch.ElapsedMilliseconds,

                    IsReachable = false,

                    Message =
                        "Unable to reach the website."
                };

            await SaveAuditHistoryAsync(
                errorResult,
                userId);

            await CreateAuditNotificationAsync(
                userId,
                errorResult);

            return errorResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Unexpected error while auditing {Url}. " +
                "UserId: {UserId}",
                normalizedUrl,
                userId);

            var errorResult =
                new AuditResponseDto
                {
                    Success = false,

                    Url =
                        normalizedUrl,

                    StatusCode = 0,

                    ResponseTimeMs =
                        stopwatch.ElapsedMilliseconds,

                    IsReachable = false,

                    Message =
                        "An unexpected error occurred " +
                        "while auditing the website."
                };

            await SaveAuditHistoryAsync(
                errorResult,
                userId);

            await CreateAuditNotificationAsync(
                userId,
                errorResult);

            return errorResult;
        }
    }

    /*
     * =========================================================
     * SAVE AUDIT HISTORY
     * =========================================================
     */

    private async Task SaveAuditHistoryAsync(
        AuditResponseDto result,
        int userId)
    {
        var normalizedUrl =
            NormalizeUrl(result.Url);

        var website =
            await _context.Websites
                .FirstOrDefaultAsync(
                    website =>
                        website.UserId == userId &&
                        website.Url == normalizedUrl);

        /*
         * Create website automatically if it doesn't exist.
         */

        if (website == null)
        {
            if (!Uri.TryCreate(
                    normalizedUrl,
                    UriKind.Absolute,
                    out var uri))
            {
                throw new InvalidOperationException(
                    "Invalid website URL.");
            }

            website =
                new Website
                {
                    Name =
                        uri.Host,

                    Url =
                        normalizedUrl,

                    UserId =
                        userId,

                    IsActive =
                        true,

                    CreatedOn =
                        DateTime.UtcNow
                };

            _context.Websites.Add(
                website);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Website created. " +
                "WebsiteId: {WebsiteId}, " +
                "UserId: {UserId}",
                website.Id,
                userId);
        }

        /*
         * Create history record.
         */

        var auditHistory =
            new AuditHistory
            {
                WebsiteId =
                    website.Id,

                Url =
                    normalizedUrl,

                StatusCode =
                    result.StatusCode,

                ResponseTimeMs =
                    (int)result.ResponseTimeMs,

                IsReachable =
                    result.IsReachable,

                Message =
                    result.Message,

                CreatedAt =
                    DateTime.UtcNow,

                /*
                 * Technical
                 */

                HttpVersion =
                    result.HttpVersion,

                Server =
                    result.Server,

                ContentType =
                    result.ContentType,

                ContentLength =
                    result.ContentLength,

                IsRedirect =
                    result.IsRedirect,

                RedirectLocation =
                    result.RedirectLocation,

                IsSslValid =
                    result.IsSslValid,

                /*
                 * SEO
                 */

                Title =
                    result.Title,

                MetaDescription =
                    result.MetaDescription,

                H1Count =
                    result.H1Count,

                H2Count =
                    result.H2Count,

                Images =
                    result.Images,

                ImagesWithoutAlt =
                    result.ImagesWithoutAlt,

                SeoScore =
                    result.SeoScore
            };

        _context.AuditHistories.Add(
            auditHistory);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Audit history saved. " +
            "AuditId: {AuditId}, " +
            "UserId: {UserId}",
            auditHistory.Id,
            userId);
    }

    /*
     * =========================================================
     * NOTIFICATION
     * =========================================================
     */

    private async Task CreateAuditNotificationAsync(
        int userId,
        AuditResponseDto result)
    {
        var isSuccessful =
            result.Success &&
            result.IsReachable;

        var notification =
            new Notification
            {
                UserId =
                    userId.ToString(),

                Title =
                    isSuccessful
                        ? "Audit completed successfully"
                        : "Audit failed",

                Message =
                    isSuccessful
                        ? $"Your website audit for " +
                          $"{result.Url} was completed successfully."
                        : $"Your website audit for " +
                          $"{result.Url} could not be completed. " +
                          $"{result.Message}",

                Type =
                    isSuccessful
                        ? "Success"
                        : "Error",

                IsRead = false,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.Notifications.Add(
            notification);

        await _context.SaveChangesAsync();
    }

    /*
     * =========================================================
     * SUBSCRIPTION
     * =========================================================
     */

    private async Task<Subscription>
        GetOrCreateSubscriptionAsync(
            int userId)
    {
        var subscription =
            await _context.Subscriptions
                .FirstOrDefaultAsync(
                    subscription =>
                        subscription.UserId == userId);

        if (subscription != null)
        {
            return subscription;
        }

        var freePlan =
      PlanDefinitions.Plans[PlanDefinitions.Free];

        subscription = new Subscription
        {
            UserId = userId,

            Plan = freePlan.Name,

            MonthlyAuditLimit =
                freePlan.MonthlyAuditLimit,

            MonthlyPrice =
                freePlan.MonthlyPrice,

            IsActive = true,

            Status = "Active",

            StartDate = DateTime.UtcNow,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.Subscriptions.Add(
            subscription);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created default Free subscription. " +
            "UserId: {UserId}",
            userId);

        return subscription;
    }

    /*
     * =========================================================
     * CURRENT MONTH
     * =========================================================
     */

    private static (
        DateTime MonthStart,
        DateTime NextMonthStart)
        GetCurrentMonthRange()
    {
        var now =
            DateTime.UtcNow;

        var monthStart =
            new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        return (
            monthStart,
            monthStart.AddMonths(1));
    }

    /*
     * =========================================================
     * FAILURE RESPONSE
     * =========================================================
     */

    private static AuditResponseDto
        CreateFailureResponse(
            string url,
            string message)
    {
        return new AuditResponseDto
        {
            Success = false,

            Url = url,

            StatusCode = 0,

            ResponseTimeMs = 0,

            IsReachable = false,

            Message = message
        };
    }

    /*
     * =========================================================
     * CLONE CACHED RESULT
     * =========================================================
     */

    private static AuditResponseDto
        CloneAuditResponse(
            AuditResponseDto source)
    {
        return new AuditResponseDto
        {
            Success =
                source.Success,

            Url =
                source.Url,

            StatusCode =
                source.StatusCode,

            ResponseTimeMs =
                source.ResponseTimeMs,

            IsReachable =
                source.IsReachable,

            Message =
                source.Message,

            /*
             * Technical
             */

            HttpVersion =
                source.HttpVersion,

            Server =
                source.Server,

            ContentType =
                source.ContentType,

            ContentLength =
                source.ContentLength,

            IsRedirect =
                source.IsRedirect,

            RedirectLocation =
                source.RedirectLocation,

            IsSslValid =
                source.IsSslValid,

            /*
             * SEO
             */

            Title =
                source.Title,

            MetaDescription =
                source.MetaDescription,

            H1Count =
                source.H1Count,

            H2Count =
                source.H2Count,

            Images =
                source.Images,

            ImagesWithoutAlt =
                source.ImagesWithoutAlt,

            SeoScore =
                source.SeoScore,
            InternalLinks =
    source.InternalLinks,

            ExternalLinks =
    source.ExternalLinks,
        };
    }

    /*
     * =========================================================
     * URL NORMALIZATION
     * =========================================================
     */

    private static string
        NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        url =
            url.Trim();

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return url.TrimEnd('/');
        }

        return uri
            .GetLeftPart(
                UriPartial.Path)
            .TrimEnd('/');
    }

    /*
     * =========================================================
     * HTML CONTENT CHECK
     * =========================================================
     */

    private static bool IsHtmlContent(
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(
                contentType))
        {
            return false;
        }

        return
            contentType.Contains(
                "text/html",
                StringComparison.OrdinalIgnoreCase)
            ||
            contentType.Contains(
                "application/xhtml+xml",
                StringComparison.OrdinalIgnoreCase);
    }

    /*
     * =========================================================
     * STATISTICS
     * =========================================================
     */

    public async Task<AuditStatisticsDto>
        GetStatisticsAsync(
            int userId)
    {
        var query =
            from audit in _context.AuditHistories

            join website in _context.Websites
                on audit.WebsiteId equals website.Id

            where website.UserId == userId

            select audit;

        var totalAudits =
            await query.CountAsync();

        var successfulAudits =
            await query.CountAsync(
                audit =>
                    audit.IsReachable);

        var failedAudits =
            await query.CountAsync(
                audit =>
                    !audit.IsReachable);

        var averageResponseTime =
            totalAudits > 0
                ? (long)Math.Round(
                    await query.AverageAsync(
                        audit =>
                            (double)audit.ResponseTimeMs))
                : 0;

        return new AuditStatisticsDto
        {
            TotalAudits =
                totalAudits,

            SuccessfulAudits =
                successfulAudits,

            FailedAudits =
                failedAudits,

            AverageResponseTimeMs =
                averageResponseTime
        };
    }


    public async Task<SubscriptionDto?> UpgradeSubscriptionAsync(
    int userId,
    string planName)
    {
        if (string.IsNullOrWhiteSpace(planName))
        {
            return null;
        }

        var planKey = PlanDefinitions.Plans.Keys
            .FirstOrDefault(x =>
                string.Equals(
                    x,
                    planName.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (planKey == null)
        {
            return null;
        }

        var plan = PlanDefinitions.Plans[planKey];

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                UserId = userId
            };

            _context.Subscriptions.Add(subscription);
        }

        subscription.Plan = plan.Name;
        subscription.MonthlyPrice = plan.MonthlyPrice;
        subscription.MonthlyAuditLimit = plan.MonthlyAuditLimit;
        subscription.StartDate = DateTime.UtcNow;
        subscription.EndDate = null;
        subscription.IsActive = true;
        subscription.Status = "Active";
        subscription.UpdatedAt = DateTime.UtcNow;

        if (subscription.CreatedAt == default)
        {
            subscription.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _context.Entry(subscription).State = EntityState.Detached;

        return await GetSubscriptionUsageAsync(userId);
    }
}