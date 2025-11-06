namespace Sakin.ThreatIntelService.Services
{
    public interface IThreatIntelRateLimiter
    {
        Task<bool> TryAcquireAsync(string providerName, int dailyQuota, CancellationToken cancellationToken = default);
    }
}
