using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.DTOs.Website;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigitalHeroes.UrlAudit.Tests;

public class WebsiteControllerTests
{
    private static WebsiteController CreateController(
        IWebsiteService service,
        int userId = 1)
    {
        var controller = new WebsiteController(service);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            "TestAuthentication");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    [Fact]
    public async Task Add_ReturnsOk()
    {
        var service = new FakeWebsiteService
        {
            AddResult = new WebsiteDto
            {
                Id = 1,
                Name = "Example",
                Url = "https://example.com",
                IsActive = true
            }
        };

        var controller = CreateController(service);

        var dto = new CreateWebsiteDto
        {
            Name = "Example",
            Url = "https://example.com"
        };

        var result = await controller.Add(dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var website = Assert.IsType<WebsiteDto>(okResult.Value);

        Assert.Equal(1, website.Id);
        Assert.Equal("Example", website.Name);
        Assert.Equal("https://example.com", website.Url);
        Assert.Equal(1, service.AddUserId);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var service = new FakeWebsiteService
        {
            GetAllResult = new List<WebsiteDto>
            {
                new()
                {
                    Id = 1,
                    Name = "Example",
                    Url = "https://example.com",
                    IsActive = true
                }
            }
        };

        var controller = CreateController(service);

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var websites = Assert.IsType<List<WebsiteDto>>(okResult.Value);

        Assert.Single(websites);
        Assert.Equal("Example", websites[0].Name);
        Assert.Equal(1, service.GetAllUserId);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var service = new FakeWebsiteService
        {
            GetByIdResult = new WebsiteDto
            {
                Id = 5,
                Name = "Example",
                Url = "https://example.com",
                IsActive = true
            }
        };

        var controller = CreateController(service);

        var result = await controller.GetById(5);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var website = Assert.IsType<WebsiteDto>(okResult.Value);

        Assert.Equal(5, website.Id);
        Assert.Equal("Example", website.Name);
        Assert.Equal(5, service.GetByIdId);
        Assert.Equal(1, service.GetByIdUserId);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        var service = new FakeWebsiteService
        {
            GetByIdResult = null
        };

        var controller = CreateController(service);

        var result = await controller.GetById(99);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.NotNull(notFoundResult.Value);
        Assert.Equal(99, service.GetByIdId);
    }

    [Fact]
    public async Task Update_WhenFound_ReturnsOk()
    {
        var service = new FakeWebsiteService
        {
            UpdateResult = new WebsiteDto
            {
                Id = 5,
                Name = "Updated",
                Url = "https://updated.example.com",
                IsActive = false
            }
        };

        var controller = CreateController(service);

        var dto = new UpdateWebsiteDto
        {
            Name = "Updated",
            Url = "https://updated.example.com",
            IsActive = false
        };

        var result = await controller.Update(5, dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var website = Assert.IsType<WebsiteDto>(okResult.Value);

        Assert.Equal(5, website.Id);
        Assert.Equal("Updated", website.Name);
        Assert.False(website.IsActive);

        Assert.Equal(1, service.UpdateUserId);
        Assert.Equal(5, service.UpdateId);
        Assert.Same(dto, service.UpdateDto);
    }

    [Fact]
    public async Task Update_WhenMissing_ReturnsNotFound()
    {
        var service = new FakeWebsiteService
        {
            UpdateResult = null
        };

        var controller = CreateController(service);

        var dto = new UpdateWebsiteDto
        {
            Name = "Updated",
            Url = "https://updated.example.com",
            IsActive = true
        };

        var result = await controller.Update(99, dto);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.NotNull(notFoundResult.Value);
        Assert.Equal(99, service.UpdateId);
    }

    [Fact]
    public async Task Delete_WhenWebsiteExists_ReturnsOk()
    {
        var service = new FakeWebsiteService
        {
            GetByIdResult = new WebsiteDto
            {
                Id = 5,
                Name = "Example",
                Url = "https://example.com",
                IsActive = true
            }
        };

        var controller = CreateController(service);

        var result = await controller.Delete(5);

        var okResult = Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);
        Assert.True(service.DeleteCalled);
        Assert.Equal(1, service.DeleteUserId);
        Assert.Equal(5, service.DeleteId);
    }

    [Fact]
    public async Task Delete_WhenWebsiteMissing_ReturnsNotFound()
    {
        var service = new FakeWebsiteService
        {
            GetByIdResult = null
        };

        var controller = CreateController(service);

        var result = await controller.Delete(99);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.NotNull(notFoundResult.Value);
        Assert.False(service.DeleteCalled);
        Assert.Equal(99, service.GetByIdId);
    }

    private sealed class FakeWebsiteService : IWebsiteService
    {
        public WebsiteDto? AddResult { get; set; }
        public List<WebsiteDto> GetAllResult { get; set; } = new();
        public WebsiteDto? GetByIdResult { get; set; }
        public WebsiteDto? UpdateResult { get; set; }

        public int AddUserId { get; private set; }
        public int GetAllUserId { get; private set; }
        public int GetByIdUserId { get; private set; }
        public int GetByIdId { get; private set; }

        public int UpdateUserId { get; private set; }
        public int UpdateId { get; private set; }
        public UpdateWebsiteDto? UpdateDto { get; private set; }

        public bool DeleteCalled { get; private set; }
        public int DeleteUserId { get; private set; }
        public int DeleteId { get; private set; }

        public Task<WebsiteDto> AddAsync(
            int userId,
            CreateWebsiteDto dto)
        {
            AddUserId = userId;

            return Task.FromResult(
                AddResult ?? new WebsiteDto());
        }

        public Task<List<WebsiteDto>> GetAllAsync(
            int userId)
        {
            GetAllUserId = userId;

            return Task.FromResult(GetAllResult);
        }

        public Task<WebsiteDto?> GetByIdAsync(
            int userId,
            int id)
        {
            GetByIdUserId = userId;
            GetByIdId = id;

            return Task.FromResult(GetByIdResult);
        }

        public Task<WebsiteDto?> UpdateAsync(
            int userId,
            int id,
            UpdateWebsiteDto dto)
        {
            UpdateUserId = userId;
            UpdateId = id;
            UpdateDto = dto;

            return Task.FromResult(UpdateResult);
        }

        public Task DeleteAsync(
            int userId,
            int id)
        {
            DeleteCalled = true;
            DeleteUserId = userId;
            DeleteId = id;

            return Task.CompletedTask;
        }
    }
}
