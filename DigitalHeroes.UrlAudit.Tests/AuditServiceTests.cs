using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Api.Models;
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
    [Fact]
    public async Task GetMonthlyAuditCountAsync_CountsOnlyReachableAuditsForUser()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        context.Websites.AddRange(
            new Website
            {
                Id = 1,
                Name = "Website One",
                Url = "https://example1.com",
                UserId = 1
            },
            new Website
            {
                Id = 2,
                Name = "Website Two",
                Url = "https://example2.com",
                UserId = 2
            });

        context.AuditHistories.AddRange(
            new AuditHistory
            {
                WebsiteId = 1,
                Url = "https://example1.com",
                IsReachable = true,
                CreatedAt = DateTime.UtcNow,
                StatusCode = 200
            },
            new AuditHistory
            {
                WebsiteId = 1,
                Url = "https://example1.com",
                IsReachable = false,
                CreatedAt = DateTime.UtcNow,
                StatusCode = 0
            },
            new AuditHistory
            {
                WebsiteId = 2,
                Url = "https://example2.com",
                IsReachable = true,
                CreatedAt = DateTime.UtcNow,
                StatusCode = 200
            });

        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            new SeoAuditService());

        // Act
        var count = await service.GetMonthlyAuditCountAsync(1);

        // Assert
        Assert.Equal(1, count);
    }


    [Fact]
    public async Task AuditUrlAsync_WhenMonthlyLimitReached_ReturnsFailureWithoutSendingRequest()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new UrlAuditDbContext(dbOptions);

        var userId = 1;
        var websiteId = 1;

        context.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Plan = "Free",
            MonthlyAuditLimit = 100,
            MonthlyPrice = 0,
            IsActive = true,
            Status = "Active",
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.Websites.Add(new Website
        {
            Id = websiteId,
            Name = "Test Website",
            Url = "https://example.com",
            UserId = userId
        });

        var currentMonthAudits = Enumerable.Range(1, 100)
            .Select(_ => new AuditHistory
            {
                WebsiteId = websiteId,
                Url = "https://example.com",
                IsReachable = true,
                CreatedAt = DateTime.UtcNow,
                StatusCode = 200
            });

        context.AuditHistories.AddRange(currentMonthAudits);

        await context.SaveChangesAsync();

        var handler = new Mock<HttpMessageHandler>();

        using var httpClient = new HttpClient(handler.Object);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new AuditSettings
        {
            TimeoutSeconds = 5,
            CacheDurationMinutes = 5
        });

        var service = new AuditService(
            httpClient,
            cache,
            NullLogger<AuditService>.Instance,
            options,
            context,
            new SeoAuditService());

        // Act
        var result = await service.AuditUrlAsync(
            "https://new-example.com",
            userId);

        // Assert
        Assert.False(result.Success);

        Assert.Contains(
            "Monthly audit limit of 100 has been reached.",
            result.Message);

        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        Assert.Equal(
            100,
            await context.AuditHistories.CountAsync());
    }


}

