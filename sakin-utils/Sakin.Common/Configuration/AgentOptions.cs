namespace Sakin.Common.Configuration;

public class AgentOptions
{
    public string AgentId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = false;
    public List<string> AllowlistedScripts { get; set; } = new();
    public string ScriptsDirectory { get; set; } = "scripts";
    public TimeSpan CommandExpireTime { get; set; } = TimeSpan.FromMinutes(5);
}