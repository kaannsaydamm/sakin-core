using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;
using Sakin.Common.Models;
using Xunit;

namespace Sakin.Correlation.Tests.Engine;

public class RuleEvaluationStreamTests
{
    private readonly Mock<ILogger<RuleEvaluator>> _mockEvaluatorLogger;
    private readonly Mock<ILogger<RuleValidator>> _mockValidatorLogger;
    private readonly Mock<ILogger<RuleParser>> _mockParserLogger;
    private readonly RuleEvaluator _evaluator;
    private readonly RuleParser _parser;

    public RuleEvaluationStreamTests()
    {
        _mockEvaluatorLogger = new Mock<ILogger<RuleEvaluator>>();
        _mockValidatorLogger = new Mock<ILogger<RuleValidator>>();
        _mockParserLogger = new Mock<ILogger<RuleParser>>();
        
        var validator = new RuleValidator(_mockValidatorLogger.Object);
        _parser = new RuleParser(validator, _mockParserLogger.Object);
        _evaluator = new RuleEvaluator(_mockEvaluatorLogger.Object);
    }

    [Fact]
    public async Task BruteForceRule_WithMultipleFailedAttempts_ShouldTriggerAlert()
    {
        var rule = await LoadRuleAsync("failed-login-attempts");

        var eventStream = GenerateBruteForceEventStream(
            username: "admin",
            sourceIp: "192.168.1.100",
            attemptCount: 5,
            timeSpanMinutes: 4
        );

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
        result.AggregationCount.Should().BeGreaterOrEqualTo(3);
        result.Context.Should().ContainKey("username");
        result.Context.Should().ContainKey("source_ip");
    }

    [Fact]
    public async Task BruteForceRule_WithInsufficientAttempts_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("failed-login-attempts");

        var eventStream = GenerateBruteForceEventStream(
            username: "admin",
            sourceIp: "192.168.1.100",
            attemptCount: 2,
            timeSpanMinutes: 4
        );

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task BruteForceRule_WithDifferentUsers_ShouldGroupSeparately()
    {
        var rule = await LoadRuleAsync("failed-login-attempts");

        var eventStream = new List<EventEnvelope>();
        eventStream.AddRange(GenerateBruteForceEventStream("user1", "192.168.1.100", 2, 4));
        eventStream.AddRange(GenerateBruteForceEventStream("user2", "192.168.1.100", 2, 4));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task BruteForceRule_WithSameUserDifferentIPs_ShouldGroupSeparately()
    {
        var rule = await LoadRuleAsync("failed-login-attempts");

        var eventStream = new List<EventEnvelope>();
        eventStream.AddRange(GenerateBruteForceEventStream("admin", "192.168.1.100", 2, 4));
        eventStream.AddRange(GenerateBruteForceEventStream("admin", "192.168.1.101", 2, 4));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task SuspiciousDnsRule_WithMaliciousDomain_ShouldTriggerAlert()
    {
        var rule = await LoadRuleAsync("suspicious-dns-query");

        var eventStream = new List<EventEnvelope>
        {
            CreateDnsQueryEvent("malicious.com", "192.168.1.100"),
            CreateDnsQueryEvent("google.com", "192.168.1.100"),
            CreateDnsQueryEvent("evil-botnet.com", "192.168.1.101")
        };

        var maliciousEvents = new[] { eventStream[0], eventStream[2] };

        foreach (var evt in maliciousEvents)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeTrue();
            result.ShouldTriggerAlert.Should().BeTrue();
        }

        var benignResult = await _evaluator.EvaluateAsync(rule, eventStream[1]);
        benignResult.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task SuspiciousDnsRule_WithBenignDomains_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("suspicious-dns-query");

        var eventStream = new List<EventEnvelope>
        {
            CreateDnsQueryEvent("google.com", "192.168.1.100"),
            CreateDnsQueryEvent("microsoft.com", "192.168.1.100"),
            CreateDnsQueryEvent("github.com", "192.168.1.100")
        };

        foreach (var evt in eventStream)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DataExfiltrationRule_WithLargeTransfers_ShouldTriggerAlert()
    {
        var rule = await LoadRuleAsync("data-exfiltration");

        var eventStream = GenerateDataExfiltrationEventStream(
            sourceIp: "192.168.1.100",
            eventCount: 3,
            bytesPerEvent: 2000000
        );

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.IsMatch.Should().BeTrue();
        result.ShouldTriggerAlert.Should().BeTrue();
        result.AggregationCount.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task DataExfiltrationRule_WithSmallTransfers_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("data-exfiltration");

        var eventStream = GenerateDataExfiltrationEventStream(
            sourceIp: "192.168.1.100",
            eventCount: 5,
            bytesPerEvent: 500000
        );

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task DataExfiltrationRule_WithDifferentHosts_ShouldGroupSeparately()
    {
        var rule = await LoadRuleAsync("data-exfiltration");

        var eventStream = new List<EventEnvelope>();
        eventStream.AddRange(GenerateDataExfiltrationEventStream("192.168.1.100", 2, 2000000));
        eventStream.AddRange(GenerateDataExfiltrationEventStream("192.168.1.101", 2, 2000000));

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);

        result.ShouldTriggerAlert.Should().BeFalse();
    }

    [Fact]
    public async Task RareHourAccessRule_WithUnusualHourAccess_ShouldTriggerAlert()
    {
        var rule = await LoadRuleAsync("rare-hour-access");

        var eventStream = new List<EventEnvelope>
        {
            CreateFileAccessEvent("/etc/passwd", "user1", hour: 2, dayOfWeek: DayOfWeek.Monday),
            CreateFileAccessEvent("/etc/shadow", "user1", hour: 3, dayOfWeek: DayOfWeek.Tuesday),
            CreateFileAccessEvent("/var/log/auth.log", "user2", hour: 23, dayOfWeek: DayOfWeek.Wednesday)
        };

        foreach (var evt in eventStream)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeTrue();
            result.ShouldTriggerAlert.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RareHourAccessRule_WithNormalHourAccess_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("rare-hour-access");

        var eventStream = new List<EventEnvelope>
        {
            CreateFileAccessEvent("/etc/passwd", "user1", hour: 10, dayOfWeek: DayOfWeek.Monday),
            CreateFileAccessEvent("/etc/shadow", "user1", hour: 14, dayOfWeek: DayOfWeek.Tuesday),
            CreateFileAccessEvent("/var/log/auth.log", "user2", hour: 16, dayOfWeek: DayOfWeek.Wednesday)
        };

        foreach (var evt in eventStream)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeFalse();
        }
    }

    [Fact]
    public async Task RareHourAccessRule_WithWeekendAccess_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("rare-hour-access");

        var eventStream = new List<EventEnvelope>
        {
            CreateFileAccessEvent("/etc/passwd", "user1", hour: 2, dayOfWeek: DayOfWeek.Saturday),
            CreateFileAccessEvent("/etc/shadow", "user1", hour: 3, dayOfWeek: DayOfWeek.Sunday)
        };

        foreach (var evt in eventStream)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeFalse();
        }
    }

    [Fact]
    public async Task MalwareDetectionRule_WithSuspiciousProcess_ShouldTriggerAlert()
    {
        var rule = await LoadRuleAsync("malware-detection");

        var eventStream = new List<EventEnvelope>
        {
            CreateProcessExecutionEvent("suspicious.exe", @"C:\Users\Public\suspicious.exe", hasSignature: false),
            CreateProcessExecutionEvent("malware.scr", @"C:\Windows\System32\malware.scr", hasSignature: false)
        };

        foreach (var evt in eventStream)
        {
            var result = await _evaluator.EvaluateAsync(rule, evt);
            result.IsMatch.Should().BeTrue();
            result.ShouldTriggerAlert.Should().BeTrue();
        }
    }

    [Fact]
    public async Task MalwareDetectionRule_WithSignedProcess_ShouldNotTriggerAlert()
    {
        var rule = await LoadRuleAsync("malware-detection");

        var eventStream = new List<EventEnvelope>
        {
            CreateProcessExecutionEvent("notepad.exe", @"C:\Windows\System32\notepad.exe", hasSignature: true)
        };

        var result = await _evaluator.EvaluateAsync(rule, eventStream[0]);
        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task ComplexStream_WithMixedEvents_ShouldEvaluateCorrectly()
    {
        var bruteForceRule = await LoadRuleAsync("failed-login-attempts");
        var dnsRule = await LoadRuleAsync("suspicious-dns-query");

        var mixedStream = new List<EventEnvelope>();
        mixedStream.AddRange(GenerateBruteForceEventStream("admin", "192.168.1.100", 5, 4));
        mixedStream.Add(CreateDnsQueryEvent("malicious.com", "192.168.1.100"));
        mixedStream.AddRange(GenerateBruteForceEventStream("user1", "192.168.1.101", 2, 4));
        mixedStream.Add(CreateDnsQueryEvent("google.com", "192.168.1.102"));

        var bruteForceEvents = mixedStream.Where(e => 
            e.Normalized?.EventType == EventType.AuthenticationAttempt).ToList();
        var dnsEvents = mixedStream.Where(e => 
            e.Normalized?.EventType == EventType.DnsQuery).ToList();

        var bruteForceResult = await _evaluator.EvaluateWithAggregationAsync(bruteForceRule, bruteForceEvents);
        bruteForceResult.ShouldTriggerAlert.Should().BeTrue();

        var maliciousDnsResult = await _evaluator.EvaluateAsync(dnsRule, dnsEvents[0]);
        maliciousDnsResult.ShouldTriggerAlert.Should().BeTrue();

        var benignDnsResult = await _evaluator.EvaluateAsync(dnsRule, dnsEvents[1]);
        benignDnsResult.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task EventStream_WithTimestampOrdering_ShouldMaintainChronology()
    {
        var rule = await LoadRuleAsync("failed-login-attempts");

        var baseTime = DateTime.UtcNow;
        var eventStream = new List<EventEnvelope>();

        for (int i = 0; i < 5; i++)
        {
            eventStream.Add(CreateAuthenticationFailureEvent(
                "admin",
                "192.168.1.100",
                "invalid_password",
                baseTime.AddMinutes(i)
            ));
        }

        eventStream.Should().BeInAscendingOrder(e => e.Normalized!.Timestamp);

        var result = await _evaluator.EvaluateWithAggregationAsync(rule, eventStream);
        result.ShouldTriggerAlert.Should().BeTrue();
    }

    private async Task<CorrelationRule> LoadRuleAsync(string ruleId)
    {
        var rulePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "configs", "rules", $"{ruleId}.json");

        if (!File.Exists(rulePath))
        {
            rulePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..", "..",
                "configs", "rules", $"{ruleId}.json");
        }

        if (!File.Exists(rulePath))
        {
            throw new FileNotFoundException($"Rule file not found: {ruleId}");
        }

        return await _parser.ParseRuleFromFileAsync(rulePath);
    }

    private List<EventEnvelope> GenerateBruteForceEventStream(
        string username,
        string sourceIp,
        int attemptCount,
        int timeSpanMinutes)
    {
        var events = new List<EventEnvelope>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < attemptCount; i++)
        {
            var timestamp = baseTime.AddSeconds(i * (timeSpanMinutes * 60.0 / attemptCount));
            events.Add(CreateAuthenticationFailureEvent(username, sourceIp, "invalid_password", timestamp));
        }

        return events;
    }

    private List<EventEnvelope> GenerateDataExfiltrationEventStream(
        string sourceIp,
        int eventCount,
        int bytesPerEvent)
    {
        var events = new List<EventEnvelope>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < eventCount; i++)
        {
            var timestamp = baseTime.AddMinutes(i * 2);
            events.Add(CreateNetworkTrafficEvent(sourceIp, "8.8.8.8", bytesPerEvent, timestamp));
        }

        return events;
    }

    private EventEnvelope CreateAuthenticationFailureEvent(
        string username,
        string sourceIp,
        string failureReason,
        DateTime? timestamp = null)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "active_directory",
            SourceType = "auth-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp ?? DateTime.UtcNow,
                EventType = EventType.AuthenticationAttempt,
                Severity = Severity.Medium,
                SourceIp = sourceIp,
                DestinationIp = "192.168.1.10",
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "username", username },
                    { "failure_reason", failureReason },
                    { "success", false }
                }
            }
        };
    }

    private EventEnvelope CreateDnsQueryEvent(string queryName, string sourceIp)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "network-sensor",
            SourceType = "dns-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.DnsQuery,
                Severity = Severity.Info,
                SourceIp = sourceIp,
                DestinationIp = "8.8.8.8",
                SourcePort = 54321,
                DestinationPort = 53,
                Protocol = Protocol.UDP,
                Metadata = new Dictionary<string, object>
                {
                    { "queryName", queryName },
                    { "queryType", "A" }
                }
            }
        };
    }

    private EventEnvelope CreateNetworkTrafficEvent(
        string sourceIp,
        string destIp,
        int bytesSent,
        DateTime? timestamp = null)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "network-sensor",
            SourceType = "network-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp ?? DateTime.UtcNow,
                EventType = EventType.NetworkTraffic,
                Severity = Severity.Info,
                SourceIp = sourceIp,
                DestinationIp = destIp,
                Protocol = Protocol.TCP,
                Metadata = new Dictionary<string, object>
                {
                    { "bytes_sent", bytesSent },
                    { "bytes_received", 1000 },
                    { "protocol", "tcp" }
                }
            }
        };
    }

    private EventEnvelope CreateFileAccessEvent(
        string filePath,
        string username,
        int hour,
        DayOfWeek dayOfWeek)
    {
        var date = DateTime.UtcNow.Date;
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(1);
        }
        var timestamp = date.AddHours(hour);

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "file-sensor",
            SourceType = "file-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp,
                EventType = EventType.FileAccess,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.10",
                Protocol = Protocol.Unknown,
                Metadata = new Dictionary<string, object>
                {
                    { "file_path", filePath },
                    { "username", username },
                    { "action", "read" }
                }
            }
        };
    }

    private EventEnvelope CreateProcessExecutionEvent(
        string processName,
        string processPath,
        bool hasSignature)
    {
        var metadata = new Dictionary<string, object>
        {
            { "process_name", processName },
            { "process_path", processPath },
            { "username", "testuser" }
        };

        if (hasSignature)
        {
            metadata["digital_signature"] = "Microsoft Corporation";
        }

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            ReceivedAt = DateTimeOffset.UtcNow,
            Source = "process-sensor",
            SourceType = "process-sensor",
            Normalized = new NormalizedEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = EventType.ProcessExecution,
                Severity = Severity.Info,
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.100",
                Protocol = Protocol.Unknown,
                Metadata = metadata
            }
        };
    }
}
