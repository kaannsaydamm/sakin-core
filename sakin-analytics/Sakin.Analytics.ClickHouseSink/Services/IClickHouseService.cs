using Sakin.Common.Models;

namespace Sakin.Analytics.ClickHouseSink.Services;

public interface IClickHouseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task BatchInsertEventsAsync(IEnumerable<EventEnvelope> events, CancellationToken cancellationToken = default);
}
