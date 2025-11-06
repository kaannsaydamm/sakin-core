using Microsoft.Extensions.Logging;

namespace Sakin.SOAR.Services;

public interface INotificationService
{
    Task<bool> SendSlackNotificationAsync(string? channel, string message, CancellationToken cancellationToken = default);
    Task<bool> SendEmailNotificationAsync(List<string> recipients, string subject, string body, CancellationToken cancellationToken = default);
    Task<string> CreateJiraTicketAsync(string summary, string description, CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly ISlackNotificationClient _slackClient;
    private readonly IEmailNotificationClient _emailClient;
    private readonly IJiraNotificationClient _jiraClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ISlackNotificationClient slackClient,
        IEmailNotificationClient emailClient,
        IJiraNotificationClient jiraClient,
        ILogger<NotificationService> logger)
    {
        _slackClient = slackClient;
        _emailClient = emailClient;
        _jiraClient = jiraClient;
        _logger = logger;
    }

    public async Task<bool> SendSlackNotificationAsync(string? channel, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _slackClient.SendNotificationAsync(channel, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
            return false;
        }
    }

    public async Task<bool> SendEmailNotificationAsync(List<string> recipients, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _emailClient.SendEmailAsync(recipients, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
            return false;
        }
    }

    public async Task<string> CreateJiraTicketAsync(string summary, string description, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _jiraClient.CreateTicketAsync(summary, description, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jira ticket");
            return string.Empty;
        }
    }
}