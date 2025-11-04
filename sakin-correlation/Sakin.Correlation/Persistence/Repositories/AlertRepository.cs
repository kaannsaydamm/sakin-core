using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sakin.Correlation.Models;
using Sakin.Correlation.Persistence.Entities;
using Sakin.Correlation.Persistence.Models;

namespace Sakin.Correlation.Persistence.Repositories;

public class AlertRepository : IAlertRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AlertDbContext _context;

    public AlertRepository(AlertDbContext context)
    {
        _context = context;
    }

    public async Task<AlertRecord> CreateAsync(AlertRecord alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var now = DateTimeOffset.UtcNow;
        var entity = MapToEntity(alert);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = now;
        }

        entity.UpdatedAt = now;

        if (entity.TriggeredAt == default)
        {
            entity.TriggeredAt = entity.CreatedAt;
        }

        await _context.Alerts.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToModel(entity);
    }

    public async Task<AlertRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Alerts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<IReadOnlyList<AlertRecord>> GetRecentAlertsAsync(
        DateTimeOffset since,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default)
    {
        var severityValue = severity.HasValue ? ToSeverityString(severity.Value) : null;

        var query = _context.Alerts.AsNoTracking().Where(a => a.TriggeredAt >= since);

        if (severityValue is not null)
        {
            query = query.Where(a => a.Severity == severityValue);
        }

        var entities = await query
            .OrderByDescending(a => a.TriggeredAt)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<AlertRecord>> GetAlertsByRuleAsync(
        string ruleId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ruleId);

        if (limit <= 0)
        {
            limit = 100;
        }

        var entities = await _context.Alerts
            .AsNoTracking()
            .Where(a => a.RuleId == ruleId)
            .OrderByDescending(a => a.TriggeredAt)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToModel).ToList();
    }

    public async Task<(IReadOnlyList<AlertRecord> Alerts, int TotalCount)> GetAlertsAsync(
        int page = 1,
        int pageSize = 50,
        SeverityLevel? severity = null,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        var query = _context.Alerts.AsNoTracking();

        if (severity is not null)
        {
            var severityValue = ToSeverityString(severity.Value);
            query = query.Where(a => a.Severity == severityValue);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(a => a.TriggeredAt)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entities.Select(MapToModel).ToList(), totalCount);
    }

    public async Task<AlertRecord?> UpdateStatusAsync(
        Guid id,
        AlertStatus status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Alerts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var newStatus = ToStatusString(status);

        if (!string.Equals(entity.Status, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = newStatus;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToModel(entity);
    }

    private static AlertEntity MapToEntity(AlertRecord alert)
    {
        return new AlertEntity
        {
            Id = alert.Id,
            RuleId = alert.RuleId,
            RuleName = alert.RuleName,
            Severity = ToSeverityString(alert.Severity),
            Status = ToStatusString(alert.Status),
            TriggeredAt = alert.TriggeredAt,
            Source = alert.Source,
            CorrelationContext = SerializeContext(alert.Context),
            MatchedConditions = SerializeMatchedConditions(alert.MatchedConditions),
            AggregationCount = alert.AggregationCount,
            AggregatedValue = alert.AggregatedValue,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = alert.UpdatedAt
        };
    }

    private static AlertRecord MapToModel(AlertEntity entity)
    {
        return new AlertRecord
        {
            Id = entity.Id,
            RuleId = entity.RuleId,
            RuleName = entity.RuleName,
            Severity = ParseSeverity(entity.Severity),
            Status = ParseStatus(entity.Status),
            TriggeredAt = entity.TriggeredAt,
            Source = entity.Source,
            Context = DeserializeContext(entity.CorrelationContext),
            MatchedConditions = DeserializeMatchedConditions(entity.MatchedConditions),
            AggregationCount = entity.AggregationCount,
            AggregatedValue = entity.AggregatedValue,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static string ToSeverityString(SeverityLevel severity)
        => severity.ToString().ToLowerInvariant();

    private static SeverityLevel ParseSeverity(string? value)
        => Enum.TryParse<SeverityLevel>(value, true, out var result) ? result : SeverityLevel.Low;

    private static string ToStatusString(AlertStatus status)
        => status.ToString().ToLowerInvariant();

    private static AlertStatus ParseStatus(string? value)
        => Enum.TryParse<AlertStatus>(value, true, out var result) ? result : AlertStatus.New;

    private static string SerializeContext(Dictionary<string, object?>? context)
    {
        context ??= new Dictionary<string, object?>();
        return JsonSerializer.Serialize(context, SerializerOptions);
    }

    private static Dictionary<string, object?> DeserializeContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }

        using var document = JsonDocument.Parse(json);
        return ConvertElement(document.RootElement) as Dictionary<string, object?> ?? new Dictionary<string, object?>();
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l
                : element.TryGetDouble(out var d)
                    ? d
                    : null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertElement(property.Value);
        }

        return dictionary;
    }

    private static List<object?> ConvertArray(JsonElement element)
    {
        var list = new List<object?>();

        foreach (var item in element.EnumerateArray())
        {
            list.Add(ConvertElement(item));
        }

        return list;
    }

    private static string SerializeMatchedConditions(IReadOnlyList<string>? matchedConditions)
    {
        matchedConditions ??= Array.Empty<string>();
        return JsonSerializer.Serialize(matchedConditions, SerializerOptions);
    }

    private static IReadOnlyList<string> DeserializeMatchedConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json, SerializerOptions);
            return values?.ToArray() ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
