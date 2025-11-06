using Sakin.Common.Models;

namespace Sakin.ThreatIntelService.Services
{
    public interface IThreatIntelService
    {
        Task<ThreatIntelScore> ProcessAsync(ThreatIntelLookupRequest request, CancellationToken cancellationToken = default);
    }
}
