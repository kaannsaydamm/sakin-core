using System;
using System.Threading;
using System.Threading.Tasks;
using Sakin.Agents.Windows.Models;

namespace Sakin.Agents.Windows.Messaging
{
    public interface IEventLogPublisher : IDisposable
    {
        Task PublishAsync(EventLogEntryData entry, CancellationToken cancellationToken = default);

        Task FlushAsync(CancellationToken cancellationToken = default);
    }
}
