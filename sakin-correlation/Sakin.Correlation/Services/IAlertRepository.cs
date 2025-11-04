using Sakin.Correlation.Models;

namespace Sakin.Correlation.Services;

public interface IAlertRepository
{
    Task PersistAsync(Alert alert, CancellationToken cancellationToken);
}
