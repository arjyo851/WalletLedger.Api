using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WalletLedger.Api.Auth;
using WalletLedger.Api.Data;
using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly WalletLedgerDbContext _db;
        public AuthController(IConfiguration config, WalletLedgerDbContext db)
        {
            _config = config;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(Guid userId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return Unauthorized();

            var permissions = new[]
            {
                Permissions.WalletRead,
                Permissions.WalletWrite,
                Permissions.TransactionCredit,
                Permissions.TransactionDebit
            };

            var accessToken = GenerateJwt(userId, permissions);

            var refreshToken = RefreshTokenGenerator.Generate();
            var refreshTokenHash = RefreshTokenGenerator.Hash(refreshToken);

            _db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(
                    int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!)
                ),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken,
                refreshToken
            });
        }

        private string GenerateJwt(Guid userId, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),//step1: make claims
            };

            claims.AddRange(permissions.Select(p => new Claim("permissions", p))); // add permissions to claims

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!) // got jwt key from config (appsettings)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); // got creds for the key and hash it

            var token = new JwtSecurityToken( // made a object of type jwtsecuritytoken with all these informtion
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!)
                ),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("login-admin")]
        public async Task<IActionResult> LoginAdmin(Guid userId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return Unauthorized();

            var permissions = new[]
            {
                Permissions.AdminHealth
            };


            var token = GenerateJwt(userId, permissions);
            return Ok(new { token });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(string refreshToken)
        {
            var tokenHash = RefreshTokenGenerator.Hash(refreshToken);

            var storedToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    !t.IsRevoked &&
                    t.ExpiresAt > DateTime.UtcNow);

            if (storedToken == null)
                return Unauthorized();

            // Rotate token 
            storedToken.IsRevoked = true;

            var newRefreshToken = RefreshTokenGenerator.Generate();
            var newRefreshTokenHash = RefreshTokenGenerator.Hash(newRefreshToken);

            _db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = storedToken.UserId,
                TokenHash = newRefreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(
                    int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!)
                ),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });

            var permissions = new[]
            {
                Permissions.WalletRead,
                Permissions.WalletWrite,
                Permissions.TransactionCredit,
                Permissions.TransactionDebit
            };

            var newAccessToken =
                GenerateJwt(storedToken.UserId, permissions);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(string refreshToken)
        {
            var tokenHash = RefreshTokenGenerator.Hash(refreshToken);

            var storedToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (storedToken != null)
            {
                storedToken.IsRevoked = true;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }


    }
}
