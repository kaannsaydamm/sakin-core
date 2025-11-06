using System;
using System.Collections.Generic;
using System.Linq;
using Sakin.Common.Models;

namespace Sakin.Integration.Tests.Helpers;

public class TestDataBuilder
{
    public static List<RawEvent> CreateBruteForcedLoginSequence(
        string sourceIp = "192.168.1.100",
        string username = "admin",
        int count = 15,
        int intervalSeconds = 10)
    {
        var events = new List<RawEvent>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            events.Add(new RawEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = baseTime.AddSeconds(i * intervalSeconds),
                Source = sourceIp,
                SourceType = "EventLog",
                Payload = new Dictionary<string, object>
                {
                    { "EventCode", 4625 },
                    { "Computer", "DOMAIN-DC" },
                    { "UserName", username },
                    { "Source", "Security" }
                },
                RawPayload = $"Failed login attempt {i + 1}"
            });
        }

        return events;
    }

    public static List<NormalizedEvent> CreateAnomalousUserLoginEvents(
        string username = "admin",
        int count = 5)
    {
        var events = new List<NormalizedEvent>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            // Create logins at 3 AM (unusual time)
            var loginTime = baseTime.Date.AddHours(3).AddMinutes(i * 5);

            events.Add(new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = loginTime,
                EventType = "user-login",
                Severity = "medium",
                SourceIp = $"192.168.1.{100 + i}",
                DestinationIp = "172.16.0.1",
                SourcePort = 50000 + i,
                DestinationPort = 22,
                Protocol = "SSH",
                Username = username,
                Hostname = "production-server",
                DeviceName = "SSH-GW",
                EventPayload = new Dictionary<string, object>
                {
                    { "auth_method", "password" },
                    { "success", true }
                },
                GeoLocation = new GeoLocationData
                {
                    CountryCode = "US",
                    CountryName = "United States",
                    City = "New York",
                    Latitude = 40.7128,
                    Longitude = -74.0060
                }
            });
        }

        return events;
    }

    public static List<NormalizedEvent> CreateDuplicateEvents(
        NormalizedEvent templateEvent,
        int count = 10)
    {
        var events = new List<NormalizedEvent>();

        for (int i = 0; i < count; i++)
        {
            var duplicateEvent = new NormalizedEvent
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = templateEvent.Timestamp.AddSeconds(i),
                EventType = templateEvent.EventType,
                Severity = templateEvent.Severity,
                SourceIp = templateEvent.SourceIp,
                DestinationIp = templateEvent.DestinationIp,
                SourcePort = templateEvent.SourcePort,
                DestinationPort = templateEvent.DestinationPort,
                Protocol = templateEvent.Protocol,
                Username = templateEvent.Username,
                Hostname = templateEvent.Hostname,
                DeviceName = templateEvent.DeviceName,
                EventPayload = new Dictionary<string, object>(templateEvent.EventPayload),
                GeoLocation = templateEvent.GeoLocation
            };

            events.Add(duplicateEvent);
        }

        return events;
    }

    public static List<NormalizedEvent> CreateMultiSourceEvents()
    {
        var events = new List<NormalizedEvent>();

        // Windows Event
        events.Add(new NormalizedEvent
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            EventType = "failed-login",
            Severity = "high",
            SourceIp = "192.168.1.100",
            DestinationIp = "192.168.1.1",
            SourcePort = 50000,
            DestinationPort = 3389,
            Protocol = "RDP",
            Username = "testuser",
            Hostname = "DC-SERVER",
            DeviceName = "WIN-DC",
            EventPayload = new Dictionary<string, object>
            {
                { "event_code", 4625 },
                { "source", "windows-security" }
            },
            GeoLocation = new GeoLocationData { CountryCode = "US" }
        });

        // Syslog Event
        events.Add(new NormalizedEvent
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            EventType = "ssh-login",
            Severity = "medium",
            SourceIp = "10.0.0.50",
            DestinationIp = "10.0.0.100",
            SourcePort = 54321,
            DestinationPort = 22,
            Protocol = "SSH",
            Username = "testuser",
            Hostname = "linux-server",
            DeviceName = "SYSLOG-HOST",
            EventPayload = new Dictionary<string, object>
            {
                { "source", "syslog" },
                { "program", "sshd" }
            },
            GeoLocation = new GeoLocationData { CountryCode = "US" }
        });

        // CEF Event
        events.Add(new NormalizedEvent
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            EventType = "http-request",
            Severity = "low",
            SourceIp = "172.16.0.100",
            DestinationIp = "172.16.0.200",
            SourcePort = 56789,
            DestinationPort = 443,
            Protocol = "HTTPS",
            Username = null,
            Hostname = null,
            DeviceName = "WAF",
            EventPayload = new Dictionary<string, object>
            {
                { "source", "cef" },
                { "action", "allowed" }
            },
            GeoLocation = new GeoLocationData { CountryCode = "CN" }
        });

        return events;
    }
}
