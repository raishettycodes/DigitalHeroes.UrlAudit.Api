using DigitalHeroes.UrlAudit.Api.DTOs.Auth;

namespace DigitalHeroes.UrlAudit.Api.Interfaces
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(RegisterRequestDto request);

        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);

        Task<string?> CreatePasswordResetTokenAsync(
            ForgotPasswordRequestDto request);

        Task<bool> ResetPasswordAsync(
            ResetPasswordRequestDto request);
    }
}