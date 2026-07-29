using System.Net;
using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

            var service = new AuditService(
                httpClient,
                cache,
                logger,
                auditSettings);

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

            var service = new AuditService(
                httpClient,
                cache,
                logger,
                auditSettings);

            // Act
            var first =
                await service.AuditUrlAsync("https://google.com");

            var second =
                await service.AuditUrlAsync("https://google.com");

            // Assert
            Assert.True(second.Success);
            Assert.Equal(first.StatusCode, second.StatusCode);
            Assert.Equal(first.Url, second.Url);
        }

        [Fact]
        public async Task AuditUrlAsync_Should_ReturnTimeoutMessage()
        {
            // Arrange
            var httpClient =
                new HttpClient(new TimeoutHttpMessageHandler());

            var cache = new MemoryCache(new MemoryCacheOptions());

            var logger = NullLogger<AuditService>.Instance;

            var auditSettings = Options.Create(new AuditSettings
            {
                TimeoutSeconds = 10,
                CacheDurationMinutes = 5
            });

            var service = new AuditService(
                httpClient,
                cache,
                logger,
                auditSettings);

            // Act
            var result =
                await service.AuditUrlAsync("https://google.com");

            // Assert
            Assert.False(result.Success);
            Assert.Equal(
                "Request timed out after 10 seconds",
                result.Message);
        }
    }
}