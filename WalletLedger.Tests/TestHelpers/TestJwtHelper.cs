using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WalletLedger.Api.Auth;

namespace WalletLedger.Tests.TestHelpers;

public static class TestJwtHelper
{
    private const string TestKey = "ThisIsATestKeyForJWTTokenGeneration12345678901234567890";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    public static string GenerateToken(Guid userId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        };

        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateUserToken(Guid userId)
    {
        return GenerateToken(userId,
            Permissions.WalletRead,
            Permissions.WalletWrite,
            Permissions.TransactionCredit,
            Permissions.TransactionDebit);
    }

    public static string GenerateAdminToken(Guid userId)
    {
        return GenerateToken(userId, Permissions.AdminHealth);
    }
}




