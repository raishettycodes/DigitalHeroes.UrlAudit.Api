using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DigitalHeroes.UrlAudit.Api.Helpers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    
    public class AuthService : IAuthService
    {
        private readonly UrlAuditDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new();
        private readonly JwtTokenGenerator _jwtGenerator;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        public async Task<bool> RegisterAsync(RegisterRequestDto request)
        {
            // Check if email already exists
            if (_context.Users.Any(x => x.Email == request.Email))
            {
                return false;
            }

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                CreatedOn = DateTime.UtcNow,
                IsActive = true
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return true;
        }
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
                return null;


            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            if (result == PasswordVerificationResult.Failed)
                return null;

            var token = _jwtGenerator.GenerateToken(user);

            return new LoginResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email
            };
        }
        public async Task<string?> CreatePasswordResetTokenAsync(
    ForgotPasswordRequestDto request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            // Do not reveal whether the email exists.
            if (user == null)
                return null;

            // Invalidate any previous unused tokens for this user.
            var existingTokens = await _context.PasswordResetTokens
                .Where(x => x.UserId == user.Id && !x.IsUsed)
                .ToListAsync();

            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
            }

            var rawToken = WebEncoders.Base64UrlEncode(
                RandomNumberGenerator.GetBytes(32));

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(rawToken)));

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);

            await _context.SaveChangesAsync();

            var frontendBaseUrl =
                _configuration["PasswordReset:FrontendBaseUrl"];

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                throw new InvalidOperationException(
                    "PasswordReset:FrontendBaseUrl is not configured.");
            }

            var resetLink =
                $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            _logger.LogInformation(
    "===== AUTH SERVICE CALLING EMAIL SERVICE FOR {Email} =====",
    user.Email);
            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                resetLink);

            return rawToken;
        }
        public async Task<bool> ResetPasswordAsync(
            ResetPasswordRequestDto request)
        {
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(request.Token)));

            var resetToken = await _context.PasswordResetTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    !x.IsUsed);

            if (resetToken == null)
                return false;

            if (resetToken.ExpiresAt <= DateTime.UtcNow)
                return false;

            resetToken.User.PasswordHash =
                _passwordHasher.HashPassword(
                    resetToken.User,
                    request.NewPassword);

            resetToken.IsUsed = true;

            await _context.SaveChangesAsync();

            return true;
        }
        public AuthService(
     UrlAuditDbContext context,
     JwtTokenGenerator jwtGenerator,
     IEmailService emailService,
     IConfiguration configuration,
     ILogger<AuthService> logger)
        {
            _context = context;
            _jwtGenerator = jwtGenerator;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }
    }
}