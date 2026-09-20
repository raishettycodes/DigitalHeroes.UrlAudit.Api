using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuditHistoryServiceTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UrlAuditDbContext(options);
    }

    [Fact]
    public async Task GetWebsiteHistoryAsync_ReturnsOnlyRequestedWebsiteHistory()
    {
        await using var context = CreateContext();

        context.AuditHistories.AddRange(
            new AuditHistory
            {
                WebsiteId = 1,
                Url = "https://example.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new AuditHistory
            {
                WebsiteId = 2,
                Url = "https://other.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });

        await context.SaveChangesAsync();

        var service = new AuditHistoryService(context);

        var result =
            await service.GetWebsiteHistoryAsync(1);

        Assert.Single(result);
        Assert.Equal(
            "https://example.com",
            result[0].Url);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public async Task GetWebsiteHistoryAsync_ReturnsHistoryOrderedByCreatedAtDescending()
    {
        await using var context = CreateContext();

        var older = new AuditHistory
        {
            WebsiteId = 1,
            Url = "https://older.com",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        var newer = new AuditHistory
        {
            WebsiteId = 1,
            Url = "https://newer.com",
            CreatedAt = DateTime.UtcNow
        };

        context.AuditHistories.AddRange(older, newer);

        await context.SaveChangesAsync();

        var service = new AuditHistoryService(context);

        var result =
            await service.GetWebsiteHistoryAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://newer.com", result[0].Url);
        Assert.Equal("https://older.com", result[1].Url);
    }

    [Fact]
    public async Task GetWebsiteHistoryAsync_MapsAuditHistoryFieldsToDto()
    {
        await using var context = CreateContext();

        var history = new AuditHistory
        {
            WebsiteId = 1,
            Url = "https://example.com",
            StatusCode = 200,
            ResponseTimeMs = 1250,
            IsReachable = true,
            Message = "Audit completed",

            HttpVersion = "HTTP/2",
            Server = "nginx",
            ContentType = "text/html",
            ContentLength = 50000,
            IsRedirect = false,
            RedirectLocation = null,
            IsSslValid = true,

            Title = "Example Website",
            MetaDescription = "Example description",
            H1Count = 1,
            H2Count = 3,
            Images = 10,
            ImagesWithoutAlt = 2,
            SeoScore = 85,

            CreatedAt = DateTime.UtcNow
        };

        context.AuditHistories.Add(history);

        await context.SaveChangesAsync();

        var service = new AuditHistoryService(context);

        var result =
            await service.GetWebsiteHistoryAsync(1);

        var dto = Assert.Single(result);

        Assert.Equal(history.Id, dto.Id);
        Assert.Equal(history.CreatedAt, dto.CreatedAt);
        Assert.Equal(history.Url, dto.Url);
        Assert.Equal(history.StatusCode, dto.StatusCode);
        Assert.Equal(history.ResponseTimeMs, dto.ResponseTimeMs);
        Assert.Equal(history.IsReachable, dto.IsReachable);
        Assert.Equal(history.Message, dto.Message);

        Assert.Equal(history.HttpVersion, dto.HttpVersion);
        Assert.Equal(history.Server, dto.Server);
        Assert.Equal(history.ContentType, dto.ContentType);
        Assert.Equal(history.ContentLength, dto.ContentLength);
        Assert.Equal(history.IsRedirect, dto.IsRedirect);
        Assert.Equal(history.RedirectLocation, dto.RedirectLocation);
        Assert.Equal(history.IsSslValid, dto.IsSslValid);

        Assert.Equal(history.Title, dto.Title);
        Assert.Equal(history.MetaDescription, dto.MetaDescription);
        Assert.Equal(history.H1Count, dto.H1Count);
        Assert.Equal(history.H2Count, dto.H2Count);
        Assert.Equal(history.Images, dto.Images);
        Assert.Equal(history.ImagesWithoutAlt, dto.ImagesWithoutAlt);
        Assert.Equal(history.SeoScore, dto.SeoScore);
    }

    [Fact]
    public async Task GetWebsiteHistoryAsync_WhenNoHistoryExists_ReturnsEmptyList()
    {
        await using var context = CreateContext();

        var service = new AuditHistoryService(context);

        var result =
            await service.GetWebsiteHistoryAsync(999);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}