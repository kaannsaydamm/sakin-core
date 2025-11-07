using Sakin.Common.Security.Models;

namespace Sakin.Common.Security.Services;

public interface IClientCredentialsStore
{
    ClientCredentials? GetClient(string clientId);
    bool ValidateClient(string clientId, string clientSecret);
}
