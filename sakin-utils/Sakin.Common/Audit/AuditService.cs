using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sakin.Messaging.Abstractions;

namespace Sakin.Common.Audit;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly AuditLoggingOptions _options;
    private readonly IEventPublisher? _eventPublisher;
    private readonly NpgsqlConnection? _connection;
    private readonly string _auditLogFilePath;

    public AuditService(
        ILogger<AuditService> logger,
        IOptions<AuditLoggingOptions> options,
        IEventPublisher? eventPublisher = null,
        NpgsqlConnection? connection = null)
    {
        _logger = logger;
        _options = options.Value;
        _eventPublisher = eventPublisher;
        _connection = connection;
        _auditLogFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "audit.jsonl");
        
        Directory.CreateDirectory(Path.GetDirectoryName(_auditLogFilePath)!);
    }

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            await Task.WhenAll(
                LogToKafkaAsync(auditEvent, cancellationToken),
                LogToPostgresAsync(auditEvent, cancellationToken),
                LogToFileAsync(auditEvent, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: {Action} by {User}", 
                auditEvent.Action, auditEvent.User);
        }
    }

    private async Task LogToKafkaAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        if (_eventPublisher != null)
        {
            try
            {
                await _eventPublisher.PublishAsync(
                    _options.Topic,
                    auditEvent.Id,
                    JsonSerializer.Serialize(auditEvent),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish audit event to Kafka");
            }
        }
    }

    private async Task LogToPostgresAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        if (_connection == null)
        {
            return;
        }

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
            }

            const string sql = @"
                INSERT INTO audit_events (id, timestamp, user_name, action, resource_type, resource_id, 
                    old_state, new_state, ip_address, user_agent, status, error_code, error_message, metadata)
                VALUES (@id, @timestamp, @user, @action, @resourceType, @resourceId, 
                    @oldState::jsonb, @newState::jsonb, @ipAddress, @userAgent, @status, @errorCode, @errorMessage, @metadata::jsonb)";

            await using var cmd = new NpgsqlCommand(sql, _connection);
            cmd.Parameters.AddWithValue("id", auditEvent.Id);
            cmd.Parameters.AddWithValue("timestamp", auditEvent.Timestamp);
            cmd.Parameters.AddWithValue("user", auditEvent.User);
            cmd.Parameters.AddWithValue("action", auditEvent.Action);
            cmd.Parameters.AddWithValue("resourceType", auditEvent.ResourceType);
            cmd.Parameters.AddWithValue("resourceId", (object?)auditEvent.ResourceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("oldState", auditEvent.OldState != null ? 
                JsonSerializer.Serialize(auditEvent.OldState) : DBNull.Value);
            cmd.Parameters.AddWithValue("newState", auditEvent.NewState != null ? 
                JsonSerializer.Serialize(auditEvent.NewState) : DBNull.Value);
            cmd.Parameters.AddWithValue("ipAddress", (object?)auditEvent.IpAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("userAgent", (object?)auditEvent.UserAgent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", auditEvent.Status);
            cmd.Parameters.AddWithValue("errorCode", (object?)auditEvent.ErrorCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("errorMessage", (object?)auditEvent.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("metadata", auditEvent.Metadata != null ? 
                JsonSerializer.Serialize(auditEvent.Metadata) : DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit event to Postgres");
        }
    }

    private async Task LogToFileAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(auditEvent);
            await File.AppendAllLinesAsync(_auditLogFilePath, new[] { json }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit event to file");
        }
    }

    public async Task<List<AuditEvent>> SearchAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            return new List<AuditEvent>();
        }

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
            }

            var whereClauses = new List<string>();
            var parameters = new List<(string, object)>();

            if (!string.IsNullOrEmpty(criteria.User))
            {
                whereClauses.Add("user_name = @user");
                parameters.Add(("user", criteria.User));
            }

            if (!string.IsNullOrEmpty(criteria.Action))
            {
                whereClauses.Add("action = @action");
                parameters.Add(("action", criteria.Action));
            }

            if (!string.IsNullOrEmpty(criteria.ResourceType))
            {
                whereClauses.Add("resource_type = @resourceType");
                parameters.Add(("resourceType", criteria.ResourceType));
            }

            if (!string.IsNullOrEmpty(criteria.ResourceId))
            {
                whereClauses.Add("resource_id = @resourceId");
                parameters.Add(("resourceId", criteria.ResourceId));
            }

            if (criteria.FromDate.HasValue)
            {
                whereClauses.Add("timestamp >= @fromDate");
                parameters.Add(("fromDate", criteria.FromDate.Value));
            }

            if (criteria.ToDate.HasValue)
            {
                whereClauses.Add("timestamp <= @toDate");
                parameters.Add(("toDate", criteria.ToDate.Value));
            }

            var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
            
            var sql = $@"
                SELECT id, timestamp, user_name, action, resource_type, resource_id,
                    old_state, new_state, ip_address, user_agent, status, error_code, error_message, metadata
                FROM audit_events
                {whereClause}
                ORDER BY timestamp DESC
                LIMIT @limit OFFSET @offset";

            await using var cmd = new NpgsqlCommand(sql, _connection);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }
            cmd.Parameters.AddWithValue("limit", criteria.Limit);
            cmd.Parameters.AddWithValue("offset", criteria.Offset);

            var results = new List<AuditEvent>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                var auditEvent = new AuditEvent
                {
                    Id = reader.GetString(0),
                    Timestamp = reader.GetDateTime(1),
                    User = reader.GetString(2),
                    Action = reader.GetString(3),
                    ResourceType = reader.GetString(4),
                    ResourceId = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IpAddress = reader.IsDBNull(8) ? null : reader.GetString(8),
                    UserAgent = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Status = reader.GetString(10),
                    ErrorCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                    ErrorMessage = reader.IsDBNull(12) ? null : reader.GetString(12)
                };

                if (!reader.IsDBNull(6))
                {
                    auditEvent.OldState = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(6));
                }

                if (!reader.IsDBNull(7))
                {
                    auditEvent.NewState = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(7));
                }

                if (!reader.IsDBNull(13))
                {
                    auditEvent.Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(13));
                }

                results.Add(auditEvent);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search audit events");
            return new List<AuditEvent>();
        }
    }
}
