using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sakin.Common.Audit
{
    public interface IAuditLogger
    {
        Task LogAuditEventAsync(
            string user,
            string action,
            Guid correlationId,
            object details,
            string? service = null,
            CancellationToken cancellationToken = default);
    }
}
