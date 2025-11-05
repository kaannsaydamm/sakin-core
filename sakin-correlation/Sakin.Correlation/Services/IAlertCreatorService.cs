using Sakin.Common.Models;
using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IAlertCreatorService
{
    Task CreateAlertAsync(CorrelationRule rule, EventEnvelope eventEnvelope, CancellationToken cancellationToken = default);
}