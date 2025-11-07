namespace Sakin.Common.Security.Models;

public class ClientCredentials
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.ReadOnly;
    public string? Scope { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}
