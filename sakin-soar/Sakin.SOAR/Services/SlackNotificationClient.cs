using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Polly;
using System.Text;
using System.Text.Json;

namespace Sakin.SOAR.Services;

public class SlackNotificationClient : ISlackNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly SlackOptions _options;
    private readonly ILogger<SlackNotificationClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public SlackNotificationClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<NotificationOptions> notificationOptions,
        ILogger<SlackNotificationClient> logger)
    {
        _httpClient = httpClient;
        _options = notificationOptions.Value.Slack;
        _logger = logger;

        // Setup retry policy
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "Slack notification failed (attempt {Attempt}). Waiting {Delay}s before retry...",
                        retryAttempt,
                        timespan.TotalSeconds);
                });
    }

    public async Task<bool> SendNotificationAsync(string? channel, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogDebug("Slack notifications disabled or webhook URL not configured");
            return false;
        }

        try
        {
            var payload = new
            {
                channel = channel ?? _options.DefaultChannel,
                username = _options.Username,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync(_options.WebhookUrl, content, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Slack notification sent successfully to channel {Channel}", channel ?? _options.DefaultChannel);
                return true;
            }
            else
            {
                _logger.LogWarning("Slack notification failed with status {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
            return false;
        }
    }
}