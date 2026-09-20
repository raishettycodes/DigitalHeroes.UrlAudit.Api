using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.DTOs;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuditHistoryControllerTests
{
    private static UrlAuditDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<UrlAuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        return new UrlAuditDbContext(options);
    }

    private static AuditHistoryController CreateController(
        UrlAuditDbContext context,
        int? userId = null)
    {
        var controller =
            new AuditHistoryController(context);

        ClaimsIdentity identity;

        if (userId.HasValue)
        {
            identity =
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            userId.Value.ToString())
                    },
                    "TestAuthentication");
        }
        else
        {
            identity =
                new ClaimsIdentity(
                    authenticationType: "TestAuthentication");
        }

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext
                    {
                        User =
                            new ClaimsPrincipal(identity)
                    }
            };

        return controller;
    }

    private static async Task SeedWebsiteAsync(
        UrlAuditDbContext context,
        int websiteId,
        int userId,
        string url)
    {
        context.Websites.Add(
            new Website
            {
                Id = websiteId,
                UserId = userId,
                Url = url
            });

        await context.SaveChangesAsync();
    }

    private static AuditHistory CreateAudit(
        int websiteId,
        string url,
        bool isReachable,
        int statusCode,
        int responseTimeMs,
        DateTime createdAt,
        string message = "Audit completed")
    {
        return new AuditHistory
        {
            WebsiteId = websiteId,
            Url = url,
            IsReachable = isReachable,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            CreatedAt = createdAt,
            Message = message,
            IsSslValid = true,
            SeoScore = 85
        };
    }

    [Fact]
    public async Task GetHistory_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateController(context);

        var result =
            await controller.GetHistory();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetHistory_ReturnsOnlyCurrentUsersHistory()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://user1.com");

        await SeedWebsiteAsync(
            context,
            2,
            2,
            "https://user2.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://user1.com",
                true,
                200,
                500,
                DateTime.UtcNow),
            CreateAudit(
                2,
                "https://user2.com",
                true,
                200,
                300,
                DateTime.UtcNow));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory();

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(1, response.TotalRecords);
        Assert.Single(response.Items);
        Assert.Equal(
            "https://user1.com",
            response.Items[0].Url);
    }

    [Fact]
    public async Task GetHistory_SearchFiltersResults()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://example.com",
                true,
                200,
                500,
                DateTime.UtcNow,
                "Audit completed"),
            CreateAudit(
                1,
                "https://other.com",
                false,
                500,
                900,
                DateTime.UtcNow.AddMinutes(-1),
                "Server error"));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                search: "other.com");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Single(response.Items);
        Assert.Equal(
            "https://other.com",
            response.Items[0].Url);
    }

    [Fact]
    public async Task GetHistory_SuccessfulStatusReturnsOnlyReachableAudits()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://success.com",
                true,
                200,
                400,
                DateTime.UtcNow),
            CreateAudit(
                1,
                "https://failed.com",
                false,
                500,
                800,
                DateTime.UtcNow.AddMinutes(-1)));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                status: "successful");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Single(response.Items);
        Assert.True(response.Items[0].IsReachable);
    }

    [Fact]
    public async Task GetHistory_FailedStatusReturnsOnlyUnreachableAudits()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://success.com",
                true,
                200,
                400,
                DateTime.UtcNow),
            CreateAudit(
                1,
                "https://failed.com",
                false,
                500,
                800,
                DateTime.UtcNow.AddMinutes(-1)));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                status: "FAILED");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Single(response.Items);
        Assert.False(response.Items[0].IsReachable);
    }

    [Fact]
    public async Task GetHistory_OldestSortReturnsOldestFirst()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://newer.com",
                true,
                200,
                500,
                DateTime.UtcNow),
            CreateAudit(
                1,
                "https://older.com",
                true,
                200,
                500,
                DateTime.UtcNow.AddHours(-1)));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                sort: "oldest");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(
            "https://older.com",
            response.Items[0].Url);
    }

    [Fact]
    public async Task GetHistory_FastestSortReturnsFastestFirst()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://slow.com",
                true,
                200,
                1000,
                DateTime.UtcNow),
            CreateAudit(
                1,
                "https://fast.com",
                true,
                200,
                100,
                DateTime.UtcNow.AddMinutes(-1)));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                sort: "fastest");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(
            "https://fast.com",
            response.Items[0].Url);
    }

    [Fact]
    public async Task GetHistory_SlowestSortReturnsSlowestFirst()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://fast.com",
                true,
                200,
                100,
                DateTime.UtcNow),
            CreateAudit(
                1,
                "https://slow.com",
                true,
                200,
                1000,
                DateTime.UtcNow.AddMinutes(-1)));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                sort: "slowest");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(
            "https://slow.com",
            response.Items[0].Url);
    }

    [Fact]
    public async Task GetHistory_AppliesPagination()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        for (var i = 1; i <= 5; i++)
        {
            context.AuditHistories.Add(
                CreateAudit(
                    1,
                    $"https://site{i}.com",
                    true,
                    200,
                    i * 100,
                    DateTime.UtcNow.AddMinutes(-i)));
        }

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                page: 2,
                pageSize: 2);

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(5, response.TotalRecords);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(2, response.Items.Count);
    }

    [Fact]
    public async Task GetHistory_ClampsInvalidPageAndPageSize()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.Add(
            CreateAudit(
                1,
                "https://example.com",
                true,
                200,
                500,
                DateTime.UtcNow));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetHistory(
                page: 0,
                pageSize: 500);

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<PagedResponseDto<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(1, response.Page);
        Assert.Equal(100, response.PageSize);
    }

    [Fact]
    public async Task GetWebsiteHistory_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateController(context);

        var result =
            await controller.GetWebsiteHistory(1);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetWebsiteHistory_WhenWebsiteDoesNotBelongToUser_ReturnsNotFound()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            2,
            "https://other-user.com");

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetWebsiteHistory(1);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetWebsiteHistory_ReturnsNewestHistoryFirst()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        context.AuditHistories.AddRange(
            CreateAudit(
                1,
                "https://older.com",
                true,
                200,
                500,
                DateTime.UtcNow.AddHours(-1)),
            CreateAudit(
                1,
                "https://newer.com",
                true,
                200,
                500,
                DateTime.UtcNow));

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetWebsiteHistory(1);

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var history =
            Assert.IsType<
                List<DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>>(
                ok.Value);

        Assert.Equal(2, history.Count);
        Assert.Equal(
            "https://newer.com",
            history[0].Url);
        Assert.Equal(
            "https://older.com",
            history[1].Url);
    }

    [Fact]
    public async Task DeleteAudit_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateController(context);

        var result =
            await controller.DeleteAudit(1);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task DeleteAudit_WhenAuditDoesNotBelongToUser_ReturnsNotFound()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            2,
            "https://other-user.com");

        var audit =
            CreateAudit(
                1,
                "https://other-user.com",
                true,
                200,
                500,
                DateTime.UtcNow);

        context.AuditHistories.Add(audit);

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.DeleteAudit(audit.Id);

        Assert.IsType<NotFoundObjectResult>(result);

        Assert.NotNull(
            await context.AuditHistories
                .FindAsync(audit.Id));
    }

    [Fact]
    public async Task DeleteAudit_WhenAuditBelongsToUser_DeletesAudit()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        var audit =
            CreateAudit(
                1,
                "https://example.com",
                true,
                200,
                500,
                DateTime.UtcNow);

        context.AuditHistories.Add(audit);

        await context.SaveChangesAsync();

        var auditId = audit.Id;

        var controller =
            CreateController(context, 1);

        var result =
            await controller.DeleteAudit(auditId);

        var ok =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(ok.Value);

        Assert.Null(
            await context.AuditHistories
                .FindAsync(auditId));
    }

    [Fact]
    public async Task GetAuditById_WhenUserIdIsMissing_ReturnsUnauthorized()
    {
        await using var context = CreateContext();

        var controller =
            CreateController(context);

        var result =
            await controller.GetAuditById(1);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetAuditById_WhenAuditDoesNotBelongToUser_ReturnsNotFound()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            2,
            "https://other-user.com");

        var audit =
            CreateAudit(
                1,
                "https://other-user.com",
                true,
                200,
                500,
                DateTime.UtcNow);

        context.AuditHistories.Add(audit);

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetAuditById(audit.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAuditById_WhenAuditBelongsToUser_ReturnsMappedDto()
    {
        await using var context = CreateContext();

        await SeedWebsiteAsync(
            context,
            1,
            1,
            "https://example.com");

        var audit =
            CreateAudit(
                1,
                "https://example.com",
                true,
                200,
                500,
                DateTime.UtcNow);

        audit.Title = "Example";
        audit.MetaDescription = "Example description";
        audit.H1Count = 1;
        audit.H2Count = 2;
        audit.Images = 5;
        audit.ImagesWithoutAlt = 1;

        context.AuditHistories.Add(audit);

        await context.SaveChangesAsync();

        var controller =
            CreateController(context, 1);

        var result =
            await controller.GetAuditById(audit.Id);

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var dto =
            Assert.IsType<
                DigitalHeroes.UrlAudit.Api.DTOs.AuditHistoryDto>(
                ok.Value);

        Assert.Equal(audit.Id, dto.Id);
        Assert.Equal(audit.WebsiteId, dto.WebsiteId);
        Assert.Equal(audit.Url, dto.Url);
        Assert.Equal(audit.StatusCode, dto.StatusCode);
        Assert.Equal(audit.ResponseTimeMs, dto.ResponseTimeMs);
        Assert.Equal(audit.IsReachable, dto.IsReachable);
        Assert.Equal(audit.Message, dto.Message);
        Assert.Equal(audit.Title, dto.Title);
        Assert.Equal(audit.MetaDescription, dto.MetaDescription);
        Assert.Equal(audit.SeoScore, dto.SeoScore);
    }
}
