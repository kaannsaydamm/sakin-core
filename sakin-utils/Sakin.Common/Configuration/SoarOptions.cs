namespace Sakin.Common.Configuration;

public class SoarOptions
{
    public bool Enabled { get; set; } = true;
    public string PlaybooksDirectory { get; set; } = "playbooks";
    public int MaxConcurrentExecutions { get; set; } = 10;
    public TimeSpan DefaultCommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}