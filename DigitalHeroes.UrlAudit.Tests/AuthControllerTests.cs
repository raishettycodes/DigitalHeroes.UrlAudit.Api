using DigitalHeroes.UrlAudit.Api.Controllers;
using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DigitalHeroes.UrlAudit.Tests;

public class AuthControllerTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "DigitalHeroes.UrlAudit.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
    private static IWebHostEnvironment CreateDevelopmentEnvironment()
    {
        return new FakeWebHostEnvironment();
    }
    private sealed class FakeAuthService : IAuthService
    {
        public bool RegisterResult { get; set; }

        public LoginResponseDto? LoginResult { get; set; }

        public RegisterRequestDto? RegisteredRequest { get; private set; }

        public LoginRequestDto? LoginRequest { get; private set; }

        public string? PasswordResetTokenResult { get; set; }

        public bool ResetPasswordResult { get; set; }

        public ForgotPasswordRequestDto? ForgotPasswordRequest { get; private set; }

        public ResetPasswordRequestDto? ResetPasswordRequest { get; private set; }

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
        public Task<string?> CreatePasswordResetTokenAsync(
    ForgotPasswordRequestDto request)
        {
            ForgotPasswordRequest = request;

            return Task.FromResult(PasswordResetTokenResult);
        }

        public Task<bool> ResetPasswordAsync(
            ResetPasswordRequestDto request)
        {
            ResetPasswordRequest = request;

            return Task.FromResult(ResetPasswordResult);
        }
    }

    [Fact]
    public async Task Register_WhenRegistrationSucceeds_ReturnsOk()
    {
        var fakeService = new FakeAuthService
        {
            RegisterResult = true
        };

        var controller = new AuthController( fakeService, CreateDevelopmentEnvironment());

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

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

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

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

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

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

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

    [Fact]
    public async Task ForgotPassword_WhenTokenIsGenerated_ReturnsOk()
    {
        var fakeService = new FakeAuthService
        {
            PasswordResetTokenResult = "test-reset-token"
        };

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

        var request =
            new ForgotPasswordRequestDto
            {
                Email = "test@example.com"
            };

        var result =
            await controller.ForgotPassword(request);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        Assert.NotNull(
            fakeService.ForgotPasswordRequest);

        Assert.Equal(
            "test@example.com",
            fakeService.ForgotPasswordRequest!.Email);
    }

    [Fact]
    public async Task ForgotPassword_WhenEmailDoesNotExist_ReturnsOk()
    {
        var fakeService = new FakeAuthService
        {
            PasswordResetTokenResult = null
        };

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());
        var request =
            new ForgotPasswordRequestDto
            {
                Email = "unknown@example.com"
            };

        var result =
            await controller.ForgotPassword(request);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        Assert.NotNull(
            fakeService.ForgotPasswordRequest);

        Assert.Equal(
            "unknown@example.com",
            fakeService.ForgotPasswordRequest!.Email);
    }

    [Fact]
    public async Task ResetPassword_WhenResetSucceeds_ReturnsOk()
    {
        var fakeService = new FakeAuthService
        {
            ResetPasswordResult = true
        };

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

        var request =
            new ResetPasswordRequestDto
            {
                Token = "test-reset-token",
                NewPassword = "NewTest@12345"
            };

        var result =
            await controller.ResetPassword(request);

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        Assert.NotNull(okResult.Value);

        Assert.NotNull(
            fakeService.ResetPasswordRequest);

        Assert.Equal(
            "test-reset-token",
            fakeService.ResetPasswordRequest!.Token);

        Assert.Equal(
            "NewTest@12345",
            fakeService.ResetPasswordRequest.NewPassword);
    }

    [Fact]
    public async Task ResetPassword_WhenResetFails_ReturnsBadRequest()
    {
        var fakeService = new FakeAuthService
        {
            ResetPasswordResult = false
        };

        var controller = new AuthController(fakeService, CreateDevelopmentEnvironment());

        var request =
            new ResetPasswordRequestDto
            {
                Token = "invalid-token",
                NewPassword = "NewTest@12345"
            };

        var result =
            await controller.ResetPassword(request);

        var badRequest =
            Assert.IsType<BadRequestObjectResult>(result);

        Assert.NotNull(badRequest.Value);
    }

}