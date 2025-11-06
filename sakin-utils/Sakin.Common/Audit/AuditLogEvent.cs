using System;

namespace Sakin.Common.Audit
{
    public record AuditLogEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public Guid CorrelationId { get; init; }

        public string User { get; init; } = string.Empty;

        public string Action { get; init; } = string.Empty;

        public string Service { get; init; } = string.Empty;

        public object? Details { get; init; }
    }
}
