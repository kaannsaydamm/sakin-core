using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Services;

namespace Sakin.HttpCollector.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HttpCollectorOptions _options;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IMetricsService _metrics;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<HttpCollectorOptions> options,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IMetricsService metrics)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.RequireApiKey)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            _logger.LogWarning("Request from {RemoteIp} rejected: Missing API key", 
                context.Connection.RemoteIpAddress);
            _metrics.IncrementHttpErrors(401);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Missing API key");
            return;
        }

        if (!_options.ValidApiKeys.Contains(apiKey.ToString()))
        {
            _logger.LogWarning("Request from {RemoteIp} rejected: Invalid API key", 
                context.Connection.RemoteIpAddress);
            _metrics.IncrementHttpErrors(401);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Invalid API key");
            return;
        }

        await _next(context);
    }
}
