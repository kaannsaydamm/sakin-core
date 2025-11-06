using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Polly;
using System.Text;
using System.Text.Json;

namespace Sakin.SOAR.Services;

public class JiraNotificationClient : IJiraNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;
    private readonly ILogger<JiraNotificationClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public JiraNotificationClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<NotificationOptions> notificationOptions,
        ILogger<JiraNotificationClient> logger)
    {
        _httpClient = httpClient;
        _options = notificationOptions.Value.Jira;
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
                        "Jira API call failed (attempt {Attempt}). Waiting {Delay}s before retry...",
                        retryAttempt,
                        timespan.TotalSeconds);
                });

        // Configure HttpClient for Jira
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiToken}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        }
    }

    public async Task<string> CreateTicketAsync(string summary, string description, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug("Jira integration disabled or base URL not configured");
            return string.Empty;
        }

        try
        {
            var payload = new
            {
                fields = new
                {
                    project = new { key = _options.ProjectKey },
                    summary = summary,
                    description = description,
                    issuetype = new { name = _options.IssueType }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("/rest/api/2/issue", content, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (result.TryGetProperty("key", out var keyProperty))
                {
                    var ticketKey = keyProperty.GetString();
                    var ticketUrl = $"{_options.BaseUrl.TrimEnd('/')}/browse/{ticketKey}";
                    
                    _logger.LogInformation("Jira ticket created successfully: {TicketKey}", ticketKey);
                    return ticketUrl;
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Jira ticket creation failed with status {StatusCode}: {Error}", 
                response.StatusCode, errorContent);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jira ticket");
            return string.Empty;
        }
    }
}