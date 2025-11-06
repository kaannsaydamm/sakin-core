using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using Sakin.HttpCollector.Configuration;
using Sakin.HttpCollector.Middleware;
using Sakin.HttpCollector.Models;

namespace Sakin.HttpCollector.Services;

public class HttpEndpointService : BackgroundService
{
    private readonly ChannelWriter<RawLogEntry> _channelWriter;
    private readonly HttpCollectorOptions _options;
    private readonly ILogger<HttpEndpointService> _logger;
    private readonly IMetricsService _metrics;
    private readonly IServiceProvider _serviceProvider;
    private WebApplication? _app;

    public HttpEndpointService(
        ChannelWriter<RawLogEntry> channelWriter,
        IOptions<HttpCollectorOptions> options,
        ILogger<HttpEndpointService> logger,
        IMetricsService metrics,
        IServiceProvider serviceProvider)
    {
        _channelWriter = channelWriter;
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(_options.Port);
        });

        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton(_metrics);
        builder.Services.AddSingleton(_logger);

        _app = builder.Build();

        _app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        _app.UseMetricServer();

        _app.MapPost(_options.Path, async (HttpContext context) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var contentType = context.Request.ContentType ?? "text/plain";

            try
            {
                if (context.Request.ContentLength > _options.MaxBodySize)
                {
                    _logger.LogWarning("Request from {SourceIp} rejected: Payload too large ({Size} bytes)",
                        sourceIp, context.Request.ContentLength);
                    _metrics.IncrementHttpErrors(413);
                    _metrics.IncrementHttpRequests(sourceIp, "oversized", 413);
                    context.Response.StatusCode = 413;
                    await context.Response.WriteAsync("Payload Too Large");
                    return;
                }

                using var reader = new StreamReader(context.Request.Body);
                var rawMessage = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(rawMessage))
                {
                    _logger.LogWarning("Request from {SourceIp} rejected: Empty body", sourceIp);
                    _metrics.IncrementHttpErrors(400);
                    _metrics.IncrementHttpRequests(sourceIp, "empty", 400);
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Bad Request: Empty body");
                    return;
                }

                var xSourceHeader = context.Request.Headers["X-Source"].FirstOrDefault();

                var logEntry = new RawLogEntry
                {
                    RawMessage = rawMessage,
                    SourceIp = sourceIp,
                    ContentType = contentType,
                    XSourceHeader = xSourceHeader,
                    ReceivedAt = DateTimeOffset.UtcNow
                };

                await _channelWriter.WriteAsync(logEntry, stoppingToken);

                var format = DetermineFormat(contentType, rawMessage);
                _metrics.IncrementHttpRequests(sourceIp, format, 202);

                stopwatch.Stop();
                _metrics.RecordHttpRequestDuration(stopwatch.Elapsed.TotalSeconds);

                context.Response.StatusCode = 202;
                await context.Response.WriteAsync("Accepted");

                _logger.LogDebug("Accepted event from {SourceIp}, format: {Format}, size: {Size} bytes",
                    sourceIp, format, rawMessage.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request from {SourceIp}", sourceIp);
                _metrics.IncrementHttpErrors(500);
                _metrics.IncrementHttpRequests(sourceIp, "error", 500);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal Server Error");
            }
        });

        _logger.LogInformation("HTTP Collector Service starting on port {Port} at path {Path}",
            _options.Port, _options.Path);

        await _app.RunAsync(stoppingToken);
    }

    private string DetermineFormat(string contentType, string rawMessage)
    {
        var ct = contentType.ToLowerInvariant();

        if (ct.Contains("application/json"))
        {
            return "cef_json";
        }

        if (rawMessage.StartsWith("CEF:", StringComparison.OrdinalIgnoreCase))
        {
            return "cef_string";
        }

        return "syslog_string";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP Collector Service stopping...");
        
        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
        }

        _channelWriter.Complete();

        await base.StopAsync(cancellationToken);
    }
}
