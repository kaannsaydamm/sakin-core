using Sakin.Common.Models;

namespace Sakin.Correlation.Services;

public interface IAnomalyDetectionService
{
    Task<AnomalyScoreResult> CalculateAnomalyScoreAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default);
}
