using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs.Website;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Tests;

public class WebsiteServiceTests
{
    private static (UrlAuditDbContext Context, WebsiteService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new UrlAuditDbContext(options);
        var service = new WebsiteService(context);

        return (context, service);
    }

    [Fact]
    public async Task AddAsync_WithNewWebsite_CreatesWebsite()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var result = await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "Example",
                    Url = "https://example.com/"
                });

            Assert.NotNull(result);
            Assert.Equal("Example", result.Name);
            Assert.Equal("https://example.com", result.Url);
            Assert.True(result.IsActive);

            Assert.Equal(1, await context.Websites.CountAsync());
        }
    }

    [Fact]
    public async Task AddAsync_WithDuplicateUrlForSameUser_ReturnsExistingWebsite()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var first = await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "Example",
                    Url = "https://example.com/"
                });

            var second = await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "Example Updated",
                    Url = "https://example.com"
                });

            Assert.Equal(first.Id, second.Id);
            Assert.Equal("Example", second.Name);
            Assert.Equal(1, await context.Websites.CountAsync());
        }
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyWebsitesForUser()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "User One",
                    Url = "https://one.example.com"
                });

            await service.AddAsync(
                2,
                new CreateWebsiteDto
                {
                    Name = "User Two",
                    Url = "https://two.example.com"
                });

            var result = await service.GetAllAsync(1);

            Assert.Single(result);
            Assert.Equal("User One", result[0].Name);
        }
    }

    [Fact]
    public async Task GetByIdAsync_WhenWebsiteBelongsToAnotherUser_ReturnsNull()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var website = await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "Private Website",
                    Url = "https://private.example.com"
                });

            var result = await service.GetByIdAsync(2, website.Id);

            Assert.Null(result);
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenWebsiteBelongsToUser_DeletesWebsite()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var website = await service.AddAsync(
                1,
                new CreateWebsiteDto
                {
                    Name = "Delete Me",
                    Url = "https://delete.example.com"
                });

            await service.DeleteAsync(1, website.Id);

            Assert.Equal(0, await context.Websites.CountAsync());
        }
    }
}
