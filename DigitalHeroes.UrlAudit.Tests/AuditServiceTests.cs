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
}