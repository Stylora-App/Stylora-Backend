using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Stylora.Domain.Entities;

namespace Stylora.Infrastructure.Services;

public class JwtService(string secret)
{
    private const int AccessTokenMinutes = 15;
    private const int RefreshTokenDays = 7;

    public string GenerateAccessToken(User user)
    {
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Email)
            ],
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: MakeCreds());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken(User user)
    {
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("token_type", "refresh")
            ],
            expires: DateTime.UtcNow.AddDays(RefreshTokenDays),
            signingCredentials: MakeCreds());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Returns the user's ID if the token is a valid, unexpired refresh JWT; otherwise throws.
    public Guid ValidateRefreshToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = MakeKey(),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        }, out _);

        if (principal.FindFirst("token_type")?.Value != "refresh")
            throw new SecurityTokenException("Not a refresh token.");

        if (!Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
            throw new SecurityTokenException("Invalid subject claim.");

        return id;
    }

    private SymmetricSecurityKey MakeKey()
        => new(Encoding.UTF8.GetBytes(secret));

    private SigningCredentials MakeCreds()
        => new(MakeKey(), SecurityAlgorithms.HmacSha256);
}
