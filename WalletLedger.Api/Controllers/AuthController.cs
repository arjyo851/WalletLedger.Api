using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WalletLedger.Api.Data;

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

            var token = GenerateJwt(userId);
            return Ok(new { token });
        }

        private string GenerateJwt(Guid userId)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) //step1: make claims
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!) // get jwt from config (appsettings)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); // get creds or the key

            var token = new JwtSecurityToken( // make a object of type jwtsecuritytoken with all these informtion
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_config["Jwt:ExpiryMinutes"]!)
                ),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
