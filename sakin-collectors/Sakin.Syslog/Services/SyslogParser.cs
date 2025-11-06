using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sakin.Syslog.Models;

namespace Sakin.Syslog.Services
{
    public class SyslogParser
    {
        private readonly ILogger<SyslogParser> _logger;
        
        // RFC3164 format: <Priority>Jan 23 12:34:56 hostname tag: message
        private static readonly Regex Rfc3164Pattern = new Regex(
            @"^<(?<priority>\d{1,3})>(?<timestamp>[A-Za-z]{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2})\s+(?<hostname>\S+)\s+(?<tag>[^:\s]+)(?:\[(?<pid>\d+)\])?\s*:\s*(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        // RFC5424 format: <Priority>1 2003-10-11T22:14:15.003Z hostname app-name procid msgid structured-data msg
        private static readonly Regex Rfc5424Pattern = new Regex(
            @"^<(?<priority>\d{1,3})>(?<version>\d+)\s+(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)\s+(?<hostname>\S+)\s+(?<appname>\S+)\s+(?<procid>\S+)\s+(?<msgid>\S+)\s+(?<structured_data>\[.*?\]|\-)\s*(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public SyslogParser(ILogger<SyslogParser> logger)
        {
            _logger = logger;
        }
        
        public SyslogMessage Parse(string rawMessage, string remoteEndpoint)
        {
            var message = new SyslogMessage
            {
                Raw = rawMessage,
                RemoteEndpoint = remoteEndpoint
            };
            
            try
            {
                // Try RFC5424 first (more structured)
                var match5424 = Rfc5424Pattern.Match(rawMessage);
                if (match5424.Success)
                {
                    return ParseRfc5424(match5424, message);
                }
                
                // Fall back to RFC3164
                var match3164 = Rfc3164Pattern.Match(rawMessage);
                if (match3164.Success)
                {
                    return ParseRfc3164(match3164, message);
                }
                
                // If neither pattern matches, treat as simple message
                return ParseSimple(rawMessage, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse syslog message from {Endpoint}: {Message}", remoteEndpoint, rawMessage);
                return ParseSimple(rawMessage, message);
            }
        }
        
        private static SyslogMessage ParseRfc5424(Match match, SyslogMessage message)
        {
            if (int.TryParse(match.Groups["priority"].Value, out var priority))
            {
                message.Priority = priority;
                message.Facility = priority >> 3;
                message.Severity = priority & 0x07;
            }
            
            if (DateTimeOffset.TryParse(match.Groups["timestamp"].Value, out var timestamp))
            {
                message.Timestamp = timestamp;
            }
            
            message.Hostname = match.Groups["hostname"].Value != "-" ? match.Groups["hostname"].Value : string.Empty;
            message.Tag = match.Groups["appname"].Value != "-" ? match.Groups["appname"].Value : string.Empty;
            message.Message = match.Groups["message"].Value ?? string.Empty;
            
            return message;
        }
        
        private static SyslogMessage ParseRfc3164(Match match, SyslogMessage message)
        {
            if (int.TryParse(match.Groups["priority"].Value, out var priority))
            {
                message.Priority = priority;
                message.Facility = priority >> 3;
                message.Severity = priority & 0x07;
            }
            
            // Parse timestamp (add current year since RFC3164 doesn't include it)
            var timestampStr = match.Groups["timestamp"].Value;
            if (DateTime.TryParse($"{DateTime.Now.Year} {timestampStr}", out var timestamp))
            {
                message.Timestamp = timestamp;
            }
            
            message.Hostname = match.Groups["hostname"].Value;
            message.Tag = match.Groups["tag"].Value;
            message.Message = match.Groups["message"].Value ?? string.Empty;
            
            return message;
        }
        
        private static SyslogMessage ParseSimple(string rawMessage, SyslogMessage message)
        {
            // Try to extract priority if present
            if (rawMessage.StartsWith("<"))
            {
                var endIndex = rawMessage.IndexOf('>');
                if (endIndex > 1 && int.TryParse(rawMessage.Substring(1, endIndex - 1), out var priority))
                {
                    message.Priority = priority;
                    message.Facility = priority >> 3;
                    message.Severity = priority & 0x07;
                    message.Message = rawMessage.Substring(endIndex + 1).TrimStart();
                }
                else
                {
                    message.Message = rawMessage;
                }
            }
            else
            {
                message.Message = rawMessage;
            }
            
            return message;
        }
    }
}