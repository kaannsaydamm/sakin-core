using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Logging;
using Sakin.Messaging.Producer;

namespace Sakin.Common.Audit
{
    public class KafkaAuditLogger : IAuditLogger
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly AuditLoggingOptions _options;
        private readonly IOptionsMonitor<TelemetryOptions> _telemetryOptions;
        private readonly ILogger<KafkaAuditLogger> _logger;

        public KafkaAuditLogger(
            IKafkaProducer kafkaProducer,
            IOptions<AuditLoggingOptions> options,
            IOptionsMonitor<TelemetryOptions> telemetryOptions,
            ILogger<KafkaAuditLogger> logger)
        {
            _kafkaProducer = kafkaProducer;
            _options = options.Value;
            _telemetryOptions = telemetryOptions;
            _logger = logger;
        }

        public async Task LogAuditEventAsync(
            string user,
            string action,
            Guid correlationId,
            object details,
            string? service = null,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return;
            }

            var effectiveServiceName = service
                ?? _options.ServiceName
                ?? _telemetryOptions.CurrentValue.ServiceName
                ?? "sakin-service";

            var auditEvent = new AuditLogEvent
            {
                CorrelationId = correlationId,
                User = string.IsNullOrWhiteSpace(user) ? "system" : user,
                Action = action,
                Service = effectiveServiceName,
                Details = _options.IncludePayload ? details : null
            };

            var topic = string.IsNullOrWhiteSpace(_options.Topic) ? "audit-log" : _options.Topic;

            try
            {
                await _kafkaProducer.ProduceAsync(topic, auditEvent, correlationId.ToString(), cancellationToken);
                _logger.LogDebug("Audit event published to {Topic}: {Action}", topic, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish audit event {CorrelationId}", correlationId);
            }
        }
    }
}
