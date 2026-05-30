using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TweetWebApp.Data;
using TweetWebApp.DTOs.AuthDtos;
using TweetWebApp.Models;

namespace TweetWebApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // ── POST /api/auth/register ───────────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            bool emailExists = await _db.Users
                .AnyAsync(u => u.Email == request.Email.ToLower());
            if (emailExists)
                return BadRequest("This Email is already exist !");

            bool usernameExists = await _db.Users
                .AnyAsync(u => u.Username == request.Username.Trim().ToLower());
            if (usernameExists)
                return BadRequest("This username is already exist .");

            var user = new User
            {
                Username = request.Username,
                Email = request.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var (accessToken, refreshToken) = await IssueTokensAsync(user);

            return StatusCode(201, new AuthResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = accessToken,
                RefreshToken = refreshToken
            });
        }

        // ── POST /api/auth/login ──────────────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized("Wrong Email or password, please try again!");

            var (accessToken, refreshToken) = await IssueTokensAsync(user);

            return Ok(new AuthResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = accessToken,
                RefreshToken = refreshToken
            });
        }

        // ── POST /api/auth/refresh ────────────────────────────────────
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            // 1. اقرأ الـ claims من الـ access token حتى لو expired
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
                return Unauthorized("Invalid access token.");

            var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized("Invalid token claims.");

            // 2. دور على الـ refresh token في الداتابيز بالـ hash بتاعه
            var tokenHash = HashToken(request.RefreshToken);
            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == tokenHash);

            if (stored == null)
                return Unauthorized("Refresh token not found.");

            // 3. تأكد إنه بتاع نفس الـ user
            if (stored.UserId != userId)
                return Unauthorized("Token mismatch.");

            // 4. لو اتحرق أو expired — ممكن يكون هجوم، احرق الكل
            if (!stored.IsActive)
            {
                await RevokeAllUserTokensAsync(userId);
                return Unauthorized("Refresh token expired or revoked. Please login again.");
            }

            // 5. احرق القديم وأصدر pair جديدة (rotation)
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return Unauthorized("User not found.");

            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var newHash = HashToken(newRefreshToken);

            stored.IsRevoked = true;
            stored.ReplacedByToken = newHash;

            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = newHash,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(
                    int.Parse(_config["JwtSettings:RefreshTokenDays"]!))
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        // ── POST /api/auth/logout ─────────────────────────────────────
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var tokenHash = HashToken(request.RefreshToken);
            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == tokenHash);

            if (stored is { IsActive: true })
            {
                stored.IsRevoked = true;
                await _db.SaveChangesAsync();
            }

            return NoContent();
        }

        // ── Helper: أصدر access + refresh معاً ───────────────────────
        private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(User user)
        {
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = HashToken(refreshToken),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(
                    int.Parse(_config["JwtSettings:RefreshTokenDays"]!))
            });

            await _db.SaveChangesAsync();
            return (accessToken, refreshToken);
        }

        // ── Helper: Access Token (JWT) ────────────────────────────────
        private string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(
                             Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Email,          user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JwtSettings:Issuer"],
                audience: _config["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),  // قصير عمداً
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ── Helper: Refresh Token (random bytes) ─────────────────────
        private static string GenerateRefreshToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        // ── Helper: اقرأ claims من JWT منتهي الصلاحية ────────────────
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,   // ← مهم: نسمح بالـ expired
                ValidateIssuerSigningKey = true,
                ValidIssuer = _config["JwtSettings:Issuer"],
                ValidAudience = _config["JwtSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                                               Encoding.UTF8.GetBytes(
                                                   _config["JwtSettings:SecretKey"]!))
            };

            try
            {
                return new JwtSecurityTokenHandler()
                    .ValidateToken(token, validationParams, out _);
            }
            catch { return null; }
        }

        // ── Helper: SHA-256 hash للـ token قبل التخزين ───────────────
        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        // ── Helper: احرق كل tokens الـ user (عند هجوم محتمل) ─────────
        private async Task RevokeAllUserTokensAsync(int userId)
        {
            var tokens = await _db.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();
            tokens.ForEach(t => t.IsRevoked = true);
            await _db.SaveChangesAsync();
        }
    }
}