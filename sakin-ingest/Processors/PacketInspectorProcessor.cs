using Microsoft.Extensions.Logging;
using Sakin.Common.Models;
using Sakin.Ingest.Pipelines;
using System.Text.Json;

namespace Sakin.Ingest.Processors
{
    public class PacketInspectorProcessor : IEventProcessor
    {
        private readonly ILogger<PacketInspectorProcessor> _logger;

        public string Name => "PacketInspector";
        public int Priority => 1;

        public PacketInspectorProcessor(ILogger<PacketInspectorProcessor> logger)
        {
            _logger = logger;
        }

        public async Task<NormalizedEvent?> ProcessAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
        {
            // Only process events from PacketInspector source
            if (!rawEvent.Metadata.TryGetValue("source", out var sourceObj) || 
                sourceObj?.ToString().Equals("packet-inspector", StringComparison.OrdinalIgnoreCase) != true)
            {
                return null;
            }

            try
            {
                // Parse the raw JSON data from PacketInspector
                var packetData = JsonSerializer.Deserialize<PacketInspectorData>(rawEvent.Data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (packetData == null)
                {
                    _logger.LogWarning("Failed to deserialize packet data for event {EventId}", rawEvent.Id);
                    return null;
                }

                // Convert to normalized network event
                var networkEvent = new NetworkEvent
                {
                    Id = Guid.Parse(rawEvent.Id),
                    Timestamp = packetData.Timestamp,
                    EventType = EventType.NetworkTraffic,
                    Severity = Severity.Info,
                    SourceIp = packetData.SrcIp,
                    DestinationIp = packetData.DstIp,
                    SourcePort = packetData.SrcPort,
                    DestinationPort = packetData.DstPort,
                    Protocol = ParseProtocol(packetData.Protocol),
                    BytesSent = packetData.BytesSent ?? 0,
                    BytesReceived = packetData.BytesReceived ?? 0,
                    PacketCount = packetData.PacketCount ?? 1,
                    Sni = packetData.Sni,
                    HttpUrl = packetData.HttpUrl,
                    HttpMethod = packetData.HttpMethod,
                    HttpStatusCode = packetData.HttpStatusCode,
                    UserAgent = packetData.UserAgent,
                    Metadata = new Dictionary<string, object>
                    {
                        ["original_source"] = rawEvent.Source,
                        ["format"] = rawEvent.Format,
                        ["interface"] = packetData.Interface ?? "unknown"
                    }
                };

                _logger.LogDebug("Successfully processed packet inspector event {EventId}", rawEvent.Id);
                return await Task.FromResult(networkEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packet inspector event {EventId}", rawEvent.Id);
                return null;
            }
        }

        private Protocol ParseProtocol(string? protocol)
        {
            return protocol?.ToLowerInvariant() switch
            {
                "tcp" => Protocol.TCP,
                "udp" => Protocol.UDP,
                "icmp" => Protocol.ICMP,
                "http" => Protocol.HTTP,
                "https" => Protocol.HTTPS,
                "tls" or "ssl" => Protocol.TLS,
                _ => Protocol.Unknown
            };
        }

        private record PacketInspectorData
        {
            public DateTime Timestamp { get; init; }
            public string SrcIp { get; init; } = string.Empty;
            public string DstIp { get; init; } = string.Empty;
            public int? SrcPort { get; init; }
            public int? DstPort { get; init; }
            public string Protocol { get; init; } = string.Empty;
            public long? BytesSent { get; init; }
            public long? BytesReceived { get; init; }
            public int? PacketCount { get; init; }
            public string? Sni { get; init; }
            public string? HttpUrl { get; init; }
            public string? HttpMethod { get; init; }
            public int? HttpStatusCode { get; init; }
            public string? UserAgent { get; init; }
            public string? Interface { get; init; }
        }
    }
}