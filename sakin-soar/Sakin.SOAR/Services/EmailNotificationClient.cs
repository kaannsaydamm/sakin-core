using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Polly;

namespace Sakin.SOAR.Services;

public class EmailNotificationClient : IEmailNotificationClient
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationClient> _logger;
    private readonly IAsyncPolicy _retryPolicy;

    public EmailNotificationClient(
        Microsoft.Extensions.Options.IOptions<NotificationOptions> notificationOptions,
        ILogger<EmailNotificationClient> logger)
    {
        _options = notificationOptions.Value.Email;
        _logger = logger;

        // Setup retry policy
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "Email notification failed (attempt {Attempt}): {Error}. Waiting {Delay}s before retry...",
                        retryAttempt,
                        exception.Message,
                        timespan.TotalSeconds);
                });
    }

    public async Task<bool> SendEmailAsync(List<string> recipients, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.SmtpServer))
        {
            _logger.LogDebug("Email notifications disabled or SMTP server not configured");
            return false;
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                using var client = new SmtpClient();
                
                await client.ConnectAsync(_options.SmtpServer, _options.SmtpPort, 
                    _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
                }

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(_options.FromAddress));

                foreach (var recipient in recipients)
                {
                    message.To.Add(MailboxAddress.Parse(recipient));
                }

                message.Subject = subject;
                message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", recipients));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", recipients));
                throw;
            }
        });
    }
}