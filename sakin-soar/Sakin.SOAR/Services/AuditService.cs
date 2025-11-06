using Microsoft.Extensions.Logging;
using Sakin.Common.Configuration;
using Sakin.Messaging.Producer;
using System.Text.Json;

namespace Sakin.SOAR.Services;

public interface IAuditService
{
    Task WriteAuditEventAsync(object auditEvent, CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly SoarKafkaTopics _kafkaTopics;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IKafkaProducer kafkaProducer,
        Microsoft.Extensions.Options.IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AuditService> logger)
    {
        _kafkaProducer = kafkaProducer;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    public async Task WriteAuditEventAsync(object auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var auditJson = JsonSerializer.Serialize(auditEvent);
            var eventId = Guid.NewGuid().ToString();

            await _kafkaProducer.ProduceAsync(
                _kafkaTopics.AuditLog,
                eventId,
                auditJson,
                cancellationToken);

            _logger.LogDebug("Audit event written: {EventType}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit event");
            // Don't throw - audit failures shouldn't break the main flow
        }
    }
}