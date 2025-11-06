namespace Sakin.SOAR.Services;

public interface ISlackNotificationClient
{
    Task<bool> SendNotificationAsync(string? channel, string message, CancellationToken cancellationToken = default);
}

public interface IEmailNotificationClient
{
    Task<bool> SendEmailAsync(List<string> recipients, string subject, string body, CancellationToken cancellationToken = default);
}

public interface IJiraNotificationClient
{
    Task<string> CreateTicketAsync(string summary, string description, CancellationToken cancellationToken = default);
}