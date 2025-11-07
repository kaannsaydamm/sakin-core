using System.Security.Claims;
using Microsoft.Extensions.Options;
using Sakin.Common.Security;

namespace Sakin.Panel.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var apiKeyHeader))
        {
            var apiKey = apiKeyHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
            {
                var keyDef = _options.Keys.FirstOrDefault(k => 
                    k.Enabled && 
                    k.Key == apiKey && 
                    (!k.ExpiresAt.HasValue || k.ExpiresAt.Value > DateTime.UtcNow));

                if (keyDef != null)
                {
                    var permissions = RolePermissions.GetPermissions(keyDef.Role);
                    
                    var claims = new List<Claim>
                    {
                        new(SakinClaimTypes.Name, keyDef.Name),
                        new(SakinClaimTypes.Role, keyDef.Role.ToString()),
                        new(SakinClaimTypes.Permissions, ((int)permissions).ToString()),
                        new("auth_type", "api_key")
                    };

                    var identity = new ClaimsIdentity(claims, "ApiKey");
                    context.User = new ClaimsPrincipal(identity);
                }
            }
        }

        await _next(context);
    }
}
