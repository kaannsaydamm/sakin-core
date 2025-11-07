using System.Security.Claims;
using Sakin.Common.Audit;
using Sakin.Common.Security;

namespace Sakin.Panel.Api.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        if (!ShouldAudit(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var startTime = DateTime.UtcNow;
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            await LogRequest(context, auditService, startTime, exception);
        }
    }

    private static bool ShouldAudit(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";
        
        if (pathValue.StartsWith("/api/auth/token"))
        {
            return false;
        }

        if (pathValue.StartsWith("/health") || pathValue.StartsWith("/metrics"))
        {
            return false;
        }

        return pathValue.StartsWith("/api/");
    }

    private async Task LogRequest(
        HttpContext context, 
        IAuditService auditService, 
        DateTime startTime,
        Exception? exception)
    {
        try
        {
            var user = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(SakinClaimTypes.Name)?.Value ?? "anonymous"
                : "anonymous";

            var action = $"{context.Request.Method}_{context.Request.Path}";
            var status = exception != null ? "error" : (context.Response.StatusCode >= 400 ? "failure" : "success");

            var auditEvent = new AuditEvent
            {
                User = user,
                Action = action,
                ResourceType = GetResourceType(context.Request.Path),
                ResourceId = GetResourceId(context.Request.Path),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].FirstOrDefault(),
                Status = status,
                ErrorCode = exception != null ? exception.GetType().Name : null,
                ErrorMessage = exception?.Message,
                Metadata = new Dictionary<string, string>
                {
                    ["method"] = context.Request.Method,
                    ["path"] = context.Request.Path,
                    ["status_code"] = context.Response.StatusCode.ToString(),
                    ["duration_ms"] = ((DateTime.UtcNow - startTime).TotalMilliseconds).ToString("F2")
                }
            };

            await auditService.LogAsync(auditEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event for request {Path}", context.Request.Path);
        }
    }

    private static string GetResourceType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return segments.Length >= 2 ? segments[1] : "unknown";
    }

    private static string? GetResourceId(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (segments.Length >= 3 && Guid.TryParse(segments[2], out _))
        {
            return segments[2];
        }
        return null;
    }
}
