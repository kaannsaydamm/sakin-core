namespace Sakin.Common.Security;

public class ApiKeyOptions
{
    public const string SectionName = "ApiKey";
    
    public bool Enabled { get; set; } = true;
    public string HeaderName { get; set; } = "X-API-Key";
    public List<ApiKeyDefinition> Keys { get; set; } = new();
}

public class ApiKeyDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.ReadOnly;
    public bool Enabled { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}
