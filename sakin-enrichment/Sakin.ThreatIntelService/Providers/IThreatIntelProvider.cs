using Sakin.Common.Models;

namespace Sakin.ThreatIntelService.Providers
{
    public interface IThreatIntelProvider
    {
        string Name { get; }

        bool Supports(ThreatIntelIndicatorType type);

        Task<ThreatIntelScore?> LookupAsync(ThreatIntelLookupRequest request, CancellationToken cancellationToken);
    }
}
