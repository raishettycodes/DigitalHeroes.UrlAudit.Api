using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuthControllerTests
{
    private sealed class FakeAuthService : IAuthService
    {
        public bool RegisterResult { get; set; }

        public LoginResponseDto? LoginResult { get; set; }

        public RegisterRequestDto? RegisteredRequest { get; private set; }

        public LoginRequestDto? LoginRequest { get; private set; }

        public Task<bool> RegisterAsync(
            RegisterRequestDto request)
        {
            RegisteredRequest = request;

            return Task.FromResult(RegisterResult);
        }

        public Task<LoginResponseDto?> LoginAsync(
            LoginRequestDto request)
        {
            LoginRequest = request;

            return Task.FromResult(LoginResult);
        }
    }

    [Fact]
    public async Task Register_WhenRegistrationSucceeds_ReturnsOk()
    {
        var fakeService = new FakeAuthService
        {
            RegisterResult = true
        };

        var controller =
            new AuthController(fakeService);

        var request =
            new RegisterRequestDto
            {
                FullName = "Test User",
                Email = "test@example.com",
                Password = "Test@12345"
            };

        var result =
            await controller.Register(request);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.Equal(
            "Registration Successful",
            okResult.Value);

        Assert.NotNull(
            fakeService.RegisteredRequest);

        Assert.Equal(
            "test@example.com",
            fakeService.RegisteredRequest!.Email);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsBadRequest()
    {
        var fakeService = new FakeAuthService
        {
            RegisterResult = false
        };

        var controller =
            new AuthController(fakeService);

        var request =
            new RegisterRequestDto
            {
                FullName = "Existing User",
                Email = "existing@example.com",
                Password = "Test@12345"
            };

        var result =
            await controller.Register(request);

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(
                result);

        Assert.Equal(
            "Email already exists.",
            badRequest.Value);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsOk()
    {
        var loginResponse =
            new LoginResponseDto
            {
                Token = "test-jwt-token",
                FullName = "Test User",
                Email = "test@example.com"
            };

        var fakeService = new FakeAuthService
        {
            LoginResult = loginResponse
        };

        var controller =
            new AuthController(fakeService);

        var request =
            new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "Test@12345"
            };

        var result =
            await controller.Login(request);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        var response =
            Assert.IsType<LoginResponseDto>(
                okResult.Value);

        Assert.Equal(
            "test-jwt-token",
            response.Token);

        Assert.Equal(
            "Test User",
            response.FullName);

        Assert.Equal(
            "test@example.com",
            response.Email);

        Assert.NotNull(
            fakeService.LoginRequest);

        Assert.Equal(
            "test@example.com",
            fakeService.LoginRequest!.Email);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        var fakeService = new FakeAuthService
        {
            LoginResult = null
        };

        var controller =
            new AuthController(fakeService);

        var request =
            new LoginRequestDto
            {
                Email = "wrong@example.com",
                Password = "WrongPassword"
            };

        var result =
            await controller.Login(request);

        Assert.IsType<UnauthorizedResult>(
            result);
    }
}