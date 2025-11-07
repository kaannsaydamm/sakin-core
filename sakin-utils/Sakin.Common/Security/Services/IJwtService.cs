using System.Security.Claims;
using Sakin.Common.Security.Models;

namespace Sakin.Common.Security.Services;

public interface IJwtService
{
    TokenResponse GenerateToken(ClientCredentials client);
    ClaimsPrincipal? ValidateToken(string token);
    bool IsTokenBlacklisted(string tokenId);
    void BlacklistToken(string tokenId, DateTime expiresAt);
}
