using System.Net;
using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using DigitalHeroes.UrlAudit.Api.Data;
using Xunit;

namespace DigitalHeroes.UrlAudit.Tests
{
    public class AuditServiceTests
    {
        [Fact]
        public async Task AuditUrlAsync_Should_ReturnSuccess_WhenUrlIsReachable()
        {
            // Arrange
            var fakeHandler =
                new FakeHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(fakeHandler);

            var cache = new MemoryCache(new MemoryCacheOptions());

            var logger = NullLogger<AuditService>.Instance;

            var auditSettings = Options.Create(new AuditSettings
            {
                TimeoutSeconds = 10,
                CacheDurationMinutes = 5
            });

            var dbOptions = new DbContextOptionsBuilder<UrlAuditDbContext>()
    .UseInMemoryDatabase("AuditTestDb")
    .Options;

            var context = new UrlAuditDbContext(dbOptions);

            var service = new AuditService(
                          httpClient,
                          cache,
                          logger,
                          auditSettings,
                          context);

            // Act
            var result =
                await service.AuditUrlAsync("https://google.com");

            // Assert
            Assert.True(result.Success);
            Assert.True(result.IsReachable);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task AuditUrlAsync_Should_ReturnCachedResult()
        {
            // Arrange
            var fakeHandler =
                new FakeHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(fakeHandler);

            var cache = new MemoryCache(new MemoryCacheOptions());

            var logger = NullLogger<AuditService>.Instance;

            var auditSettings = Options.Create(new AuditSettings
            {
                TimeoutSeconds = 10,
                CacheDurationMinutes = 5
            });

        }
    }

     
}