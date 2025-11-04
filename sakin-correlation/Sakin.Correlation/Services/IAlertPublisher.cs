using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IAlertPublisher
{
    Task PublishAsync(Alert alert, CancellationToken cancellationToken);
}
