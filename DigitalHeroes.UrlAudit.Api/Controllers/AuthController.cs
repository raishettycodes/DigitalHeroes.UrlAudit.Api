using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DigitalHeroes.UrlAudit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            var result = await _authService.RegisterAsync(request);

            if (!result)
                return BadRequest("Email already exists.");

            return Ok("Registration Successful");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);

            if (result == null)
                return Unauthorized();

            return Ok(result);
        }
    }
}