namespace Sakin.Common.Configuration;

public class NotificationOptions
{
    public SlackOptions Slack { get; set; } = new();
    public EmailOptions Email { get; set; } = new();
    public JiraOptions Jira { get; set; } = new();
}

public class SlackOptions
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
    public string DefaultChannel { get; set; } = "#security-alerts";
    public string Username { get; set; } = "SAKIN-SOAR";
}

public class EmailOptions
{
    public bool Enabled { get; set; } = false;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public List<string> DefaultRecipients { get; set; } = new();
}

public class JiraOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = "SEC";
    public string IssueType { get; set; } = "Bug";
}