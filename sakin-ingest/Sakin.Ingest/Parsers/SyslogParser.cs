using Sakin.Common.Models;
using Sakin.Ingest.Parsers.Utilities;

namespace Sakin.Ingest.Parsers;

public class SyslogParser : IEventParser
{
    public string SourceType => "syslog";

    private static readonly GrokMatcher Rfc5424Pattern = new(
        @"<(?<priority>\d+)>(?<version>\d+)\s+(?<timestamp>[^\s]+)\s+(?<hostname>[^\s]+)\s+(?<tag>[^\s\[]+)(?:\[(?<pid>\d+)\])?\s*:\s*(?<message>.*)");

    private static readonly GrokMatcher Rfc3164Pattern = new(
        @"(?<timestamp>[A-Za-z]{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2})\s+(?<hostname>[^\s]+)\s+(?<tag>[^\s\[]+)(?:\[(?<pid>\d+)\])?\s*:?\s*(?<message>.*)");

    public async Task<NormalizedEvent> ParseAsync(EventEnvelope raw)
    {
        return await Task.FromResult(ParseSyslog(raw));
    }

    private static NormalizedEvent ParseSyslog(EventEnvelope raw)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var logLine = raw.Raw.Trim();

            string hostname = string.Empty;
            string tag = string.Empty;
            string message = string.Empty;
            DateTime timestamp = DateTime.UtcNow;
            int severity = (int)Severity.Info;
            int priority = 0;

            // Try RFC5424 first
            if (Rfc5424Pattern.TryMatch(logLine, out var groups5424))
            {
                hostname = groups5424.GetValueOrDefault("hostname", string.Empty);
                tag = groups5424.GetValueOrDefault("tag", string.Empty);
                message = groups5424.GetValueOrDefault("message", string.Empty);

                if (int.TryParse(groups5424.GetValueOrDefault("priority"), out var pri))
                {
                    priority = pri;
                    severity = pri & 0x07; // Extract severity from priority
                }

                if (groups5424.TryGetValue("timestamp", out var ts))
                {
                    TimeParser.TryParse(ts, out timestamp);
                }
            }
            else if (Rfc3164Pattern.TryMatch(logLine, out var groups3164))
            {
                // Try RFC3164
                hostname = groups3164.GetValueOrDefault("hostname", string.Empty);
                tag = groups3164.GetValueOrDefault("tag", string.Empty);
                message = groups3164.GetValueOrDefault("message", string.Empty);

                if (groups3164.TryGetValue("timestamp", out var ts))
                {
                    TimeParser.TryParse(ts, out timestamp);
                }
            }
            else
            {
                // Fallback: try basic parsing
                message = logLine;
                hostname = raw.Source;
            }

            var eventType = DetermineEventType(tag, message);
            var parsedSeverity = DetermineSeverity(severity);

            metadata["tag"] = tag;
            metadata["priority"] = priority;
            metadata["message"] = message;

            // Extract common patterns from message
            ExtractCommonPatterns(message, metadata);

            var sourceIp = ExtractSourceIp(message);
            if (!string.IsNullOrEmpty(sourceIp) && !IpParser.TryValidateAndNormalize(sourceIp, out sourceIp))
            {
                sourceIp = string.Empty;
            }

            return new NormalizedEvent
            {
                Id = raw.EventId,
                Timestamp = timestamp,
                EventType = eventType,
                Severity = parsedSeverity,
                SourceIp = sourceIp,
                DeviceName = hostname,
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
                EventType = EventType.SystemLog,
                Severity = Severity.Low,
                Payload = raw.Raw,
                Metadata = metadata
            };
        }
    }

    private static EventType DetermineEventType(string tag, string message)
    {
        var combined = (tag + " " + message).ToLower();

        if (combined.Contains("ssh") || combined.Contains("sshd"))
            return EventType.SshConnection;

        if (combined.Contains("sudo"))
            return EventType.ProcessExecution;

        if (combined.Contains("failed") || combined.Contains("error") || combined.Contains("denied"))
            return EventType.SecurityAlert;

        if (combined.Contains("authentication") || combined.Contains("login") || combined.Contains("password"))
            return EventType.AuthenticationAttempt;

        if (combined.Contains("file") || combined.Contains("access"))
            return EventType.FileAccess;

        return EventType.SystemLog;
    }

    private static Severity DetermineSeverity(int syslogSeverity)
    {
        return syslogSeverity switch
        {
            0 => Severity.Critical, // Emergency
            1 => Severity.Critical, // Alert
            2 => Severity.Critical, // Critical
            3 => Severity.High,     // Error
            4 => Severity.Medium,   // Warning
            5 => Severity.Low,      // Notice
            6 => Severity.Low,      // Info
            7 => Severity.Low,      // Debug
            _ => Severity.Info
        };
    }

    private static void ExtractCommonPatterns(string message, Dictionary<string, object> metadata)
    {
        // SSH failed login
        if (message.Contains("Failed password") || message.Contains("Invalid user"))
        {
            metadata["event_type"] = "ssh_failed_login";
        }

        // Sudo command
        if (message.Contains("sudo") && (message.Contains("COMMAND") || message.Contains("TTY")))
        {
            metadata["event_type"] = "sudo_command";
        }

        // Access denied
        if (message.Contains("denied") || message.Contains("permission denied"))
        {
            metadata["event_type"] = "access_denied";
        }
    }

    private static string ExtractSourceIp(string message)
    {
        var ipPattern = RegexCache.GetOrCreate(@"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
        var match = ipPattern.Match(message);

        if (match.Success)
        {
            var ipGroup = match.Groups["ip"];
            if (ipGroup.Success)
            {
                return ipGroup.Value;
            }
        }

        return string.Empty;
    }
}
