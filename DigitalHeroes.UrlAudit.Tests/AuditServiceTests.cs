using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuditServiceTests
{
    [Fact]
    public async Task AuditUrlAsync_WhenRequestTimesOut_ReturnsFailure()
    {
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        using var httpClient = new HttpClient(handler.Object);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        var seoAuditService = new SeoAuditService();

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            seoAuditService);

        var result = await service.AuditUrlAsync("https://example.com",1);

        Assert.False(result.Success);
        Assert.Equal("https://example.com", result.Url);
        Assert.Contains("Request timed out", result.Message);
    }

    [Fact]
    public async Task AuditUrlAsync_WhenUrlIsInvalid_ReturnsFailure()
    {
        var handler = new Mock<HttpMessageHandler>();

        using var httpClient = new HttpClient(handler.Object);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        var seoAuditService = new SeoAuditService();

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            seoAuditService);

        var result = await service.AuditUrlAsync(
            "not-a-valid-url",
            1);

        Assert.False(result.Success);
        Assert.Equal("not-a-valid-url", result.Url);
        Assert.Equal(0, result.StatusCode);
        Assert.False(result.IsReachable);
        Assert.Equal(
            "Please enter a valid HTTP or HTTPS URL.",
            result.Message);

        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task AuditUrlAsync_WhenUrlIsBlank_ReturnsFailure()
    {
        var handler = new Mock<HttpMessageHandler>();

        using var httpClient = new HttpClient(handler.Object);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        var seoAuditService = new SeoAuditService();

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            seoAuditService);

        var result = await service.AuditUrlAsync(
            "   ",
            1);

        Assert.False(result.Success);
        Assert.Equal("   ", result.Url);
        Assert.Equal(0, result.StatusCode);
        Assert.False(result.IsReachable);
        Assert.Equal(
            "Website URL is required.",
            result.Message);

        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task AuditUrlAsync_WhenHttpRequestFails_ReturnsFailure()
    {
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());

        using var httpClient = new HttpClient(handler.Object);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        var seoAuditService = new SeoAuditService();

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            seoAuditService);

        var result = await service.AuditUrlAsync(
            "https://example.com",
            1);

        Assert.False(result.Success);
        Assert.Equal("https://example.com", result.Url);
        Assert.Equal(0, result.StatusCode);
        Assert.False(result.IsReachable);
        Assert.Equal(
            "Unable to reach the website.",
            result.Message);

        var history = await context.AuditHistories.SingleAsync();

        Assert.Equal("https://example.com", history.Url);
        Assert.False(history.IsReachable);
        Assert.Equal(
            "Unable to reach the website.",
            history.Message);

        var notification = await context.Notifications.SingleAsync();

        Assert.Equal("1", notification.UserId);
        Assert.Equal("Audit failed", notification.Title);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

}

