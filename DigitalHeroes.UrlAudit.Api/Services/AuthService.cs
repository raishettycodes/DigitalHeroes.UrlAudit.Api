using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.DTOs.Auth;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DigitalHeroes.UrlAudit.Api.Helpers;

namespace DigitalHeroes.UrlAudit.Api.Services
{
    
    public class AuthService : IAuthService
    {
        private readonly UrlAuditDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new();
        private readonly JwtTokenGenerator _jwtGenerator;
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
            Console.WriteLine("Stored Hash:");
            Console.WriteLine(user.PasswordHash);

            Console.WriteLine("Entered Password:");
            Console.WriteLine(request.Password);

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            Console.WriteLine("Verify Result: " + result);

            //var result = _passwordHasher.VerifyHashedPassword(
            //    user,
            //    user.PasswordHash,
            //    request.Password);

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

        public AuthService(
    UrlAuditDbContext context,
    JwtTokenGenerator jwtGenerator)
        {
            _context = context;
            _jwtGenerator = jwtGenerator;
        }
    }
}