using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sakin.Common.Security.Models;

namespace Sakin.Common.Security.Services;

public class JwtService : IJwtService
{
    private readonly JwtOptions _jwtOptions;
    private readonly IMemoryCache _cache;
    private readonly TokenValidationParameters _validationParameters;

    public JwtService(IOptions<JwtOptions> jwtOptions, IMemoryCache cache)
    {
        _jwtOptions = jwtOptions.Value;
        _cache = cache;
        
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = _jwtOptions.ValidateIssuer,
            ValidateAudience = _jwtOptions.ValidateAudience,
            ValidateLifetime = _jwtOptions.ValidateLifetime,
            ValidateIssuerSigningKey = _jwtOptions.ValidateIssuerSigningKey,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }

    public TokenResponse GenerateToken(ClientCredentials client)
    {
        var tokenId = Guid.NewGuid().ToString();
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);
        
        var permissions = RolePermissions.GetPermissions(client.Role);
        
        var claims = new List<Claim>
        {
            new(SakinClaimTypes.TokenId, tokenId),
            new(SakinClaimTypes.ClientId, client.ClientId),
            new(SakinClaimTypes.Name, client.Name),
            new(SakinClaimTypes.Role, client.Role.ToString()),
            new(SakinClaimTypes.Permissions, ((int)permissions).ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString()),
        };
        
        if (!string.IsNullOrEmpty(client.Scope))
        {
            claims.Add(new Claim(SakinClaimTypes.Scope, client.Scope));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return new TokenResponse
        {
            AccessToken = tokenString,
            TokenType = "Bearer",
            ExpiresIn = _jwtOptions.AccessTokenExpirationMinutes * 60,
            Scope = client.Scope
        };
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, _validationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return null;
            }
            
            var tokenId = principal.FindFirst(SakinClaimTypes.TokenId)?.Value;
            if (!string.IsNullOrEmpty(tokenId) && IsTokenBlacklisted(tokenId))
            {
                return null;
            }
            
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public bool IsTokenBlacklisted(string tokenId)
    {
        return _cache.TryGetValue($"blacklist:{tokenId}", out _);
    }

    public void BlacklistToken(string tokenId, DateTime expiresAt)
    {
        var ttl = expiresAt - DateTime.UtcNow;
        if (ttl > TimeSpan.Zero)
        {
            _cache.Set($"blacklist:{tokenId}", true, ttl);
        }
    }
}
