using System.Text.Json.Serialization;

namespace Sakin.Correlation.Models;

public class Condition
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public ConditionOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = true;

    [JsonPropertyName("negate")]
    public bool Negate { get; set; } = false;
}

public enum ConditionOperator
{
    [JsonPropertyName("equals")]
    Equals,
    
    [JsonPropertyName("not_equals")]
    NotEquals,
    
    [JsonPropertyName("contains")]
    Contains,
    
    [JsonPropertyName("not_contains")]
    NotContains,
    
    [JsonPropertyName("starts_with")]
    StartsWith,
    
    [JsonPropertyName("ends_with")]
    EndsWith,
    
    [JsonPropertyName("greater_than")]
    GreaterThan,
    
    [JsonPropertyName("greater_than_or_equal")]
    GreaterThanOrEqual,
    
    [JsonPropertyName("less_than")]
    LessThan,
    
    [JsonPropertyName("less_than_or_equal")]
    LessThanOrEqual,
    
    [JsonPropertyName("in")]
    In,
    
    [JsonPropertyName("not_in")]
    NotIn,
    
    [JsonPropertyName("regex")]
    Regex,
    
    [JsonPropertyName("exists")]
    Exists,
    
    [JsonPropertyName("not_exists")]
    NotExists
}