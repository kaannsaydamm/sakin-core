using Microsoft.AspNetCore.Mvc;
using Sakin.Common.Audit;
using Sakin.Common.Security.Models;
using Sakin.Common.Security.Services;

namespace Sakin.Panel.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IClientCredentialsStore _clientStore;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IClientCredentialsStore clientStore,
        IJwtService jwtService,
        IAuditService auditService,
        ILogger<AuthController> logger)
    {
        _clientStore = clientStore;
        _jwtService = jwtService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
        {
            await LogAuthAttempt(request.ClientId, "token_request", "failure", "missing_credentials");
            return BadRequest(new { error = "invalid_request", error_description = "Client ID and secret are required" });
        }

        if (!_clientStore.ValidateClient(request.ClientId, request.ClientSecret))
        {
            await LogAuthAttempt(request.ClientId, "token_request", "failure", "invalid_credentials");
            _logger.LogWarning("Failed authentication attempt for client: {ClientId}", request.ClientId);
            return Unauthorized(new { error = "invalid_client", error_description = "Invalid client credentials" });
        }

        var client = _clientStore.GetClient(request.ClientId);
        if (client == null)
        {
            await LogAuthAttempt(request.ClientId, "token_request", "failure", "client_not_found");
            return Unauthorized(new { error = "invalid_client", error_description = "Client not found or disabled" });
        }

        var token = _jwtService.GenerateToken(client);
        
        await LogAuthAttempt(request.ClientId, "token_request", "success", null);
        
        return Ok(token);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeToken()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return BadRequest(new { error = "invalid_request", error_description = "Authorization header required" });
        }

        var token = authHeader["Bearer ".Length..];
        var principal = _jwtService.ValidateToken(token);
        
        if (principal == null)
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        var tokenId = principal.FindFirst("jti")?.Value;
        var exp = principal.FindFirst("exp")?.Value;
        
        if (!string.IsNullOrEmpty(tokenId) && long.TryParse(exp, out var expTimestamp))
        {
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expTimestamp).UtcDateTime;
            _jwtService.BlacklistToken(tokenId, expiresAt);
            
            var clientId = principal.FindFirst("client_id")?.Value ?? "unknown";
            await LogAuthAttempt(clientId, "token_revoked", "success", null);
            
            return Ok(new { message = "Token revoked successfully" });
        }

        return BadRequest(new { error = "invalid_token" });
    }

    private async Task LogAuthAttempt(string clientId, string action, string status, string? errorCode)
    {
        var auditEvent = new AuditEvent
        {
            User = clientId,
            Action = action,
            ResourceType = "authentication",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
            Status = status,
            ErrorCode = errorCode
        };

        await _auditService.LogAsync(auditEvent);
    }
}
