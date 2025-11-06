using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;
using Sakin.Common.Models;
using Sakin.Common.Utilities;
using Sakin.Ingest.Configuration;
using Sakin.Ingest.Parsers;
using Sakin.Ingest.Services;
using Sakin.Messaging.Consumer;
using Sakin.Messaging.Producer;

namespace Sakin.Ingest.Workers;

public class EventIngestWorker(
    IKafkaConsumer consumer,
    IKafkaProducer producer,
    IOptions<IngestKafkaOptions> options,
    ParserRegistry parserRegistry,
    IGeoIpService geoIpService,
    IRedisClient redisClient,
    IOptions<ThreatIntelOptions> threatOptions,
    ILogger<EventIngestWorker> logger) : BackgroundService
{
    private readonly IKafkaConsumer _consumer = consumer;
    private readonly IKafkaProducer _producer = producer;
    private readonly IngestKafkaOptions _options = options.Value;
    private readonly ParserRegistry _parserRegistry = parserRegistry;
    private readonly IGeoIpService _geoIpService = geoIpService;
    private readonly IRedisClient _redisClient = redisClient;
    private readonly ThreatIntelOptions _threatIntelOptions = threatOptions.Value;
    private readonly ILogger<EventIngestWorker> _logger = logger;

    private static readonly Regex DomainRegex = new("^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HashRegex = new("^[a-f0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions ThreatIntelSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string RawTopic => string.IsNullOrWhiteSpace(_options.RawEventsTopic)
        ? "raw-events"
        : _options.RawEventsTopic;

    private string NormalizedTopic => string.IsNullOrWhiteSpace(_options.NormalizedEventsTopic)
        ? "normalized-events"
        : _options.NormalizedEventsTopic;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting ingest worker with raw topic {RawTopic}, normalized topic {NormalizedTopic}, consumer group {ConsumerGroup}",
            RawTopic,
            NormalizedTopic,
            _options.ConsumerGroup);

        try
        {
            await _consumer.ConsumeAsync<EventEnvelope>(HandleMessageAsync, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Ingest worker cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ingest worker");
            throw;
        }
    }

    private async Task HandleMessageAsync(ConsumeResult<EventEnvelope> result)
    {
        if (result.Message is null)
        {
            _logger.LogWarning("Received null message on topic {Topic}", result.Topic);
            return;
        }

        _logger.LogInformation(
            "Processing raw event {EventId} from source {Source} with sourceType {SourceType} (topic: {Topic}, partition: {Partition}, offset: {Offset})",
            result.Message.EventId,
            result.Message.Source,
            result.Message.SourceType,
            result.Topic,
            result.Partition,
            result.Offset);

        var normalizedEnvelope = await BuildNormalizedEnvelopeAsync(result.Message);
        var messageKey = normalizedEnvelope.EventId.ToString();

        await _producer.ProduceAsync(
            NormalizedTopic,
            normalizedEnvelope,
            messageKey);

        _logger.LogInformation(
            "Published normalized event {EventId} to topic {Topic}",
            normalizedEnvelope.EventId,
            NormalizedTopic);
    }

    private async Task<EventEnvelope> BuildNormalizedEnvelopeAsync(EventEnvelope envelope)
    {
        NormalizedEvent normalizedEvent;

        var parser = _parserRegistry.GetParser(envelope.SourceType);
        if (parser is not null)
        {
            try
            {
                normalizedEvent = await parser.ParseAsync(envelope);

                if (envelope.Enrichment is { Count: > 0 } enrichment)
                {
                    var mergedMetadata = new Dictionary<string, object>(enrichment);
                    foreach (var kvp in normalizedEvent.Metadata)
                    {
                        if (!mergedMetadata.ContainsKey(kvp.Key))
                        {
                            mergedMetadata[kvp.Key] = kvp.Value;
                        }
                    }
                    normalizedEvent = normalizedEvent with { Metadata = mergedMetadata };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parser for sourceType {SourceType} failed, using passthrough normalization", envelope.SourceType);
                normalizedEvent = BuildPassthroughNormalizedEvent(envelope);
            }
        }
        else
        {
            _logger.LogWarning("No parser found for sourceType {SourceType}, using passthrough normalization", envelope.SourceType);
            normalizedEvent = BuildPassthroughNormalizedEvent(envelope);
        }

        // Apply GeoIP enrichment if enabled and we have IP addresses
        var geoEnrichedEnvelope = ApplyGeoIpEnrichment(envelope, normalizedEvent);
        var threatIntelEnrichedEnvelope = await ApplyThreatIntelEnrichmentAsync(geoEnrichedEnvelope);

        return threatIntelEnrichedEnvelope;
    }

    private EventEnvelope ApplyGeoIpEnrichment(EventEnvelope envelope, NormalizedEvent normalizedEvent)
    {
        var enrichment = new Dictionary<string, object>(envelope.Enrichment);

        // Enrich source IP if present
        if (!string.IsNullOrWhiteSpace(normalizedEvent.SourceIp))
        {
            var sourceGeo = _geoIpService.Lookup(normalizedEvent.SourceIp);
            if (sourceGeo != null)
            {
                enrichment["source_geo"] = sourceGeo;
                _logger.LogDebug("Enriched source IP {SourceIp} with GeoIP data: {Country}, {City}", 
                    normalizedEvent.SourceIp, sourceGeo.Country, sourceGeo.City);
            }
        }

        // Enrich destination IP if present
        if (!string.IsNullOrWhiteSpace(normalizedEvent.DestinationIp))
        {
            var destGeo = _geoIpService.Lookup(normalizedEvent.DestinationIp);
            if (destGeo != null)
            {
                enrichment["dest_geo"] = destGeo;
                _logger.LogDebug("Enriched destination IP {DestinationIp} with GeoIP data: {Country}, {City}", 
                    normalizedEvent.DestinationIp, destGeo.Country, destGeo.City);
            }
        }

        return envelope with
        {
            Normalized = normalizedEvent,
            Enrichment = enrichment
        };
    }

    private async Task<EventEnvelope> ApplyThreatIntelEnrichmentAsync(EventEnvelope envelope)
    {
        if (!_threatIntelOptions.Enabled)
        {
            return envelope;
        }

        if (envelope.Normalized is null)
        {
            return envelope;
        }

        var indicators = ExtractIndicators(envelope.Normalized);
        if (indicators.Count == 0)
        {
            return envelope;
        }

        var enrichmentUpdates = new Dictionary<string, ThreatIntelScore>();
        var producedIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lookupTopic = _threatIntelOptions.LookupTopic;

        foreach (var indicator in indicators)
        {
            var cacheKey = BuildThreatIntelCacheKey(indicator);

            ThreatIntelScore? cachedScore = null;
            try
            {
                var cachedValue = await _redisClient.StringGetAsync(cacheKey);
                if (!string.IsNullOrWhiteSpace(cachedValue))
                {
                    cachedScore = JsonSerializer.Deserialize<ThreatIntelScore>(cachedValue, ThreatIntelSerializerOptions);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize threat intel cache entry for key {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving threat intel cache entry for key {CacheKey}", cacheKey);
            }

            if (cachedScore != null)
            {
                enrichmentUpdates[indicator.EnrichmentKey] = cachedScore;
                continue;
            }

            if (string.IsNullOrWhiteSpace(lookupTopic))
            {
                _logger.LogWarning("Threat intel lookup topic is not configured. Skipping lookup for {IndicatorType} {Value}", indicator.IndicatorType, indicator.Value);
                continue;
            }

            var dedupeKey = $"{indicator.IndicatorType}:{indicator.HashType?.ToString() ?? string.Empty}:{indicator.Value}";
            if (!producedIndicators.Add(dedupeKey))
            {
                continue;
            }

            var request = new ThreatIntelLookupRequest
            {
                Type = indicator.IndicatorType,
                Value = indicator.Value,
                HashType = indicator.HashType
            };

            try
            {
                await _producer.ProduceAsync(lookupTopic, request, indicator.Value);
                _logger.LogDebug("Enqueued threat intel lookup for {IndicatorType} {Value}", indicator.IndicatorType, indicator.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue threat intel lookup for {IndicatorType} {Value}", indicator.IndicatorType, indicator.Value);
            }
        }

        if (enrichmentUpdates.Count == 0)
        {
            return envelope;
        }

        var enrichment = new Dictionary<string, object>(envelope.Enrichment);

        if (enrichment.TryGetValue("threat_intel", out var existing) && existing is Dictionary<string, ThreatIntelScore> existingScores)
        {
            foreach (var kvp in enrichmentUpdates)
            {
                existingScores[kvp.Key] = kvp.Value;
            }

            enrichment["threat_intel"] = existingScores;
        }
        else
        {
            enrichment["threat_intel"] = enrichmentUpdates;
        }

        return envelope with { Enrichment = enrichment };
    }

    private List<ThreatIntelIndicatorCandidate> ExtractIndicators(NormalizedEvent normalizedEvent)
    {
        var indicators = new List<ThreatIntelIndicatorCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIndicator(string value, string enrichmentKey, ThreatIntelIndicatorType type, ThreatIntelHashType? hashType = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalizedValue = value.Trim().ToLowerInvariant();
            var dedupeKey = $"{type}:{hashType?.ToString() ?? string.Empty}:{normalizedValue}";
            if (!seen.Add(dedupeKey))
            {
                return;
            }

            indicators.Add(new ThreatIntelIndicatorCandidate(normalizedValue, enrichmentKey, type, hashType));
        }

        if (!string.IsNullOrWhiteSpace(normalizedEvent.SourceIp) &&
            IPAddress.TryParse(normalizedEvent.SourceIp, out var sourceIp) &&
            !IsPrivateIp(sourceIp))
        {
            var type = sourceIp.AddressFamily == AddressFamily.InterNetwork
                ? ThreatIntelIndicatorType.Ipv4
                : ThreatIntelIndicatorType.Ipv6;
            AddIndicator(normalizedEvent.SourceIp, "source_ip", type);
        }

        if (!string.IsNullOrWhiteSpace(normalizedEvent.DestinationIp) &&
            IPAddress.TryParse(normalizedEvent.DestinationIp, out var destinationIp) &&
            !IsPrivateIp(destinationIp))
        {
            var type = destinationIp.AddressFamily == AddressFamily.InterNetwork
                ? ThreatIntelIndicatorType.Ipv4
                : ThreatIntelIndicatorType.Ipv6;
            AddIndicator(normalizedEvent.DestinationIp, "destination_ip", type);
        }

        foreach (var kvp in normalizedEvent.Metadata)
        {
            if (kvp.Value is null)
            {
                continue;
            }

            var metadataKey = NormalizeMetadataKey(kvp.Key);

            switch (kvp.Value)
            {
                case string strValue:
                    EvaluateMetadataValue(strValue, metadataKey);
                    break;
                case JsonElement jsonElement:
                    EvaluateJsonElement(jsonElement, metadataKey);
                    break;
                case IEnumerable enumerable:
                    foreach (var item in enumerable)
                    {
                        if (item is string str)
                        {
                            EvaluateMetadataValue(str, metadataKey);
                        }
                        else if (item is JsonElement element)
                        {
                            EvaluateJsonElement(element, metadataKey);
                        }
                    }
                    break;
            }
        }

        return indicators;

        void EvaluateMetadataValue(string value, string metadataKey)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();

            if (IPAddress.TryParse(trimmed, out var ipValue) && !IsPrivateIp(ipValue))
            {
                var type = ipValue.AddressFamily == AddressFamily.InterNetwork
                    ? ThreatIntelIndicatorType.Ipv4
                    : ThreatIntelIndicatorType.Ipv6;
                AddIndicator(trimmed, BuildMetadataEnrichmentKey(metadataKey, type), type);
                return;
            }

            if (TryParseHash(trimmed, out var hashType))
            {
                AddIndicator(trimmed, BuildMetadataEnrichmentKey(metadataKey, ThreatIntelIndicatorType.FileHash), ThreatIntelIndicatorType.FileHash, hashType);
                return;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                AddIndicator(uri.Host, BuildMetadataEnrichmentKey(metadataKey, ThreatIntelIndicatorType.Domain), ThreatIntelIndicatorType.Domain);
                AddIndicator(trimmed, BuildMetadataEnrichmentKey(metadataKey, ThreatIntelIndicatorType.Url), ThreatIntelIndicatorType.Url);
                return;
            }

            if (DomainRegex.IsMatch(trimmed))
            {
                AddIndicator(trimmed, BuildMetadataEnrichmentKey(metadataKey, ThreatIntelIndicatorType.Domain), ThreatIntelIndicatorType.Domain);
            }
        }

        void EvaluateJsonElement(JsonElement element, string metadataKey)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    EvaluateMetadataValue(element.GetString() ?? string.Empty, metadataKey);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        EvaluateJsonElement(item, metadataKey);
                    }
                    break;
            }
        }
    }

    private static string BuildThreatIntelCacheKey(ThreatIntelIndicatorCandidate indicator)
    {
        return ThreatIntelCacheKeyBuilder.BuildCacheKey(indicator.IndicatorType, indicator.Value, indicator.HashType);
    }

    private static string BuildMetadataEnrichmentKey(string metadataKey, ThreatIntelIndicatorType type)
    {
        var typeSegment = type switch
        {
            ThreatIntelIndicatorType.Ipv4 or ThreatIntelIndicatorType.Ipv6 => "ip",
            ThreatIntelIndicatorType.Domain => "domain",
            ThreatIntelIndicatorType.Url => "url",
            ThreatIntelIndicatorType.FileHash => "hash",
            _ => type.ToString().ToLowerInvariant()
        };

        return $"metadata.{metadataKey}.{typeSegment}";
    }

    private static string NormalizeMetadataKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "unknown";
        }

        var normalized = new string(key.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        normalized = normalized.Trim('_');

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static bool TryParseHash(string value, out ThreatIntelHashType hashType)
    {
        hashType = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (!HashRegex.IsMatch(trimmed))
        {
            return false;
        }

        switch (trimmed.Length)
        {
            case 32:
                hashType = ThreatIntelHashType.Md5;
                return true;
            case 40:
                hashType = ThreatIntelHashType.Sha1;
                return true;
            case 64:
                hashType = ThreatIntelHashType.Sha256;
                return true;
            default:
                return false;
        }
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (bytes[0] == 0xfc || bytes[0] == 0xfd)
            {
                return true;
            }

            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            {
                return true;
            }

            return false;
        }

        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true;
        }

        return false;
    }

    private sealed record ThreatIntelIndicatorCandidate(
        string Value,
        string EnrichmentKey,
        ThreatIntelIndicatorType IndicatorType,
        ThreatIntelHashType? HashType);

    private static NormalizedEvent BuildPassthroughNormalizedEvent(EventEnvelope envelope)
    {
        var normalizationMetadata = envelope.Enrichment is { Count: > 0 } enrichment
            ? new Dictionary<string, object>(enrichment)
            : new Dictionary<string, object>();

        return new NormalizedEvent
        {
            Id = envelope.EventId,
            Timestamp = envelope.ReceivedAt.UtcDateTime,
            Payload = envelope.Raw,
            Metadata = normalizationMetadata
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ingest worker");

        try
        {
            await _producer.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while flushing producer during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
