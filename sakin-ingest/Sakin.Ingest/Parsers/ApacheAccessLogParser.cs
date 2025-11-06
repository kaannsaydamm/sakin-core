using Sakin.Common.Models;
using Sakin.Ingest.Parsers.Utilities;

namespace Sakin.Ingest.Parsers;

public class ApacheAccessLogParser : IEventParser
{
    public string SourceType => "apache";

    private static readonly GrokMatcher CommonLogPattern = new(
        "^(?<ip>\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3})\\s+(?:\\S+\\s+){2}(?<user>\\S+)\\s+\\[(?<timestamp>[^\\]]+)\\]\\s+\"(?<method>\\S+)\\s+(?<path>\\S+)\\s+(?<httpversion>HTTP/[\\d.]+)\"\\s+(?<statuscode>\\d{3})\\s+(?<bytessize>\\d+|-)\\s+\"(?<referrer>[^\"]*)\"\\s+\"(?<useragent>[^\"]*)\"");

    private static readonly GrokMatcher CombinedLogPattern = new(
        "^(?<ip>\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3})\\s+(?:\\S+\\s+){2}(?<user>\\S+)\\s+\\[(?<timestamp>[^\\]]+)\\]\\s+\"(?<method>\\S+)\\s+(?<path>\\S+)\\s+(?<httpversion>HTTP/[\\d.]+)\"\\s+(?<statuscode>\\d{3})\\s+(?<bytessize>\\d+|-)\\s+\"(?<referrer>[^\"]*)\"\\s+\"(?<useragent>[^\"]*)\"\\s+(?<responsetime>\\S+)?");

    public async Task<NormalizedEvent> ParseAsync(EventEnvelope raw)
    {
        return await Task.FromResult(ParseApacheLog(raw));
    }

    private static NormalizedEvent ParseApacheLog(EventEnvelope raw)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var logLine = raw.Raw.Trim();

            string sourceIp = string.Empty;
            string method = string.Empty;
            string path = string.Empty;
            string userAgent = string.Empty;
            string referrer = string.Empty;
            int statusCode = 0;
            int responseTime = 0;
            DateTime timestamp = DateTime.UtcNow;

            if (CombinedLogPattern.TryMatch(logLine, out var groups))
            {
                sourceIp = groups.GetValueOrDefault("ip", string.Empty);
                method = groups.GetValueOrDefault("method", string.Empty);
                path = groups.GetValueOrDefault("path", string.Empty);
                userAgent = groups.GetValueOrDefault("useragent", string.Empty);
                referrer = groups.GetValueOrDefault("referrer", string.Empty);

                if (int.TryParse(groups.GetValueOrDefault("statuscode"), out var code))
                {
                    statusCode = code;
                }

                if (groups.TryGetValue("responsetime", out var respTime) && int.TryParse(respTime, out var parsedTime))
                {
                    responseTime = parsedTime;
                }

                if (groups.TryGetValue("timestamp", out var ts))
                {
                    TimeParser.TryParse(ts, out timestamp);
                }
            }

            IpParser.TryValidateAndNormalize(sourceIp, out sourceIp);

            var eventType = DetermineEventType(statusCode);
            var severity = DetermineSeverity(statusCode);
            var protocol = DetermineProtocol(method);

            metadata["http_method"] = method;
            metadata["http_status"] = statusCode;
            metadata["path"] = path;
            metadata["user_agent"] = userAgent;
            metadata["referrer"] = referrer;
            if (responseTime > 0)
            {
                metadata["response_time_ms"] = responseTime;
            }

            return new NormalizedEvent
            {
                Id = raw.EventId,
                Timestamp = timestamp,
                EventType = eventType,
                Severity = severity,
                SourceIp = sourceIp,
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
                EventType = EventType.HttpRequest,
                Severity = Severity.Low,
                Payload = raw.Raw,
                Metadata = metadata
            };
        }
    }

    private static EventType DetermineEventType(int statusCode)
    {
        return statusCode switch
        {
            >= 400 and < 500 => EventType.HttpRequest,
            >= 500 => EventType.SecurityAlert,
            _ => EventType.HttpRequest
        };
    }

    private static Severity DetermineSeverity(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => Severity.High,
            >= 400 and < 500 => Severity.Low,
            >= 300 and < 400 => Severity.Low,
            >= 200 and < 300 => Severity.Info,
            _ => Severity.Unknown
        };
    }

    private static Protocol DetermineProtocol(string method)
    {
        return method.ToUpper() switch
        {
            "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS" => Protocol.HTTP,
            _ => Protocol.Unknown
        };
    }
}
