using Microsoft.Extensions.Options;
using Sakin.Common.Security.Models;
using System.Security.Cryptography;
using System.Text;

namespace Sakin.Common.Security.Services;

public class ClientCredentialsStore : IClientCredentialsStore
{
    private readonly Dictionary<string, ClientCredentials> _clients = new();

    public ClientCredentialsStore(IOptions<ClientCredentialsOptions> options)
    {
        foreach (var client in options.Value.Clients)
        {
            _clients[client.ClientId] = client;
        }
    }

    public ClientCredentials? GetClient(string clientId)
    {
        if (_clients.TryGetValue(clientId, out var client) && client.Enabled)
        {
            if (client.ExpiresAt.HasValue && client.ExpiresAt.Value < DateTime.UtcNow)
            {
                return null;
            }
            return client;
        }
        return null;
    }

    public bool ValidateClient(string clientId, string clientSecret)
    {
        var client = GetClient(clientId);
        if (client == null)
        {
            return false;
        }
        
        return SecureStringCompare(client.ClientSecret, clientSecret);
    }
    
    private static bool SecureStringCompare(string a, string b)
    {
        if (a == null || b == null)
        {
            return false;
        }
        
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        
        if (aBytes.Length != bBytes.Length)
        {
            return false;
        }
        
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

public class ClientCredentialsOptions
{
    public const string SectionName = "ClientCredentials";
    
    public List<ClientCredentials> Clients { get; set; } = new();
}
