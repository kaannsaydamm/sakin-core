using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class Action
{
    [JsonPropertyName("type")]
    public ActionType Type { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    [JsonPropertyName("delay")]
    public int? Delay { get; set; }

    [JsonPropertyName("retry")]
    public RetryPolicy? Retry { get; set; }
}

public enum ActionType
{
    [JsonPropertyName("alert")]
    Alert,
    
    [JsonPropertyName("webhook")]
    Webhook,
    
    [JsonPropertyName("email")]
    Email,
    
    [JsonPropertyName("script")]
    Script,
    
    [JsonPropertyName("log")]
    Log,
    
    [JsonPropertyName("block")]
    Block,
    
    [JsonPropertyName("quarantine")]
    Quarantine,
    
    [JsonPropertyName("playbook")]
    Playbook
}

public class RetryPolicy
{
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; } = 3;

    [JsonPropertyName("delay")]
    public int Delay { get; set; } = 1000;

    [JsonPropertyName("backoff")]
    public BackoffType Backoff { get; set; } = BackoffType.Fixed;
}

public enum BackoffType
{
    [JsonPropertyName("fixed")]
    Fixed,
    
    [JsonPropertyName("exponential")]
    Exponential,
    
    [JsonPropertyName("linear")]
    Linear
}