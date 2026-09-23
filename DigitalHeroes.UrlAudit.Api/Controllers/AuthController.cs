using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;

namespace DigitalHeroes.UrlAudit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _environment;

        public AuthController(
    IAuthService authService,
    IWebHostEnvironment environment)
        {
            _authService = authService;
            _environment = environment;
        }

        [HttpPost("register")]
        [EnableRateLimiting("FixedPolicy")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            var result = await _authService.RegisterAsync(request);

            if (!result)
                return BadRequest("Email already exists.");

            return Ok("Registration Successful");
        }

        [HttpPost("login")]
        [EnableRateLimiting("FixedPolicy")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);

            if (result == null)
                return Unauthorized();

            return Ok(result);
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("FixedPolicy")]
        public async Task<IActionResult> ForgotPassword(
     ForgotPasswordRequestDto request)
        {
            var token = await _authService.CreatePasswordResetTokenAsync(request);

            // Production-safe generic response.
            var response = new
            {
                message = "If an account exists for this email, a password reset link has been generated."
            };

            // Local development only:
            // expose the token so we can test the complete reset flow
            if (token != null && _environment.IsDevelopment())
            {
                return Ok(new
                {
                    response.message,
                    resetToken = token
                });
            }

            return Ok(response);
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("FixedPolicy")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordRequestDto request)
        {
            var result = await _authService.ResetPasswordAsync(request);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "The password reset link is invalid or has expired."
                });
            }

            return Ok(new
            {
                message = "Password reset successful."
            });
        }
    }
}