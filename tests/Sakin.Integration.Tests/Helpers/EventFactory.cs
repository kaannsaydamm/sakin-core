using System;
using System.Collections.Generic;
using Sakin.Common.Models;

namespace Sakin.Integration.Tests.Helpers;

public static class EventFactory
{
    public static EventEnvelope CreateWindowsEventLogEnvelope(
        int eventCode = 4625,
        string? sourceIp = "192.168.1.100",
        string? username = "testuser",
        string? computerName = "TEST-PC")
    {
        var normalizedEvent = CreateNormalizedEvent(
            sourceIp: sourceIp,
            username: username,
            eventType: eventCode == 4625 ? EventType.FailedLogin : EventType.SecurityEvent);

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = sourceIp ?? "192.168.1.100",
            SourceType = "EventLog",
            Raw = $"Event ID: {eventCode}, Source IP: {sourceIp}, Username: {username}",
            Normalized = normalizedEvent,
            Enrichment = new Dictionary<string, object>
            {
                { "EventCode", eventCode },
                { "Computer", computerName ?? "TEST-PC" },
                { "Source", "Security" }
            }
        };
    }

    public static EventEnvelope CreateSyslogEnvelope(
        string? sourceIp = "10.0.0.50",
        string? hostname = "syslog-host",
        string? message = "test syslog message")
    {
        var timestamp = DateTimeOffset.UtcNow;
        var rfc5424Message = $"<134>{timestamp:MMM  d HH:mm:ss} {hostname} testapp[1234]: {message}";

        var normalizedEvent = CreateNormalizedEvent(
            sourceIp: sourceIp,
            eventType: EventType.LogEvent,
            hostname: hostname);

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = timestamp,
            Source = sourceIp ?? "10.0.0.50",
            SourceType = "Syslog",
            Raw = rfc5424Message,
            Normalized = normalizedEvent,
            Enrichment = new Dictionary<string, object>
            {
                { "hostname", hostname ?? "syslog-host" },
                { "program", "testapp" },
                { "message", message ?? "test syslog message" }
            }
        };
    }

    public static EventEnvelope CreateFailedLoginEnvelope(
        string sourceIp = "192.168.1.100",
        string username = "admin")
    {
        return CreateWindowsEventLogEnvelope(
            eventCode: 4625,
            sourceIp: sourceIp,
            username: username,
            computerName: "DOMAIN-DC");
    }

    public static EventEnvelope CreateHTTPCEFEnvelope(
        string sourceIp = "172.16.0.100",
        string? destinationIp = "172.16.0.200",
        int destinationPort = 443)
    {
        var cefMessage = $"CEF:0|Test|Device|1.0|1001|HTTP Request|Medium|" +
            $"src={sourceIp} dst={destinationIp} dpt={destinationPort} " +
            $"proto=tcp act=allowed cs1Label=RuleID cs1=RULE001";

        var normalizedEvent = CreateNormalizedEvent(
            sourceIp: sourceIp,
            eventType: EventType.NetworkTraffic);

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = sourceIp,
            SourceType = "CEF",
            Raw = cefMessage,
            Normalized = normalizedEvent,
            Enrichment = new Dictionary<string, object>
            {
                { "vendor", "Test" },
                { "product", "Device" },
                { "version", "1.0" },
                { "destination_port", destinationPort }
            }
        };
    }

    public static NormalizedEvent CreateNormalizedEvent(
        string? sourceIp = "192.168.1.100",
        EventType? eventType = null,
        Severity? severity = null,
        string? username = "testuser",
        string? hostname = "test-host")
    {
        return new NormalizedEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType ?? EventType.SecurityEvent,
            Severity = severity ?? Severity.Medium,
            SourceIp = sourceIp ?? "192.168.1.100",
            DestinationIp = "172.16.0.200",
            SourcePort = 12345,
            DestinationPort = 443,
            Protocol = Protocol.TCP,
            Payload = System.Text.Json.JsonSerializer.Serialize(new { event_code = 4625, raw_message = "test event" }),
            Metadata = new Dictionary<string, object>
            {
                { "event_code", 4625 }
            },
            DeviceName = "TEST-DEVICE",
            Username = username,
            Hostname = hostname
        };
    }
}
