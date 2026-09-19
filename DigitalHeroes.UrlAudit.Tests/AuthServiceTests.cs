using DigitalHeroes.UrlAudit.Api.Configuration;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Helpers;
using DigitalHeroes.UrlAudit.Api.Models;
using DigitalHeroes.UrlAudit.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuthServiceTests
{
    private static (UrlAuditDbContext Context, AuthService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<UrlAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new UrlAuditDbContext(options);

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = "ThisIsATestJwtSecretKey12345678901234567890",
            Issuer = "DigitalHeroes",
            Audience = "DigitalHeroesUsers"
        });

        var jwtGenerator = new JwtTokenGenerator(jwtSettings);

        var service = new AuthService(context, jwtGenerator);

        return (context, service);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_ReturnsTrue()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var request = new RegisterRequestDto
            {
                FullName = "Test User",
                Email = "test@example.com",
                Password = "Test@12345"
            };

            var result = await service.RegisterAsync(request);

            Assert.True(result);

            var user = await context.Users
                .SingleOrDefaultAsync(x => x.Email == "test@example.com");

            Assert.NotNull(user);
            Assert.Equal("Test User", user.FullName);
            Assert.True(user.IsActive);
            Assert.NotEqual("Test@12345", user.PasswordHash);
        }
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFalse()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var request = new RegisterRequestDto
            {
                FullName = "Test User",
                Email = "test@example.com",
                Password = "Test@12345"
            };

            var firstResult = await service.RegisterAsync(request);
            var secondResult = await service.RegisterAsync(request);

            Assert.True(firstResult);
            Assert.False(secondResult);

            Assert.Equal(
                1,
                await context.Users.CountAsync(x => x.Email == "test@example.com"));
        }
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            await service.RegisterAsync(new RegisterRequestDto
            {
                FullName = "Login User",
                Email = "login@example.com",
                Password = "Test@12345"
            });

            var result = await service.LoginAsync(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = "Test@12345"
            });

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
            Assert.Equal("Login User", result.FullName);
            Assert.Equal("login@example.com", result.Email);
        }
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            await service.RegisterAsync(new RegisterRequestDto
            {
                FullName = "Login User",
                Email = "login@example.com",
                Password = "Test@12345"
            });

            var result = await service.LoginAsync(new LoginRequestDto
            {
                Email = "login@example.com",
                Password = "WrongPassword"
            });

            Assert.Null(result);
        }
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsNull()
    {
        var (context, service) = CreateService();
        await using (context)
        {
            var result = await service.LoginAsync(new LoginRequestDto
            {
                Email = "unknown@example.com",
                Password = "Test@12345"
            });

            Assert.Null(result);
        }
    }
}
