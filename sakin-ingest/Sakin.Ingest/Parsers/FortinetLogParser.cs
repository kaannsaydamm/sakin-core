using Sakin.Common.Models;
using Sakin.Ingest.Parsers.Utilities;

namespace Sakin.Ingest.Parsers;

public class FortinetLogParser : IEventParser
{
    public string SourceType => "fortinet";

    private static readonly GrokMatcher FortinetCefPattern = new(
        @"CEF:0\|Fortinet\|FortiGate\|[^\|]*\|(?<signatureId>[^\|]*)\|(?<signatureName>[^\|]*)\|(?<severity>\d+)\|(?<cefContent>.*)");

    private static readonly GrokMatcher FortinetKeyValuePattern = new(
        @"action=(?<action>\w+).*?srcip=(?<srcip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}).*?dstip=(?<dstip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}).*?srcport=(?<srcport>\d+).*?dstport=(?<dstport>\d+).*?proto=(?<proto>\d+)");

    public async Task<NormalizedEvent> ParseAsync(EventEnvelope raw)
    {
        return await Task.FromResult(ParseFortinetLog(raw));
    }

    private static NormalizedEvent ParseFortinetLog(EventEnvelope raw)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var logLine = raw.Raw.Trim();

            string action = "unknown";
            string sourceIp = string.Empty;
            string destinationIp = string.Empty;
            int sourcePort = 0;
            int destinationPort = 0;
            Protocol protocol = Protocol.Unknown;
            DateTime timestamp = DateTime.UtcNow;
            EventType eventType = EventType.NetworkTraffic;
            Severity severity = Severity.Info;

            // Try CEF format first
            if (FortinetCefPattern.TryMatch(logLine, out var cefGroups))
            {
                var signatureName = cefGroups.GetValueOrDefault("signatureName", string.Empty);
                action = cefGroups.GetValueOrDefault("action", "unknown");

                if (int.TryParse(cefGroups.GetValueOrDefault("severity"), out var cefSev))
                {
                    severity = MapCefSeverity(cefSev);
                }

                metadata["signature_id"] = cefGroups.GetValueOrDefault("signatureId", string.Empty);
                metadata["signature_name"] = signatureName;

                // Parse key-value content
                if (cefGroups.TryGetValue("cefContent", out var kvContent))
                {
                    ParseKeyValueContent(kvContent, out sourceIp, out destinationIp, out sourcePort, out destinationPort, out protocol, out action, metadata);
                }
            }
            else
            {
                // Try parsing as key-value format directly
                ParseKeyValueContent(logLine, out sourceIp, out destinationIp, out sourcePort, out destinationPort, out protocol, out action, metadata);
            }

            if (!string.IsNullOrEmpty(sourceIp))
            {
                IpParser.TryValidateAndNormalize(sourceIp, out sourceIp);
            }

            if (!string.IsNullOrEmpty(destinationIp))
            {
                IpParser.TryValidateAndNormalize(destinationIp, out destinationIp);
            }

            if (action.Equals("deny", StringComparison.OrdinalIgnoreCase) || action.Equals("drop", StringComparison.OrdinalIgnoreCase))
            {
                eventType = EventType.SecurityAlert;
                severity = severity == Severity.Info ? Severity.Medium : severity;
            }

            metadata["action"] = action;

            return new NormalizedEvent
            {
                Id = raw.EventId,
                Timestamp = timestamp,
                EventType = eventType,
                Severity = severity,
                SourceIp = sourceIp,
                DestinationIp = destinationIp,
                SourcePort = sourcePort > 0 ? sourcePort : null,
                DestinationPort = destinationPort > 0 ? destinationPort : null,
                Protocol = protocol,
                Payload = raw.Raw,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            metadata["parse_error"] = ex.Message;

            return new NormalizedEvent
            {
                Id = raw.EventId,
                Timestamp = DateTime.UtcNow,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Low,
                Payload = raw.Raw,
                Metadata = metadata
            };
        }
    }

    private static void ParseKeyValueContent(string content, out string sourceIp, out string destinationIp, 
        out int sourcePort, out int destinationPort, out Protocol protocol, out string action, 
        Dictionary<string, object> metadata)
    {
        sourceIp = string.Empty;
        destinationIp = string.Empty;
        sourcePort = 0;
        destinationPort = 0;
        protocol = Protocol.Unknown;
        action = "unknown";

        var fields = content.Split(' ');
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;

            var parts = field.Split('=');
            if (parts.Length != 2)
                continue;

            var key = parts[0].ToLower();
            var value = parts[1];

            switch (key)
            {
                case "action":
                    action = value;
                    break;
                case "srcip":
                    sourceIp = value;
                    break;
                case "dstip":
                    destinationIp = value;
                    break;
                case "srcport":
                    int.TryParse(value, out sourcePort);
                    break;
                case "dstport":
                    int.TryParse(value, out destinationPort);
                    break;
                case "proto":
                    protocol = MapProtocol(value);
                    break;
                case "policyid":
                    metadata["policy_id"] = value;
                    break;
                case "service":
                    metadata["service"] = value;
                    break;
                default:
                    if (!metadata.ContainsKey(key))
                    {
                        metadata[key] = value;
                    }
                    break;
            }
        }
    }

    private static Protocol MapProtocol(string protoNumber)
    {
        if (!int.TryParse(protoNumber, out var proto))
            return Protocol.Unknown;

        return proto switch
        {
            6 => Protocol.TCP,
            17 => Protocol.UDP,
            1 => Protocol.ICMP,
            _ => Protocol.Unknown
        };
    }

    private static Severity MapCefSeverity(int cefSeverity)
    {
        return cefSeverity switch
        {
            >= 8 => Severity.Critical,
            >= 6 => Severity.High,
            >= 4 => Severity.Medium,
            >= 2 => Severity.Low,
            _ => Severity.Info
        };
    }
}
