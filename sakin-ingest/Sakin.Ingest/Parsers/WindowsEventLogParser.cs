using System.Xml;
using Sakin.Common.Models;
using Sakin.Ingest.Parsers.Utilities;

namespace Sakin.Ingest.Parsers;

public class WindowsEventLogParser : IEventParser
{
    public string SourceType => "windows-eventlog";

    private static readonly Dictionary<uint, (string ActionName, EventType EventType, Severity Severity)> EventCodeMappings = new()
    {
        // Authentication events
        { 4625, ("login_failed", EventType.AuthenticationAttempt, Severity.Medium) },
        { 4624, ("login_success", EventType.AuthenticationAttempt, Severity.Info) },
        { 4720, ("user_created", EventType.SystemLog, Severity.Info) },
        { 4721, ("user_enabled", EventType.SystemLog, Severity.Info) },
        { 4722, ("user_disabled", EventType.SystemLog, Severity.Info) },
        { 4725, ("user_disabled_by_admin", EventType.SystemLog, Severity.Info) },
        { 4726, ("user_deleted", EventType.SystemLog, Severity.Info) },
        { 4740, ("account_lockout", EventType.SecurityAlert, Severity.Medium) },
        { 4767, ("account_unlocked", EventType.SystemLog, Severity.Info) },
        
        // Privilege escalation
        { 4672, ("privilege_assignment", EventType.SecurityAlert, Severity.High) },
        { 4688, ("process_creation", EventType.ProcessExecution, Severity.Info) },
        
        // File access
        { 4656, ("file_object_accessed", EventType.FileAccess, Severity.Low) },
        { 4658, ("object_handle_closed", EventType.FileAccess, Severity.Low) },
        { 4660, ("object_deleted", EventType.FileAccess, Severity.Medium) },
        { 4663, ("attempt_made_to_access_object", EventType.FileAccess, Severity.Low) },
        
        // Group policy
        { 5136, ("directory_service_object_modified", EventType.SystemLog, Severity.Medium) },
        
        // Other security events
        { 4719, ("audit_policy_changed", EventType.SecurityAlert, Severity.High) },
        { 4907, ("audit_settings_modified", EventType.SecurityAlert, Severity.High) },
    };

    public async Task<NormalizedEvent> ParseAsync(EventEnvelope raw)
    {
        return await Task.FromResult(ParseWindowsEventLog(raw));
    }

    private static NormalizedEvent ParseWindowsEventLog(EventEnvelope raw)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(raw.Raw);

            var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("ns", "http://schemas.microsoft.com/win/2004/08/events/event");

            var eventIdNode = xmlDoc.SelectSingleNode("//ns:EventID", nsManager);
            var eventId = uint.TryParse(eventIdNode?.InnerText, out var id) ? id : 0;

            var systemNode = xmlDoc.SelectSingleNode("//ns:System", nsManager);
            var computer = xmlDoc.SelectSingleNode("//ns:Computer", nsManager)?.InnerText ?? string.Empty;
            var timestamp = xmlDoc.SelectSingleNode("//ns:TimeCreated/@SystemTime", nsManager)?.Value ?? string.Empty;

            var eventData = xmlDoc.SelectNodes("//ns:EventData/ns:Data", nsManager);
            var eventDataDict = new Dictionary<string, string>();
            if (eventData != null)
            {
                foreach (XmlNode dataNode in eventData)
                {
                    if (dataNode is null)
                        continue;

                    var nameAttr = dataNode.Attributes?["Name"]?.Value ?? string.Empty;
                    var value = dataNode.InnerText ?? string.Empty;
                    if (!string.IsNullOrEmpty(nameAttr))
                    {
                        eventDataDict[nameAttr] = value;
                    }
                }
            }

            var action = "unknown_action";
            var eventType = EventType.SystemLog;
            var severity = Severity.Info;

            if (EventCodeMappings.TryGetValue(eventId, out var mapping))
            {
                action = mapping.ActionName;
                eventType = mapping.EventType;
                severity = mapping.Severity;
            }

            var username = eventDataDict.ContainsKey("TargetUserName")
                ? eventDataDict["TargetUserName"]
                : eventDataDict.ContainsKey("UserName") ? eventDataDict["UserName"] : string.Empty;

            var sourceIp = eventDataDict.ContainsKey("IpAddress")
                ? eventDataDict["IpAddress"]
                : eventDataDict.ContainsKey("SourceNetworkAddress") ? eventDataDict["SourceNetworkAddress"] : string.Empty;

            if (!string.IsNullOrEmpty(sourceIp))
            {
                IpParser.TryValidateAndNormalize(sourceIp, out sourceIp);
            }

            metadata["event_id"] = eventId;
            metadata["action"] = action;
            metadata["username"] = username;
            metadata["computer"] = computer;

            if (eventDataDict.Count > 0)
            {
                metadata["event_data"] = eventDataDict;
            }

            if (!TimeParser.TryParse(timestamp, out var parsedTimestamp))
            {
                parsedTimestamp = DateTime.UtcNow;
            }

            return new NormalizedEvent
            {
                Id = raw.EventId,
                Timestamp = parsedTimestamp,
                EventType = eventType,
                Severity = severity,
                SourceIp = sourceIp,
                DeviceName = computer,
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
}
